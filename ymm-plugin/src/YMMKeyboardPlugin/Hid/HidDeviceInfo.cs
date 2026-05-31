namespace YMMKeyboardPlugin.Hid;

public sealed class HidDeviceInfo
{
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string DevicePath { get; init; } = string.Empty;
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }

    public string ToSummary()
    {
        return $"VID:PID={VendorId:X4}:{ProductId:X4}, In/OutReport={MaxInputReportLength}/{MaxOutputReportLength}, Product={ProductName}, Maker={Manufacturer}";
    }
}
