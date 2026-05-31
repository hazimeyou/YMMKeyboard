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
        @"(?:YMMK:)?(?<uid>[0-9a-fA-F]+):(?<state>[PR]):SW_(?<switch>\d+)",
        RegexOptions.Compiled);

    private readonly int? vendorId;
    private readonly int? productId;
    private readonly string productNameFilter;
    private readonly string manufacturerFilter;
    private readonly Dictionary<string, SerialKeyboardDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> workers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> failedPathCooldownUntilUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> failedPathLastLoggedAtUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> failedPathCount = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> excludedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object lockObj = new();
    private CancellationTokenSource? cts;
    private DateTime lastManagerErrorAtUtc = DateTime.MinValue;
    private DateTime lastSummaryWrittenAtUtc = DateTime.MinValue;
    private int managerLoopCount;
    private int totalWorkerErrorCount;

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

                managerLoopCount++;
                TryWriteSummary();
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
        var result = new List<HidDevice>();
        var devices = DeviceList.Local.GetHidDevices().ToArray();
        var useImplicitFilter = !vendorId.HasValue
            && !productId.HasValue
            && string.IsNullOrWhiteSpace(productNameFilter)
            && string.IsNullOrWhiteSpace(manufacturerFilter);

        foreach (var d in devices)
        {
            try
            {
                var path = d.DevicePath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (IsPathExcluded(path))
                    continue;
                if (IsPathInCooldown(path))
                    continue;

                if (vendorId.HasValue && d.VendorID != vendorId.Value)
                    continue;
                if (productId.HasValue && d.ProductID != productId.Value)
                    continue;

                var productName = SafeGetProductName(d);
                var manufacturer = SafeGetManufacturer(d);

                if (!string.IsNullOrWhiteSpace(productNameFilter)
                    && !productName.Contains(productNameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(manufacturerFilter)
                    && !manufacturer.Contains(manufacturerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (useImplicitFilter && !IsLikelyYmmKeyboardDevice(d, productName, manufacturer))
                    continue;

                result.Add(d);
            }
            catch (Exception ex)
            {
                ThrottledManagerWarn($"Skipping HID device during filtering. error={ex.Message}");
            }
        }

        return result;
    }

    private bool IsPathInCooldown(string path)
    {
        lock (lockObj)
        {
            if (!failedPathCooldownUntilUtc.TryGetValue(path, out var until))
                return false;

            if (DateTime.UtcNow >= until)
            {
                failedPathCooldownUntilUtc.Remove(path);
                return false;
            }

            return true;
        }
    }

    private bool IsPathExcluded(string path)
    {
        lock (lockObj)
        {
            return excludedPaths.Contains(path);
        }
    }

    private static bool IsLikelyYmmKeyboardDevice(HidDevice d, string product, string maker)
    {
        if (TryGetUsagePageAndUsage(d, out var page, out var usage))
        {
            if (page == 0xFF00 && usage == 0x0001)
                return true;
        }

        if (product.Contains("CircuitPython HID", StringComparison.OrdinalIgnoreCase)
            || maker.Contains("Waveshare", StringComparison.OrdinalIgnoreCase))
            return true;

        return d.GetMaxInputReportLength() == 64 && d.GetMaxOutputReportLength() == 64;
    }

    private static bool TryGetUsagePageAndUsage(HidDevice d, out int usagePage, out int usage)
    {
        usagePage = 0;
        usage = 0;

        try
        {
            var t = d.GetType();

            var upProp = t.GetProperty("UsagePage");
            var uProp = t.GetProperty("Usage");
            if (upProp is not null && uProp is not null)
            {
                usagePage = Convert.ToInt32(upProp.GetValue(d) ?? 0);
                usage = Convert.ToInt32(uProp.GetValue(d) ?? 0);
                return true;
            }

            var upMethod = t.GetMethod("GetUsagePage", Type.EmptyTypes);
            var uMethod = t.GetMethod("GetUsage", Type.EmptyTypes);
            if (upMethod is not null && uMethod is not null)
            {
                usagePage = Convert.ToInt32(upMethod.Invoke(d, null) ?? 0);
                usage = Convert.ToInt32(uMethod.Invoke(d, null) ?? 0);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static string SafeGetProductName(HidDevice d)
    {
        try { return d.GetProductName() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafeGetManufacturer(HidDevice d)
    {
        try { return d.GetManufacturer() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private void ThrottledManagerWarn(string message)
    {
        var now = DateTime.UtcNow;
        if ((now - lastManagerErrorAtUtc).TotalSeconds < 10)
            return;
        lastManagerErrorAtUtc = now;
        PluginLogger.Warn("HidKeyboardLink", message);
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
            RegisterPathFailure(path, ex);
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

    private void RegisterPathFailure(string path, Exception ex)
    {
        var now = DateTime.UtcNow;
        var excluded = false;
        var failCount = 0;
        lock (lockObj)
        {
            totalWorkerErrorCount++;
            failedPathCooldownUntilUtc[path] = now.AddSeconds(60);
            failedPathCount.TryGetValue(path, out failCount);
            failCount++;
            failedPathCount[path] = failCount;
            if (failCount >= 3)
            {
                excludedPaths.Add(path);
                excluded = true;
            }
        }

        if (ShouldLogPathFailure(path, now))
        {
            var mode = excluded ? "excluded=session" : "cooldown=60s";
            PluginLogger.Error("HidKeyboardLink",
                $"HID worker failed. path={path}. {mode}. failCount={failCount}", ex);
        }
    }

    private bool ShouldLogPathFailure(string path, DateTime nowUtc)
    {
        lock (lockObj)
        {
            if (!failedPathLastLoggedAtUtc.TryGetValue(path, out var last))
            {
                failedPathLastLoggedAtUtc[path] = nowUtc;
                return true;
            }

            if ((nowUtc - last).TotalSeconds < 60)
                return false;

            failedPathLastLoggedAtUtc[path] = nowUtc;
            return true;
        }
    }

    private void TryWriteSummary()
    {
        var now = DateTime.UtcNow;
        if ((now - lastSummaryWrittenAtUtc).TotalSeconds < 30)
            return;

        lastSummaryWrittenAtUtc = now;

        try
        {
            int activeWorkers;
            int cooldownCount;
            int excludedCount;
            int knownDeviceCount;
            int errorCount;
            lock (lockObj)
            {
                activeWorkers = workers.Count;
                cooldownCount = failedPathCooldownUntilUtc.Count;
                excludedCount = excludedPaths.Count;
                knownDeviceCount = devices.Count;
                errorCount = totalWorkerErrorCount;
            }

            var summary = string.Join(Environment.NewLine, new[]
            {
                $"time_utc={DateTime.UtcNow:O}",
                $"manager_loop_count={managerLoopCount}",
                $"active_workers={activeWorkers}",
                $"cooldown_paths={cooldownCount}",
                $"excluded_paths={excludedCount}",
                $"known_devices={knownDeviceCount}",
                $"worker_error_count={errorCount}",
            });

            Directory.CreateDirectory(PluginLogger.DiagnosticsDirectoryPath);
            var path = Path.Combine(PluginLogger.DiagnosticsDirectoryPath, "hid_runtime_summary.txt");
            File.WriteAllText(path, summary, Encoding.UTF8);
        }
        catch
        {
            // best effort
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
