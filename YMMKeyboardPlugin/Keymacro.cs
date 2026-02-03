using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace YMMKeyboardPlugin
{
    public class Keymacro : IDisposable
    {
        private SerialKeyboardLink? _link;

        // UIDごとの割り当てテーブル
        // UID → (SW番号 → Action)
        private readonly Dictionary<string, Dictionary<int, Action>> _macros
            = new();

        public void Initialize()
        {
            // ★ 実際に使うCOMポートを指定
            // ※ 固定が嫌なら後で設定化
            _link = new SerialKeyboardLink("COM5");

            _link.DeviceDetected += OnDeviceDetected;
            _link.KeyEventReceived += OnKeyEventReceived;

            _link.Start();

            Debug.WriteLine("[Keymacro] Initialized");
        }

        private void OnDeviceDetected(SerialKeyboardDevice device)
        {
            Debug.WriteLine($"[Keymacro] Device detected: {device.Uid}");

            // 初回検出時に割り当てテーブルを用意
            if (!_macros.ContainsKey(device.Uid))
            {
                _macros[device.Uid] = CreateDefaultMapping(device.Uid);
            }

            // UI通知（確認用）

                MessageBox.Show($"デバイス接続: {device.Uid}");
           
        }

        private void OnKeyEventReceived(SerialKeyboardDevice device, KeyEvent e)
        {
            // 押下のみ反応（必要ならRも処理）
            if (!e.IsPressed)
                return;

            if (!_macros.TryGetValue(device.Uid, out var deviceMap))
                return;

            if (!deviceMap.TryGetValue(e.SwitchId, out var action))
                return;

            Debug.WriteLine(
                $"[Keymacro] {device.Uid} SW_{e.SwitchId} pressed");

            // ★ マクロ実行
            action.Invoke();
        }

        private Dictionary<int, Action> CreateDefaultMapping(string uid)
        {
            var map = new Dictionary<int, Action>();

            // ====== サンプル割り当て ======

            map[0] = () =>
            {
                ShowToast("SW_0実行");
            };

            map[1] = () =>
            {
                ShowToast("SW_1実行");
            };
            map[2] = () =>
            {
                ShowToast("SW_2実行");
            };
            map[3] = () =>
            {
                ShowToast("SW_3実行");
            };
            map[4] = () =>
            {
                ShowToast("SW_4実行");
            };
            map[5] = () =>
            {
                ShowToast("SW_5実行");
            };
            map[6] = () =>
            {
                ShowToast("SW_6実行");
            };
            map[7] = () =>
            {
                ShowToast("SW_7実行");
            };
            map[8] = () =>
            {
                ShowToast("SW_8実行");
            };
            map[9] = () =>
            {
                ShowToast("SW_9実行");
            };
            map[10] = () =>
            {
                ShowToast("SW_10実行");
            };
            map[11] = () =>
            {
                ShowToast("SW_11実行");
            };
            map[12] = () =>
            {
                ShowToast("SW_12実行");
            };
            map[13] = () =>
            {
                ShowToast("SW_13実行");
            };

            map[14] = () =>
            {
                ShowToast("SW_14実行");
            };
            map[15] = () =>
            {
                ShowToast("SW_15実行");
            };
            map[16] = () =>
            {
                ShowToast("SW_16実行");
            };
            map[17] = () =>
            {
                ShowToast("SW_17実行");
            };
            map[18] = () =>
            {
                ShowToast("SW_18実行");
            };
            map[19] = () =>
            {
                ShowToast("SW_19実行");
            };
            map[20] = () =>
            {
                ShowToast("SW_20実行");
            };
            map[21] = () =>
            {
                ShowToast("SW_21実行");
            };
            map[22] = () =>
            {
                ShowToast("SW_22実行");
            };
            map[23] = () =>
            {
                ShowToast("SW_23実行");
            };
            map[24] = () =>
            {
                ShowToast("SW_24実行");
            };
            map[25] = () =>
            {
                ShowToast("SW_25実行");
            };
            map[26] = () =>
            {
                ShowToast("SW_26実行");
            };
            map[27] = () =>
            {
                ShowToast("SW_27実行");
            };
            map[28] = () =>
            {
                ShowToast("SW_28実行");
            };
            map[29] = () =>
            {
                ShowToast("SW_29実行");
            };
            map[41] = () =>
            {
                ShowToast("SW_41実行");
            };

            // ==============================

            return map;
        }

        private void ShowToast(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message);
            });
        }

        public void Dispose()
        {
            _link?.Dispose();
            _link = null;
        }
    }
}
