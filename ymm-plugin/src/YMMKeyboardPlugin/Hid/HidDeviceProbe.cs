using HidSharp;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Hid;

public static class HidDeviceProbe
{
    public static IReadOnlyList<HidDeviceInfo> EnumerateAll()
    {
        try
        {
            return DeviceList.Local.GetHidDevices()
                .Select(d => new HidDeviceInfo
                {
                    VendorId = d.VendorID,
                    ProductId = d.ProductID,
                    ProductName = d.ProductName ?? string.Empty,
                    Manufacturer = d.Manufacturer ?? string.Empty,
                    SerialNumber = TryGetStringValue(d, "SerialNumber"),
                    DevicePath = d.DevicePath ?? string.Empty,
                    MaxInputReportLength = d.GetMaxInputReportLength(),
                    MaxOutputReportLength = d.GetMaxOutputReportLength(),
                    UsagePage = TryGetUsageValue(d, "UsagePage"),
                    Usage = TryGetUsageValue(d, "Usage"),
                })
                .OrderBy(d => d.VendorId)
                .ThenBy(d => d.ProductId)
                .ToArray();
        }
        catch (Exception ex)
        {
            PluginLogger.Error("HidDeviceProbe", "HID enumeration failed.", ex);
            return Array.Empty<HidDeviceInfo>();
        }
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
}
