using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YMMKeyboardPlugin
{
    public class SerialKeyboardLink : IDisposable
    {
        private readonly string _portName;
        private SerialPort? _port;
        private CancellationTokenSource? _cts;

        private readonly Dictionary<string, SerialKeyboardDevice> _devices = new();

        // ★ デバイス検出イベント
        public event Action<SerialKeyboardDevice>? DeviceDetected;

        // ★ キーイベント受信
        public event Action<SerialKeyboardDevice, KeyEvent>? KeyEventReceived;

        public SerialKeyboardLink(string portName)
        {
            _portName = portName;
        }

        public void Start()
        {
            if (_port != null)
                return;

            _cts = new CancellationTokenSource();

            _port = new SerialPort(_portName, 115200)
            {
                NewLine = "\n",
                Encoding = Encoding.ASCII,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };

            _port.Open();

            Task.Run(() => ReadLoop(_cts.Token));
        }

        private void ReadLoop(CancellationToken token)
        {
            Debug.WriteLine($"[SerialKeyboardLink] Start reading {_portName}");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    string line = _port!.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    Debug.WriteLine($"[SERIAL] {line}");

                    // 例: 504434042839481c:P:SW_12
                    var parts = line.Split(':');
                    if (parts.Length != 3)
                        continue;

                    string uid = parts[0];
                    string state = parts[1];
                    string sw = parts[2];

                    if (!sw.StartsWith("SW_"))
                        continue;

                    if (!int.TryParse(sw.Substring(3), out int switchId))
                        continue;

                    // ★ 初めて見るUIDなら即デバイス登録
                    if (!_devices.TryGetValue(uid, out var device))
                    {
                        device = new SerialKeyboardDevice(uid);
                        _devices[uid] = device;
                        //
                        //MessageBox.Show("デバイス検出: " + uid);
                        DeviceDetected?.Invoke(device);
                    }

                    var keyEvent = new KeyEvent
                    {
                        Uid = uid,
                        IsPressed = state == "P",
                        SwitchId = switchId
                    };

                    KeyEventReceived?.Invoke(device, keyEvent);
                }
                catch (TimeoutException)
                {
                    // 正常（無視）
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SerialKeyboardLink] Error: {ex.Message}");
                }
            }

            Debug.WriteLine($"[SerialKeyboardLink] Stop reading {_portName}");
        }

        public void Stop()
        {
            _cts?.Cancel();

            if (_port != null)
            {
                try { _port.Close(); } catch { }
                _port.Dispose();
                _port = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
