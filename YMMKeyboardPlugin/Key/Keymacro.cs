using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin
{
    public class Keymacro : IDisposable
    {
        private readonly Dictionary<string, SerialKeyboardLink> links = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<int, Action>> macros = new();
        private readonly keyboardViewModel mp3Vm = new();

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

            if (!e.IsPressed)
            {
                Debug.WriteLine("[Keymacro] Released -> ignored");
                return;
            }

            if (!macros.TryGetValue(device.Uid, out var deviceMap))
            {
                Debug.WriteLine("[Keymacro] No mapping for UID");
                return;
            }

            if (!deviceMap.TryGetValue(e.SwitchId, out var action))
            {
                Debug.WriteLine("[Keymacro] No action for this switch");
                return;
            }

            Debug.WriteLine("[Keymacro] Action FOUND -> Invoke");

            try
            {
                action.Invoke();
                Debug.WriteLine("[Keymacro] Action Invoke DONE");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Keymacro] Action Invoke ERROR");
                Debug.WriteLine(ex.ToString());
                MessageBox.Show(ex.ToString());
            }
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
        }
    }
}
