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
    private DateTime lastRawReportLogAtUtc = DateTime.MinValue;
    private int rawReportSampleCount;
    private string selectedPath = string.Empty;
    private string attemptedPath = string.Empty;
    private string openedPath = string.Empty;
    private int openedVid;
    private int openedPid;
    private string openedProductName = string.Empty;
    private string openedManufacturer = string.Empty;
    private string openedSerial = string.Empty;
    private bool openSucceeded;
    private bool readLoopStarted;
    private int readAttemptCount;
    private int readSuccessCount;
    private int readTimeoutCount;
    private DateTime? lastReadAtUtc;
    private string lastExceptionType = string.Empty;
    private string lastExceptionMessage = string.Empty;

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
                var candidateDevices = GetCandidateDevices().ToArray();
                lock (lockObj)
                {
                    selectedPath = candidateDevices.FirstOrDefault()?.DevicePath ?? string.Empty;
                }
                TryWriteSummary();

                foreach (var hidDevice in candidateDevices)
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
                        TryWriteSummary(force: true);
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

                if (useImplicitFilter && !IsFormalYmmKeyboardDevice(d, productName, manufacturer))
                    continue;

                result.Add(d);
            }
            catch (Exception ex)
            {
                ThrottledManagerWarn($"Skipping HID device during filtering. error={ex.Message}");
            }
        }

        // For CircuitPython/Waveshare groups, keep multiple interfaces and try them in score order.
        // For other groups, keep only the top-scored interface.
        var reduced = result
            .GroupBy(d => BuildInterfaceGroupKey(d))
            .SelectMany(g =>
            {
                var ordered = g.OrderByDescending(ScoreDeviceForInput).ToArray();
                var first = ordered.FirstOrDefault();
                if (first is null)
                    return Array.Empty<HidDevice>();

                var product = SafeGetProductName(first);
                var maker = SafeGetManufacturer(first);
                if (product.Contains("CircuitPython HID", StringComparison.OrdinalIgnoreCase)
                    || maker.Contains("Waveshare", StringComparison.OrdinalIgnoreCase))
                {
                    return ordered;
                }

                return new[] { first };
            })
            .ToArray();

        if (reduced.Length > 0)
        {
            var picked = string.Join(", ", reduced.Select(d =>
                $"{d.VendorID:X4}:{d.ProductID:X4}/in{SafeGetInputLen(d)}/out{SafeGetOutputLen(d)}"));
            ThrottledManagerWarn($"HID candidate interfaces selected: {picked}");
        }

        return reduced;
    }

    private static string BuildInterfaceGroupKey(HidDevice d)
    {
        var maker = SafeGetManufacturer(d);
        var product = SafeGetProductName(d);
        return $"{d.VendorID:X4}:{d.ProductID:X4}|{maker}|{product}";
    }

    private static int ScoreDeviceForInput(HidDevice d)
    {
        var inLen = SafeGetInputLen(d);
        var outLen = SafeGetOutputLen(d);
        var score = 0;

        // Most custom/bidirectional HID endpoints expose output reports.
        if (outLen > 0) score += 1000;

        // Prefer wider report sizes (e.g., 64-byte vendor reports).
        score += inLen * 10;
        score += outLen * 5;

        // Boost explicit custom HID usage if available.
        if (TryGetUsagePageAndUsage(d, out var page, out var usage) && page == 0xFF00 && usage == 0x0001)
            score += 5000;

        // Penalize tiny report endpoints that are often non-target collections.
        if (inLen <= 3 && outLen == 0) score -= 200;

        return score;
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

    private static bool IsFormalYmmKeyboardDevice(HidDevice d, string product, string maker)
    {
        if (d.VendorID != 0x2E8A
            || d.ProductID != 0x4020
            || !string.Equals(maker, "YMMKeyboard", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasKnownProductName =
            string.Equals(product, "YMMKeyboard RP2040", StringComparison.OrdinalIgnoreCase)
            || string.Equals(product, "YMM Control HID", StringComparison.OrdinalIgnoreCase);

        var hasFormalUsage = TryGetUsagePageAndUsage(d, out var page, out var usage)
            && page == 0xFF00
            && usage == 0x0001;

        return hasKnownProductName || hasFormalUsage;
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

    private static int SafeGetInputLen(HidDevice d)
    {
        try { return d.GetMaxInputReportLength(); }
        catch { return 0; }
    }

    private static int SafeGetOutputLen(HidDevice d)
    {
        try { return d.GetMaxOutputReportLength(); }
        catch { return 0; }
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
            lock (lockObj)
            {
                attemptedPath = path;
            }
            TryWriteSummary(force: true);

            if (!hidDevice.TryOpen(out var stream))
            {
                lock (lockObj)
                {
                    openedPath = path;
                    openedVid = hidDevice.VendorID;
                    openedPid = hidDevice.ProductID;
                    openedProductName = SafeGetProductName(hidDevice);
                    openedManufacturer = SafeGetManufacturer(hidDevice);
                    openedSerial = SafeGetSerialNumber(hidDevice);
                    openSucceeded = false;
                    lastExceptionType = string.Empty;
                    lastExceptionMessage = "TryOpen failed";
                }
                TryWriteSummary(force: true);
                ThrottledManagerWarn($"TryOpen failed. path={path}");
                return;
            }

            using (stream)
            {
                lock (lockObj)
                {
                    openedPath = path;
                    openedVid = hidDevice.VendorID;
                    openedPid = hidDevice.ProductID;
                    openedProductName = SafeGetProductName(hidDevice);
                    openedManufacturer = SafeGetManufacturer(hidDevice);
                    openedSerial = SafeGetSerialNumber(hidDevice);
                    openSucceeded = true;
                    readLoopStarted = true;
                    lastExceptionType = string.Empty;
                    lastExceptionMessage = string.Empty;
                }
                TryWriteSummary(force: true);

                var stableUid = BuildStableSyntheticUid(hidDevice);
                var device = GetOrCreateDevice(stableUid, out var isNew);
                if (isNew)
                {
                    PluginLogger.Info("HidKeyboardLink",
                        $"HID device detected: {stableUid} ({hidDevice.VendorID:X4}:{hidDevice.ProductID:X4}, product={hidDevice.GetProductName()}, maker={hidDevice.GetManufacturer()}, in={hidDevice.GetMaxInputReportLength()}, out={hidDevice.GetMaxOutputReportLength()}, path={path})");
                    DeviceDetected?.Invoke(device);
                }

                var reportBuffer = new byte[Math.Max(64, hidDevice.GetMaxInputReportLength())];
                while (!token.IsCancellationRequested)
                {
                    int length;
                    try
                    {
                        lock (lockObj)
                        {
                            readAttemptCount++;
                        }
                        TryWriteSummary();
                        length = await stream.ReadAsync(reportBuffer, 0, reportBuffer.Length, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (TimeoutException)
                    {
                        lock (lockObj)
                        {
                            readTimeoutCount++;
                            lastExceptionType = nameof(TimeoutException);
                            lastExceptionMessage = string.Empty;
                        }
                        TryWriteSummary(force: true);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            lastExceptionType = ex.GetType().Name;
                            lastExceptionMessage = ex.Message;
                        }
                        TryWriteSummary(force: true);
                        break;
                    }

                if (length <= 0)
                    break;

                lock (lockObj)
                {
                    readSuccessCount++;
                    lastReadAtUtc = DateTime.UtcNow;
                }
                TryWriteSummary();
                MaybeLogRawReport(path, reportBuffer, length);
                ParseReport(stableUid, device, reportBuffer, length);
            }
        }
        }
        catch (Exception ex)
        {
            lock (lockObj)
            {
                lastExceptionType = ex.GetType().Name;
                lastExceptionMessage = ex.Message;
            }
            TryWriteSummary(force: true);
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
        var candidates = ExtractAsciiCandidates(report, length);
        if (candidates.Count == 0)
            return;

        Match? matched = null;
        string matchedText = string.Empty;
        foreach (var candidate in candidates)
        {
            var m = serialEventPattern.Match(candidate);
            if (!m.Success)
                continue;

            matched = m;
            matchedText = candidate;
            break;
        }

        if (matched is null)
        {
            MaybeLogUnparsedAscii(string.Join(" | ", candidates.Take(3)));
            return;
        }

        var uid = matched.Groups["uid"].Value.ToLowerInvariant();
        var state = matched.Groups["state"].Value;
        if (!int.TryParse(matched.Groups["switch"].Value, out var switchId))
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
            TransportType = "HID",
            SourceDevice = actualDevice.Uid,
            RawInput = matchedText,
            InputId = $"{uid}:{state}:SW_{switchId:00}",
            IsPressed = state == "P",
            SwitchId = switchId,
            ReceivedAtUtc = DateTime.UtcNow,
        });

        PluginLogger.Info("HidKeyboardLink",
            $"Parsed key event via HID. uid={uid}, switch={switchId}, pressed={(state == "P")}, source=\"{matchedText}\"");
    }

    private static List<string> ExtractAsciiCandidates(byte[] report, int length)
    {
        var candidates = new List<string>();
        if (length <= 0)
            return candidates;

        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var b = report[i];
            var isPrintable = b >= 0x20 && b <= 0x7E;
            if (isPrintable)
            {
                sb.Append((char)b);
                continue;
            }

            if (sb.Length > 0)
            {
                var token = sb.ToString().Trim();
                if (token.Length > 0)
                    candidates.Add(token);
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            var token = sb.ToString().Trim();
            if (token.Length > 0)
                candidates.Add(token);
        }

        return candidates;
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

    private static string SafeGetSerialNumber(HidDevice d)
    {
        try { return d.GetSerialNumber() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private void TryWriteSummary(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - lastSummaryWrittenAtUtc).TotalSeconds < 30)
            return;

        lastSummaryWrittenAtUtc = now;

        try
        {
            int activeWorkers;
            int cooldownCount;
            int excludedCount;
            int knownDeviceCount;
            int errorCount;
            string selectedPathSnapshot;
            string attemptedPathSnapshot;
            string openedPathSnapshot;
            int openedVidSnapshot;
            int openedPidSnapshot;
            string openedProductNameSnapshot;
            string openedManufacturerSnapshot;
            string openedSerialSnapshot;
            bool openSucceededSnapshot;
            bool readLoopStartedSnapshot;
            int readAttemptCountSnapshot;
            int readSuccessCountSnapshot;
            int readTimeoutCountSnapshot;
            DateTime? lastReadAtSnapshot;
            string lastExceptionTypeSnapshot;
            string lastExceptionMessageSnapshot;
            lock (lockObj)
            {
                activeWorkers = workers.Count;
                cooldownCount = failedPathCooldownUntilUtc.Count;
                excludedCount = excludedPaths.Count;
                knownDeviceCount = devices.Count;
                errorCount = totalWorkerErrorCount;
                selectedPathSnapshot = selectedPath;
                attemptedPathSnapshot = attemptedPath;
                openedPathSnapshot = openedPath;
                openedVidSnapshot = openedVid;
                openedPidSnapshot = openedPid;
                openedProductNameSnapshot = openedProductName;
                openedManufacturerSnapshot = openedManufacturer;
                openedSerialSnapshot = openedSerial;
                openSucceededSnapshot = openSucceeded;
                readLoopStartedSnapshot = readLoopStarted;
                readAttemptCountSnapshot = readAttemptCount;
                readSuccessCountSnapshot = readSuccessCount;
                readTimeoutCountSnapshot = readTimeoutCount;
                lastReadAtSnapshot = lastReadAtUtc;
                lastExceptionTypeSnapshot = lastExceptionType;
                lastExceptionMessageSnapshot = lastExceptionMessage;
            }

            var summary = string.Join(Environment.NewLine, new[]
            {
                $"time_utc={DateTime.UtcNow:O}",
                $"selectedPath={selectedPathSnapshot}",
                $"attemptedPath={attemptedPathSnapshot}",
                $"openedPath={openedPathSnapshot}",
                $"openedVid={openedVidSnapshot:X4}",
                $"openedPid={openedPidSnapshot:X4}",
                $"openedProductName={openedProductNameSnapshot}",
                $"openedManufacturer={openedManufacturerSnapshot}",
                $"openedSerial={openedSerialSnapshot}",
                $"openSucceeded={openSucceededSnapshot}",
                $"readLoopStarted={readLoopStartedSnapshot}",
                $"readAttemptCount={readAttemptCountSnapshot}",
                $"readSuccessCount={readSuccessCountSnapshot}",
                $"readTimeoutCount={readTimeoutCountSnapshot}",
                $"lastReadAt={(lastReadAtSnapshot.HasValue ? lastReadAtSnapshot.Value.ToString("O") : string.Empty)}",
                $"lastExceptionType={lastExceptionTypeSnapshot}",
                $"lastExceptionMessage={lastExceptionMessageSnapshot}",
                $"manager_loop_count={managerLoopCount}",
                $"active_workers={activeWorkers}",
                $"cooldown_paths={cooldownCount}",
                $"excluded_paths={excludedCount}",
                $"known_devices={knownDeviceCount}",
                $"worker_error_count={errorCount}",
                $"raw_report_samples={rawReportSampleCount}",
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

    private void MaybeLogRawReport(string path, byte[] report, int length)
    {
        var now = DateTime.UtcNow;
        if ((now - lastRawReportLogAtUtc).TotalSeconds < 10)
            return;

        lastRawReportLogAtUtc = now;
        rawReportSampleCount++;

        var safeLen = Math.Min(length, 32);
        var hex = BitConverter.ToString(report, 0, safeLen);
        var ascii = Encoding.ASCII.GetString(report, 0, safeLen)
            .Replace('\0', '.')
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        PluginLogger.Info("HidKeyboardLink",
            $"Raw HID sample path={path}, len={length}, headHex={hex}, headAscii={ascii}");
    }

    private static void MaybeLogUnparsedAscii(string ascii)
    {
        if (ascii.Length > 96)
            ascii = ascii[..96] + "...";
        PluginLogger.Info("HidKeyboardLink", $"Unparsed HID ascii: {ascii}");
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
