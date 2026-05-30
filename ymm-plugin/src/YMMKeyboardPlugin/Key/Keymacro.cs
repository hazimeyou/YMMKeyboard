using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin
{
    public class Keymacro : IDisposable
    {
        private const int SingleKeyDelayMs = 35;
        private static readonly HashSet<string> immediateSwitches = new(StringComparer.OrdinalIgnoreCase)
        {
            "SW36", // rotary clockwise
            "SW37", // rotary counter-clockwise
        };
        private static readonly bool verboseLatencyLog =
            string.Equals(Environment.GetEnvironmentVariable("YMMK_VERBOSE_LATENCY"), "1", StringComparison.Ordinal);

        private readonly Dictionary<string, SerialKeyboardLink> links = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> pressedSwitches = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> consumedSwitches = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, CancellationTokenSource>> pendingSingleActions = new(StringComparer.OrdinalIgnoreCase);
        private readonly object stateLock = new();

        public void Initialize()
        {
            Debug.WriteLine("[Keymacro] Initialize START");
            YMMKeyboardSettings.ConnectionRequested += OnConnectionRequested;
            YMMKeyboardSettings.DisconnectionRequested += OnDisconnectionRequested;
            YMMKeyboardSettings.SettingsLoaded += OnSettingsLoaded;
            ConnectStartupPorts();
            Debug.WriteLine("[Keymacro] Initialize END");
        }

        private void OnSettingsLoaded()
        {
            ConnectStartupPorts();
        }

        private void ConnectStartupPorts()
        {
            foreach (var portName in YMMKeyboardSettings.Current.GetStartupPortNames())
                ConnectPort(portName);
        }

        private void OnConnectionRequested(string portName)
        {
            ConnectPort(portName);
        }

        private void OnDisconnectionRequested(string portName)
        {
            DisconnectPort(portName);
            Debug.WriteLine($"[Keymacro] Disconnected by request: {portName}");
        }

        private void ConnectPort(string? portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                Debug.WriteLine("[Keymacro] COM port is not configured");
                return;
            }

            if (links.ContainsKey(portName))
            {
                Debug.WriteLine($"[Keymacro] Already connected: {portName}");
                return;
            }

            try
            {
                var link = new SerialKeyboardLink(portName);
                link.DeviceDetected += OnDeviceDetected;
                link.KeyEventReceived += OnKeyEventReceived;
                link.Start();
                links[portName] = link;
                Debug.WriteLine($"[Keymacro] Connected to {portName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Keymacro] Connection error: {ex}");
                MessageBox.Show($"COMポート {portName} に接続できませんでした。\n{ex.Message}");
            }
        }

        private void DisconnectPort(string? portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                return;

            if (!links.TryGetValue(portName, out var link))
                return;

            var knownUids = link.KnownUids.ToArray();
            link.DeviceDetected -= OnDeviceDetected;
            link.KeyEventReceived -= OnKeyEventReceived;
            link.Dispose();
            links.Remove(portName);

            foreach (var uid in knownUids)
            {
                ClearDeviceState(uid);
            }
        }

        private void DisconnectAll()
        {
            foreach (var portName in links.Keys.ToList())
                DisconnectPort(portName);
        }

        private void OnDeviceDetected(SerialKeyboardDevice device)
        {
            Debug.WriteLine($"[Keymacro] DeviceDetected UID={device.Uid}");
            YMMKeyboardSettings.Current.RegisterKnownDeviceUid(device.Uid);
        }

        private void OnKeyEventReceived(SerialKeyboardDevice device, KeyEvent e)
        {
            Debug.WriteLine($"[Keymacro] KeyEventReceived UID={device.Uid} SW={e.SwitchId} Pressed={e.IsPressed}");

            if (!SwitchLayout.TryGetSwitchName(e.SwitchId, out var switchName))
            {
                Debug.WriteLine("[Keymacro] Unknown switch id");
                return;
            }

            if (e.IsPressed)
                HandleKeyPressed(device.Uid, switchName, e.ReceivedAtUtc);
            else
                HandleKeyReleased(device.Uid, switchName);
        }

        private void HandleKeyPressed(string uid, string switchName, DateTime receivedAtUtc)
        {
            ButtonConfig? comboConfig = null;
            string combinationKey = string.Empty;

            lock (stateLock)
            {
                var pressed = GetOrCreateSwitchSet(pressedSwitches, uid);
                pressed.Add(switchName);

                if (pressed.Count == 1)
                {
                    if (immediateSwitches.Contains(switchName))
                    {
                        CancelPendingSingles(uid, pressed);
                        LogLatency($"Immediate action uid={uid} switch={switchName}", receivedAtUtc);
                        MappingConverter.ExecuteDeviceSwitch(uid, switchName);
                        return;
                    }

                    ScheduleSingleAction(uid, switchName, receivedAtUtc);
                    return;
                }

                combinationKey = SwitchLayout.NormalizeCombination(pressed);
                comboConfig = YMMKeyboardSettings.Current.GetDeviceComboButtonConfig(uid, combinationKey);
                if (!HasExecutableAction(comboConfig))
                    return;

                CancelPendingSingles(uid, pressed);
                var consumed = GetOrCreateSwitchSet(consumedSwitches, uid);
                foreach (var pressedSwitch in pressed)
                    consumed.Add(pressedSwitch);
            }

            if (comboConfig is not null)
            {
                LogLatency($"Combo action uid={uid} combo={combinationKey}", receivedAtUtc);
                MappingConverter.ExecuteAction(comboConfig.ActionName, comboConfig.Parameter, combinationKey, uid);
            }
        }

        private void HandleKeyReleased(string uid, string switchName)
        {
            lock (stateLock)
            {
                if (pressedSwitches.TryGetValue(uid, out var pressed))
                {
                    pressed.Remove(switchName);
                    if (pressed.Count == 0)
                        pressedSwitches.Remove(uid);
                }

                if (consumedSwitches.TryGetValue(uid, out var consumed))
                {
                    consumed.Remove(switchName);
                    if (consumed.Count == 0)
                        consumedSwitches.Remove(uid);
                }
            }
        }

        private void ScheduleSingleAction(string uid, string switchName, DateTime receivedAtUtc)
        {
            var pendingByUid = GetOrCreatePendingSingles(uid);
            if (pendingByUid.TryGetValue(switchName, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            var cts = new CancellationTokenSource();
            pendingByUid[switchName] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(SingleKeyDelayMs, cts.Token);

                    lock (stateLock)
                    {
                        if (cts.IsCancellationRequested)
                            return;

                        if (consumedSwitches.TryGetValue(uid, out var consumed) && consumed.Contains(switchName))
                            return;

                        RemovePendingSingle(uid, switchName);
                    }

                    LogLatency($"Single action uid={uid} switch={switchName}", receivedAtUtc);
                    MappingConverter.ExecuteDeviceSwitch(uid, switchName);
                }
                catch (TaskCanceledException)
                {
                }
                finally
                {
                    cts.Dispose();
                }
            });
        }

        private static int GetLatencyMs(DateTime receivedAtUtc)
        {
            var elapsed = DateTime.UtcNow - receivedAtUtc;
            return elapsed <= TimeSpan.Zero ? 0 : (int)elapsed.TotalMilliseconds;
        }

        private static void LogLatency(string prefix, DateTime receivedAtUtc)
        {
            if (!verboseLatencyLog)
                return;
            PluginLogger.Info("Keymacro", $"{prefix} latencyMs={GetLatencyMs(receivedAtUtc)}");
        }

        private void CancelPendingSingles(string uid, IEnumerable<string> switchNames)
        {
            if (!pendingSingleActions.TryGetValue(uid, out var pendingByUid))
                return;

            foreach (var switchName in switchNames)
            {
                if (!pendingByUid.TryGetValue(switchName, out var cts))
                    continue;

                cts.Cancel();
                pendingByUid.Remove(switchName);
            }

            if (pendingByUid.Count == 0)
                pendingSingleActions.Remove(uid);
        }

        private static bool HasExecutableAction(ButtonConfig config)
        {
            return !string.IsNullOrWhiteSpace(config.ActionName)
                && config.ActionName != MappingConverter.NoneActionName;
        }

        private HashSet<string> GetOrCreateSwitchSet(Dictionary<string, HashSet<string>> store, string uid)
        {
            if (!store.TryGetValue(uid, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                store[uid] = set;
            }

            return set;
        }

        private Dictionary<string, CancellationTokenSource> GetOrCreatePendingSingles(string uid)
        {
            if (!pendingSingleActions.TryGetValue(uid, out var pendingByUid))
            {
                pendingByUid = new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
                pendingSingleActions[uid] = pendingByUid;
            }

            return pendingByUid;
        }

        private void RemovePendingSingle(string uid, string switchName)
        {
            if (!pendingSingleActions.TryGetValue(uid, out var pendingByUid))
                return;

            pendingByUid.Remove(switchName);
            if (pendingByUid.Count == 0)
                pendingSingleActions.Remove(uid);
        }

        private void ClearDeviceState(string uid)
        {
            lock (stateLock)
            {
                if (pendingSingleActions.TryGetValue(uid, out var pendingByUid))
                {
                    foreach (var cts in pendingByUid.Values)
                        cts.Cancel();
                    pendingSingleActions.Remove(uid);
                }

                pressedSwitches.Remove(uid);
                consumedSwitches.Remove(uid);
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("[Keymacro] Dispose");
            YMMKeyboardSettings.ConnectionRequested -= OnConnectionRequested;
            YMMKeyboardSettings.DisconnectionRequested -= OnDisconnectionRequested;
            YMMKeyboardSettings.SettingsLoaded -= OnSettingsLoaded;
            DisconnectAll();

            lock (stateLock)
            {
                foreach (var pendingByUid in pendingSingleActions.Values)
                {
                    foreach (var cts in pendingByUid.Values)
                        cts.Cancel();
                }

                pendingSingleActions.Clear();
                pressedSwitches.Clear();
                consumedSwitches.Clear();
            }
        }
    }
}
