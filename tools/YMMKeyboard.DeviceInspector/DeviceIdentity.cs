namespace YMMKeyboard.DeviceInspector;

internal static class DeviceIdentity
{
    public const ushort FormalVendorId = 0x2E8A;
    public const ushort FormalProductId = 0x4020;
    public const string FormalManufacturer = "YMMKeyboard";
    public const string FormalProduct = "YMMKeyboard RP2040";
    public const string FormalCdcInterface = "YMM Serial Bridge";
    public const string FormalHidInterface = "YMM Control HID";
    public const ushort HidUsagePage = 0xFF00;
    public const ushort HidUsage = 0x0001;
    public const byte HidReportId = 1;
    public const int HidPayloadBytes = 63;

    public static string DescribeFormal() =>
        $"VID:PID={FormalVendorId:X4}:{FormalProductId:X4}, Maker=\"{FormalManufacturer}\", Product=\"{FormalProduct}\", CDC=\"{FormalCdcInterface}\", HID=\"{FormalHidInterface}\"";

    public static string ClassifyHid(int vendorId, int productId, string productName, string manufacturer, int usagePage, int usage)
    {
        if (vendorId == FormalVendorId
            && productId == FormalProductId
            && string.Equals(productName, FormalProduct, StringComparison.OrdinalIgnoreCase)
            && string.Equals(manufacturer, FormalManufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return "formal";
        }

        return "other";
    }
}
