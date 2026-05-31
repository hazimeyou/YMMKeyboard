using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HidSharp;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Hid;

public sealed class HidKeyboardLink : IKeyboardLink
{
    private static readonly Regex serialEventPattern = new(
        @"(?<uid>[0-9a-fA-F]+):(?<state>[PR]):SW_(?<switch>\d+)",
        RegexOptions.Compiled);

    private readonly int? vendorId;
    private readonly int? productId;
    private readonly Dictionary<string, SerialKeyboardDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object devicesLock = new();
    private CancellationTokenSource? cts;

    public event Action<SerialKeyboardDevice>? DeviceDetected;
    public event Action<SerialKeyboardDevice, KeyEvent>? KeyEventReceived;

    public IReadOnlyList<string> KnownUids
    {
        get
        {
            lock (devicesLock)
            {
                return devices.Keys.ToArray();
            }
        }
    }

    public HidKeyboardLink(int? vendorId, int? productId)
    {
        this.vendorId = vendorId;
        this.productId = productId;
    }

    public void Start()
    {
        if (cts is not null)
            return;

        cts = new CancellationTokenSource();
        Task.Run(() => RunLoop(cts.Token));
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                foreach (var hidDevice in GetCandidateDevices())
                {
                    if (token.IsCancellationRequested)
                        break;

                    await ReadDeviceAsync(hidDevice, token);
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("HidKeyboardLink", "HID read loop failed.", ex);
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private IEnumerable<HidDevice> GetCandidateDevices()
    {
        var list = DeviceList.Local.GetHidDevices();
        if (vendorId.HasValue)
            list = list.Where(d => d.VendorID == vendorId.Value);
        if (productId.HasValue)
            list = list.Where(d => d.ProductID == productId.Value);
        return list.ToArray();
    }

    private async Task ReadDeviceAsync(HidDevice hidDevice, CancellationToken token)
    {
        if (!hidDevice.TryOpen(out var stream))
            return;

        using (stream)
        {
            var uid = BuildSyntheticUid(hidDevice.DevicePath ?? $"{hidDevice.VendorID:X4}:{hidDevice.ProductID:X4}");
            var device = GetOrCreateDevice(uid, out var isNew);
            if (isNew)
            {
                PluginLogger.Info("HidKeyboardLink", $"HID device detected: {uid} ({hidDevice.VendorID:X4}:{hidDevice.ProductID:X4})");
                DeviceDetected?.Invoke(device);
            }

            var reportBuffer = new byte[Math.Max(64, hidDevice.GetMaxInputReportLength())];
            while (!token.IsCancellationRequested)
            {
                int length;
                try
                {
                    length = await stream.ReadAsync(reportBuffer, 0, reportBuffer.Length, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (length <= 0)
                    break;

                ParseReport(uid, device, reportBuffer, length);
            }
        }
    }

    private void ParseReport(string fallbackUid, SerialKeyboardDevice device, byte[] report, int length)
    {
        var ascii = Encoding.ASCII.GetString(report, 0, length).Trim('\0', '\r', '\n', ' ');
        if (ascii.Length == 0)
            return;

        var match = serialEventPattern.Match(ascii);
        if (!match.Success)
            return;

        var uid = match.Groups["uid"].Value.ToLowerInvariant();
        var state = match.Groups["state"].Value;
        if (!int.TryParse(match.Groups["switch"].Value, out var switchId))
            return;

        var actualDevice = device;
        if (!string.Equals(uid, fallbackUid, StringComparison.OrdinalIgnoreCase))
        {
            actualDevice = GetOrCreateDevice(uid, out var isNewUid);
            if (isNewUid)
                DeviceDetected?.Invoke(actualDevice);
        }

        KeyEventReceived?.Invoke(actualDevice, new KeyEvent
        {
            Uid = uid,
            IsPressed = state == "P",
            SwitchId = switchId,
            ReceivedAtUtc = DateTime.UtcNow,
        });
    }

    private SerialKeyboardDevice GetOrCreateDevice(string uid, out bool isNew)
    {
        lock (devicesLock)
        {
            if (devices.TryGetValue(uid, out var existing))
            {
                isNew = false;
                return existing;
            }

            var created = new SerialKeyboardDevice(uid);
            devices[uid] = created;
            isNew = true;
            return created;
        }
    }

    private static string BuildSyntheticUid(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
