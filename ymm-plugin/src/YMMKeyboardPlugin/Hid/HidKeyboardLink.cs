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
    private readonly string productNameFilter;
    private readonly string manufacturerFilter;
    private readonly Dictionary<string, SerialKeyboardDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> workers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object lockObj = new();
    private CancellationTokenSource? cts;

    public event Action<SerialKeyboardDevice>? DeviceDetected;
    public event Action<SerialKeyboardDevice, KeyEvent>? KeyEventReceived;

    public IReadOnlyList<string> KnownUids
    {
        get
        {
            lock (lockObj)
            {
                return devices.Keys.ToArray();
            }
        }
    }

    public HidKeyboardLink(int? vendorId, int? productId, string productNameFilter, string manufacturerFilter)
    {
        this.vendorId = vendorId;
        this.productId = productId;
        this.productNameFilter = (productNameFilter ?? string.Empty).Trim();
        this.manufacturerFilter = (manufacturerFilter ?? string.Empty).Trim();
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

                    var path = hidDevice.DevicePath ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    lock (lockObj)
                    {
                        if (workers.ContainsKey(path))
                            continue;

                        workers[path] = Task.Run(() => ReadDeviceLoop(hidDevice, path, token), token);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("HidKeyboardLink", "HID manager loop failed.", ex);
            }

            CleanupCompletedWorkers();

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

        if (!string.IsNullOrWhiteSpace(productNameFilter))
        {
            list = list.Where(d =>
                (d.GetProductName() ?? string.Empty).Contains(productNameFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(manufacturerFilter))
        {
            list = list.Where(d =>
                (d.GetManufacturer() ?? string.Empty).Contains(manufacturerFilter, StringComparison.OrdinalIgnoreCase));
        }

        return list.ToArray();
    }

    private async Task ReadDeviceLoop(HidDevice hidDevice, string path, CancellationToken token)
    {
        try
        {
            if (!hidDevice.TryOpen(out var stream))
                return;

            using (stream)
            {
                var stableUid = BuildStableSyntheticUid(hidDevice);
                var device = GetOrCreateDevice(stableUid, out var isNew);
                if (isNew)
                {
                    PluginLogger.Info("HidKeyboardLink",
                        $"HID device detected: {stableUid} ({hidDevice.VendorID:X4}:{hidDevice.ProductID:X4}, product={hidDevice.GetProductName()}, maker={hidDevice.GetManufacturer()})");
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

                    ParseReport(stableUid, device, reportBuffer, length);
                }
            }
        }
        catch (Exception ex)
        {
            PluginLogger.Error("HidKeyboardLink", $"HID worker failed. path={path}", ex);
        }
        finally
        {
            lock (lockObj)
            {
                workers.Remove(path);
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
        lock (lockObj)
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

    private static string BuildStableSyntheticUid(HidDevice hidDevice)
    {
        var identity = string.Join("|",
            hidDevice.VendorID.ToString("X4"),
            hidDevice.ProductID.ToString("X4"),
            hidDevice.GetManufacturer() ?? string.Empty,
            hidDevice.GetProductName() ?? string.Empty,
            hidDevice.GetSerialNumber() ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private void CleanupCompletedWorkers()
    {
        lock (lockObj)
        {
            var completed = workers.Where(x => x.Value.IsCompleted).Select(x => x.Key).ToArray();
            foreach (var path in completed)
                workers.Remove(path);
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
