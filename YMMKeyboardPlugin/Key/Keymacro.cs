using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin
{
    public class Keymacro : IDisposable
    {
        private const int SingleKeyDelayMs = 120;

        private readonly Dictionary<string, SerialKeyboardLink> links = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<int, Action>> macros = new();
        private readonly LegacyKeyboardViewModel mp3Vm = new();
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

            link.DeviceDetected -= OnDeviceDetected;
            link.KeyEventReceived -= OnKeyEventReceived;
            link.Dispose();
            links.Remove(portName);
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

            if (!macros.ContainsKey(device.Uid))
            {
                Debug.WriteLine("[Keymacro] Create mapping");
                macros[device.Uid] = CreateDeviceMap(device.Uid);
            }
            else
            {
                Debug.WriteLine("[Keymacro] Mapping already exists");
            }
        }

        private static Dictionary<int, Action> CreateDeviceMap(string uid)
        {
            var map = new Dictionary<int, Action>();
            foreach (var item in SwitchLayout.All)
            {
                var switchName = item.SwitchName;
                map[item.SwitchId] = () => MappingConverter.ExecuteDeviceSwitch(uid, switchName);
            }

            return map;
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
                HandleKeyPressed(device.Uid, switchName);
            else
                HandleKeyReleased(device.Uid, switchName);
        }

        private void HandleKeyPressed(string uid, string switchName)
        {
            ButtonConfig? comboConfig = null;
            string combinationKey = string.Empty;

            lock (stateLock)
            {
                var pressed = GetOrCreateSwitchSet(pressedSwitches, uid);
                pressed.Add(switchName);

                if (pressed.Count == 1)
                {
                    ScheduleSingleAction(uid, switchName);
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
                MappingConverter.ExecuteAction(comboConfig.ActionName, comboConfig.Parameter, combinationKey, uid);
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

        private void ScheduleSingleAction(string uid, string switchName)
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

        public static void ShowToast(string message)
        {
            Debug.WriteLine("[Toast] " + message);
            Application.Current?.Dispatcher.Invoke(() =>
            {
            });
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
