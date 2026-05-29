using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin
{
    public class SerialKeyboardLink : IDisposable
    {
        private readonly string _portName;
        private SerialPort? _port;
        private CancellationTokenSource? _cts;

        private readonly Dictionary<string, SerialKeyboardDevice> _devices = new();
        private readonly object _devicesLock = new();

        public event Action<SerialKeyboardDevice>? DeviceDetected;
        public event Action<SerialKeyboardDevice, KeyEvent>? KeyEventReceived;

        public IReadOnlyList<string> KnownUids
        {
            get
            {
                lock (_devicesLock)
                {
                    return _devices.Keys.ToList();
                }
            }
        }

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
            PluginLogger.Info("SerialKeyboardLink", $"Port opened: {_portName}");
            Task.Run(() => ReadLoop(_cts.Token));
        }

        private void ReadLoop(CancellationToken token)
        {
            Debug.WriteLine($"[SerialKeyboardLink] Start reading {_portName}");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var line = _port!.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    Debug.WriteLine($"[SERIAL] {line}");
                    PluginLogger.Info("SerialKeyboardLink", $"RX {_portName}: {line}");

                    var parts = line.Split(':');
                    if (parts.Length != 3)
                        continue;

                    var uid = parts[0];
                    var state = parts[1];
                    var sw = parts[2];

                    if (!sw.StartsWith("SW_"))
                        continue;

                    if (!int.TryParse(sw.Substring(3), out var switchId))
                        continue;

                    SerialKeyboardDevice device;
                    bool isNewDevice;
                    lock (_devicesLock)
                    {
                        if (!_devices.TryGetValue(uid, out device!))
                        {
                            device = new SerialKeyboardDevice(uid);
                            _devices[uid] = device;
                            isNewDevice = true;
                        }
                        else
                        {
                            isNewDevice = false;
                        }
                    }

                    if (isNewDevice)
                    {
                        PluginLogger.Info("SerialKeyboardLink", $"Device detected on {_portName}: {uid}");
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
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SerialKeyboardLink] Error: {ex.Message}");
                    PluginLogger.Error("SerialKeyboardLink", $"Read loop error on {_portName}", ex);
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
                PluginLogger.Info("SerialKeyboardLink", $"Port closed: {_portName}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
