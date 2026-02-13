using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace YMMKeyboardPlugin
{
    public class Keymacro : IDisposable
    {
        private SerialKeyboardLink? _link;

        private readonly Dictionary<string, Dictionary<int, Action>> _macros
            = new();

        private readonly Mp3InsertViewModel _mp3Vm = new();

        public void Initialize()
        {
            Debug.WriteLine("[Keymacro] Initialize START");

            _link = new SerialKeyboardLink("COM5");

            _link.DeviceDetected += OnDeviceDetected;
            _link.KeyEventReceived += OnKeyEventReceived;

            _link.Start();

            Debug.WriteLine("[Keymacro] Initialize END");
            //MessageBox.Show("Keymacro Initialize 完了");
        }

        private void OnDeviceDetected(SerialKeyboardDevice device)
        {
            Debug.WriteLine($"[Keymacro] DeviceDetected UID={device.Uid}");
           // MessageBox.Show($"DeviceDetected\nUID={device.Uid}");

            if (!_macros.ContainsKey(device.Uid))
            {
                Debug.WriteLine("[Keymacro] Create mapping");
                _macros[device.Uid] =
                    CreateDefaultMapping.Mapping(device.Uid, _mp3Vm);
            }
            else
            {
                Debug.WriteLine("[Keymacro] Mapping already exists");
            }
        }

        private void OnKeyEventReceived(SerialKeyboardDevice device, KeyEvent e)
        {
            Debug.WriteLine(
                $"[Keymacro] KeyEventReceived UID={device.Uid} SW={e.SwitchId} Pressed={e.IsPressed}");

            if (!e.IsPressed)
            {
                Debug.WriteLine("[Keymacro] Released → ignored");
                return;
            }

            if (!_macros.TryGetValue(device.Uid, out var deviceMap))
            {
                Debug.WriteLine("[Keymacro] No mapping for UID");
                return;
            }

            if (!deviceMap.TryGetValue(e.SwitchId, out var action))
            {
                Debug.WriteLine("[Keymacro] No action for this switch");
                return;
            }

            Debug.WriteLine("[Keymacro] Action FOUND → Invoke");

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
                //MessageBox.Show(message);
            });
        }

        public void Dispose()
        {
            Debug.WriteLine("[Keymacro] Dispose");
            _link?.Dispose();
            _link = null;
        }
    }
}
