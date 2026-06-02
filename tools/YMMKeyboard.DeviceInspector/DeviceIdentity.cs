namespace YMMKeyboard.DeviceInspector;

internal static class DeviceIdentity
{
    public const ushort FormalVendorId = 0x2E8A;
    public const ushort FormalProductId = 0x4020;
    public const string FormalManufacturer = "YMMKeyboard Project";
    public const string FormalProduct = "YMM RP2040 Control Keyboard";
    public const string FormalCdcInterface = "YMM Serial Bridge";
    public const string FormalHidInterface = "YMM Control HID";
    public const ushort HidUsagePage = 0xFF00;
    public const ushort HidUsage = 0x0001;
    public const byte HidReportId = 1;
    public const int HidPayloadBytes = 63;

    public const ushort TemporaryVendorId = 0x2E8A;
    public const ushort TemporaryProductId = 0x101F;
    public const string TemporaryManufacturer = "YMMKeyboard";
    public const string TemporaryProduct = "YMM HID";

    public static string DescribeFormal() =>
        $"VID:PID={FormalVendorId:X4}:{FormalProductId:X4}, Maker=\"{FormalManufacturer}\", Product=\"{FormalProduct}\", CDC=\"{FormalCdcInterface}\", HID=\"{FormalHidInterface}\"";

    public static string DescribeTemporary() =>
        $"VID:PID={TemporaryVendorId:X4}:{TemporaryProductId:X4}, Maker=\"{TemporaryManufacturer}\", Product=\"{TemporaryProduct}\"";

    public static string ClassifyHid(int vendorId, int productId, string productName, string manufacturer, int usagePage, int usage)
    {
        if (vendorId == FormalVendorId
            && productId == FormalProductId
            && string.Equals(productName, FormalProduct, StringComparison.OrdinalIgnoreCase)
            && string.Equals(manufacturer, FormalManufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return "formal";
        }

        if (vendorId == TemporaryVendorId
            && productId == TemporaryProductId
            && string.Equals(productName, TemporaryProduct, StringComparison.OrdinalIgnoreCase)
            && string.Equals(manufacturer, TemporaryManufacturer, StringComparison.OrdinalIgnoreCase))
        {
            return "temporary";
        }

        if (vendorId == FormalVendorId || productId == FormalProductId || usagePage == HidUsagePage || usage == HidUsage)
            return "likely-ymm";

        if (productName.Contains("YMM", StringComparison.OrdinalIgnoreCase)
            || manufacturer.Contains("YMM", StringComparison.OrdinalIgnoreCase))
        {
            return "possible-ymm";
        }

        return "other";
    }
}
