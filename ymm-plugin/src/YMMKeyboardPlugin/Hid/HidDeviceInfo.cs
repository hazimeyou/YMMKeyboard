namespace YMMKeyboardPlugin.Hid;

public sealed class HidDeviceInfo
{
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string DevicePath { get; init; } = string.Empty;
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
    public int UsagePage { get; init; }
    public int Usage { get; init; }

    public string ToSummary()
    {
        return $"VID:PID={VendorId:X4}:{ProductId:X4}, Usage={UsagePage:X4}:{Usage:X4}, In/OutReport={MaxInputReportLength}/{MaxOutputReportLength}, Product={ProductName}, Maker={Manufacturer}";
    }
}
