using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin
{
    public class SerialKeyboardLink : IKeyboardLink
    {
        private static readonly Regex serialEventPattern = new(
            @"(?<uid>[0-9a-fA-F]+):(?<state>[PR]):SW_(?<switch>\d+)",
            RegexOptions.Compiled);
        private static readonly Regex rotaryRawEventPattern = new(
            @"^(?:(?<uid>[0-9a-fA-F]+):)?(?<switch>SW_?(?<switchId>\d+)):(?<state>[PR])$",
            RegexOptions.Compiled);
        private static readonly Regex ansiEscapePattern = new(
            @"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\a]*(?:\a|\x1B\\))",
            RegexOptions.Compiled);
        private static readonly bool verboseSerialLog =
            string.Equals(Environment.GetEnvironmentVariable("YMMK_VERBOSE_SERIAL"), "1", StringComparison.Ordinal);

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
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true
            };

            _port.Open();
            // Some USB CDC firmwares require a post-open DTR assert edge.
            _port.DtrEnable = false;
            Thread.Sleep(40);
            _port.DtrEnable = true;
            Thread.Sleep(180);
            _port.DiscardInBuffer();
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

                    var normalizedLine = NormalizeSerialLine(line);
                    Debug.WriteLine($"[SERIAL] {normalizedLine}");
                    if (verboseSerialLog)
                        PluginLogger.Info("SerialKeyboardLink", $"RX {_portName}: {normalizedLine}");

                    var match = serialEventPattern.Match(normalizedLine);
                    var isRotaryRaw = false;
                    if (!match.Success)
                    {
                        match = rotaryRawEventPattern.Match(normalizedLine);
                        isRotaryRaw = match.Success;
                    }

                    if (!match.Success)
                        continue;

                    var uid = match.Groups["uid"].Success && !string.IsNullOrWhiteSpace(match.Groups["uid"].Value)
                        ? match.Groups["uid"].Value.ToLowerInvariant()
                        : _portName.ToLowerInvariant();
                    var state = match.Groups["state"].Value;
                    var switchGroupName = isRotaryRaw ? "switchId" : "switch";
                    if (!int.TryParse(match.Groups[switchGroupName].Value, out var switchId))
                        continue;
                    switchId = NormalizeRotarySwitchId(switchId);

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
                        TransportType = "Serial",
                        SourceDevice = _portName,
                        RawInput = normalizedLine,
                        InputId = $"{uid}:{state}:SW_{switchId:00}",
                        IsPressed = state == "P",
                        SwitchId = switchId,
                        ReceivedAtUtc = DateTime.UtcNow
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

        private static string NormalizeSerialLine(string line)
        {
            // ターミナル制御シーケンスや不可視文字が混入してもイベント行を抽出できるよう正規化する。
            var stripped = ansiEscapePattern.Replace(line, string.Empty);
            var sb = new StringBuilder(stripped.Length);
            foreach (var ch in stripped)
            {
                if (!char.IsControl(ch) || ch == '\t')
                    sb.Append(ch);
            }

            return sb.ToString().Trim();
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

        private static int NormalizeRotarySwitchId(int switchId)
        {
            return switchId switch
            {
                36 => 37,
                37 => 36,
                _ => switchId
            };
        }
    }
}
