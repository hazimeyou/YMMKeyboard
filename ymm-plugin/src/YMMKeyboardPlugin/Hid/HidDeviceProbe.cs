using HidSharp;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Hid;

public static class HidDeviceProbe
{
    public static IReadOnlyList<HidDeviceInfo> EnumerateAll()
        => EnumerateAllWithDiagnostics().EnumeratedDevices;

    public static HidEnumerationDiagnosticResult EnumerateAllWithDiagnostics()
    {
        List<HidDevice> rawDevices;
        try
        {
            rawDevices = DeviceList.Local.GetHidDevices().ToList();
        }
        catch (Exception ex)
        {
            PluginLogger.Error("HidDeviceProbe", "HID enumeration failed.", ex);
            return new HidEnumerationDiagnosticResult
            {
                Failures =
                [
                    new HidEnumerationFailureDiagnostic
                    {
                        Index = -1,
                        Vid = 0,
                        Pid = 0,
                        Path = string.Empty,
                        ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                        Message = ex.Message,
                        Stage = "enumerate-hid-devices",
                    }
                ],
            };
        }

        var devices = new List<HidEnumerationDeviceDiagnostic>(rawDevices.Count);
        var failures = new List<HidEnumerationFailureDiagnostic>();
        var safeDevices = new List<HidDeviceInfo>();

        for (var index = 0; index < rawDevices.Count; index++)
        {
            var device = rawDevices[index];
            var diagnostic = ProbeDevice(device, index, failures, out var safeInfo);
            devices.Add(diagnostic);

            if (safeInfo is not null)
                safeDevices.Add(safeInfo);
        }

        var enumeratedDevices = safeDevices
            .ToList();
        var diagnosticDevices = devices
            .OrderBy(device => device.Vid)
            .ThenBy(device => device.Pid)
            .ThenBy(device => device.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var failedCount = devices.Count(device => device.Status == "failed");
        var partialCount = devices.Count(device => device.Status == "partial");
        var skippedCount = devices.Count(device => device.Status == "skipped");

        return new HidEnumerationDiagnosticResult
        {
            TotalDeviceCount = rawDevices.Count,
            SuccessCount = devices.Count(device => device.Status is "ok" or "partial"),
            FailedCount = failedCount,
            PartialCount = partialCount,
            SkippedCount = skippedCount,
            Devices = diagnosticDevices,
            Failures = failures,
            EnumeratedDevices = enumeratedDevices,
        };
    }

    public static string BuildReportText(IReadOnlyList<HidDeviceInfo> devices)
    {
        var lines = new List<string>
        {
            $"HID Device Report ({DateTime.Now:yyyy-MM-dd HH:mm:ss})",
            $"Count={devices.Count}",
            string.Empty
        };

        foreach (var device in devices)
        {
            lines.Add(device.ToSummary());
            lines.Add($"Path={device.DevicePath}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static HidEnumerationDeviceDiagnostic ProbeDevice(
        HidDevice device,
        int index,
        List<HidEnumerationFailureDiagnostic> failures,
        out HidDeviceInfo? safeInfo)
    {
        var vid = 0;
        var pid = 0;
        var path = string.Empty;
        var status = "ok";

        try
        {
            vid = device.VendorID;
            pid = device.ProductID;
            path = SafeString(() => device.DevicePath);

            if (string.IsNullOrWhiteSpace(path))
            {
                failures.Add(new HidEnumerationFailureDiagnostic
                {
                    Index = index,
                    Vid = vid,
                    Pid = pid,
                    Path = string.Empty,
                    ExceptionType = "MissingDevicePath",
                    Message = "DevicePath is empty.",
                    Stage = "get-device-path",
                });

                safeInfo = null;
                return new HidEnumerationDeviceDiagnostic
                {
                    Index = index,
                    Vid = vid,
                    Pid = pid,
                    ProductName = string.Empty,
                    Manufacturer = string.Empty,
                    Serial = string.Empty,
                    UsagePage = 0,
                    Usage = 0,
                    Path = string.Empty,
                    MaxInputReportLength = 0,
                    MaxOutputReportLength = 0,
                    Status = "skipped",
                };
            }

            var productName = ReadString(device, index, vid, pid, path, failures, "get-product-name", () => device.GetProductName(), ref status);
            var manufacturer = ReadString(device, index, vid, pid, path, failures, "get-manufacturer", () => device.GetManufacturer(), ref status);
            var serial = ReadString(device, index, vid, pid, path, failures, "get-serial", () => TryGetStringValue(device, "SerialNumber"), ref status);
            var usagePage = ReadInt(device, index, vid, pid, path, failures, "get-usage-page", () => TryGetUsageValue(device, "UsagePage"), ref status);
            var usage = ReadInt(device, index, vid, pid, path, failures, "get-usage", () => TryGetUsageValue(device, "Usage"), ref status);
            var maxInputReportLength = ReadInt(device, index, vid, pid, path, failures, "get-input-report-length", () => device.GetMaxInputReportLength(), ref status);
            var maxOutputReportLength = ReadInt(device, index, vid, pid, path, failures, "get-output-report-length", () => device.GetMaxOutputReportLength(), ref status);

            safeInfo = new HidDeviceInfo
            {
                VendorId = vid,
                ProductId = pid,
                ProductName = productName,
                Manufacturer = manufacturer,
                SerialNumber = serial,
                DevicePath = path,
                MaxInputReportLength = maxInputReportLength,
                MaxOutputReportLength = maxOutputReportLength,
                UsagePage = usagePage,
                Usage = usage,
            };

            if (status == "ok")
            {
                return new HidEnumerationDeviceDiagnostic
                {
                    Index = index,
                    Vid = vid,
                    Pid = pid,
                    ProductName = productName,
                    Manufacturer = manufacturer,
                    Serial = serial,
                    UsagePage = usagePage,
                    Usage = usage,
                    Path = path,
                    MaxInputReportLength = maxInputReportLength,
                    MaxOutputReportLength = maxOutputReportLength,
                    Status = "ok",
                };
            }

            return new HidEnumerationDeviceDiagnostic
            {
                Index = index,
                Vid = vid,
                Pid = pid,
                ProductName = productName,
                Manufacturer = manufacturer,
                Serial = serial,
                UsagePage = usagePage,
                Usage = usage,
                Path = path,
                MaxInputReportLength = maxInputReportLength,
                MaxOutputReportLength = maxOutputReportLength,
                Status = "partial",
            };
        }
        catch (Exception ex)
        {
            status = "failed";
            failures.Add(new HidEnumerationFailureDiagnostic
            {
                Index = index,
                Vid = vid,
                Pid = pid,
                Path = path,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                Stage = "build-device-info",
            });

            PluginLogger.Warn("HidDeviceProbe", $"HID device probe failed. index={index}, vid={vid:X4}, pid={pid:X4}, path={path}, error={ex.GetType().Name}: {ex.Message}");
            safeInfo = null;

            return new HidEnumerationDeviceDiagnostic
            {
                Index = index,
                Vid = vid,
                Pid = pid,
                ProductName = string.Empty,
                Manufacturer = string.Empty,
                Serial = string.Empty,
                UsagePage = 0,
                Usage = 0,
                Path = path,
                MaxInputReportLength = 0,
                MaxOutputReportLength = 0,
                Status = status,
            };
        }
    }

    private static string ReadString(
        HidDevice device,
        int index,
        int vid,
        int pid,
        string path,
        List<HidEnumerationFailureDiagnostic> failures,
        string stage,
        Func<string?> getter,
        ref string status)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch (Exception ex)
        {
            status = "partial";
            failures.Add(CreateFailure(index, vid, pid, path, stage, ex));
            PluginLogger.Warn("HidDeviceProbe", $"HID string read failed. index={index}, vid={vid:X4}, pid={pid:X4}, stage={stage}, error={ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    private static int ReadInt(
        HidDevice device,
        int index,
        int vid,
        int pid,
        string path,
        List<HidEnumerationFailureDiagnostic> failures,
        string stage,
        Func<int> getter,
        ref string status)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            status = "partial";
            failures.Add(CreateFailure(index, vid, pid, path, stage, ex));
            PluginLogger.Warn("HidDeviceProbe", $"HID int read failed. index={index}, vid={vid:X4}, pid={pid:X4}, stage={stage}, error={ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    private static HidEnumerationFailureDiagnostic CreateFailure(
        int index,
        int vid,
        int pid,
        string path,
        string stage,
        Exception ex)
    {
        return new HidEnumerationFailureDiagnostic
        {
            Index = index,
            Vid = vid,
            Pid = pid,
            Path = path,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            Stage = stage,
        };
    }

    private static int TryGetUsageValue(HidDevice d, string propertyName)
    {
        try
        {
            var prop = d.GetType().GetProperty(propertyName);
            if (prop is not null)
                return Convert.ToInt32(prop.GetValue(d) ?? 0);
        }
        catch
        {
        }

        return 0;
    }

    private static string TryGetStringValue(HidDevice d, string propertyName)
    {
        try
        {
            var prop = d.GetType().GetProperty(propertyName);
            if (prop is not null)
                return prop.GetValue(d)?.ToString() ?? string.Empty;
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string SafeString(Func<string?> getter)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed class HidEnumerationDiagnosticResult
{
    public int TotalDeviceCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int PartialCount { get; init; }
    public int SkippedCount { get; init; }
    public List<HidEnumerationDeviceDiagnostic> Devices { get; init; } = [];
    public List<HidEnumerationFailureDiagnostic> Failures { get; init; } = [];

    public IReadOnlyList<HidDeviceInfo> EnumeratedDevices { get; init; } = [];
}

public sealed class HidEnumerationDeviceDiagnostic
{
    public int Index { get; init; }
    public int Vid { get; init; }
    public int Pid { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
}

public sealed class HidEnumerationFailureDiagnostic
{
    public int Index { get; init; }
    public int Vid { get; init; }
    public int Pid { get; init; }
    public string Path { get; init; } = string.Empty;
    public string ExceptionType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
}
