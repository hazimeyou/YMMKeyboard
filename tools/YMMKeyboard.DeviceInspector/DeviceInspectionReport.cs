using System.Text.Json.Serialization;

namespace YMMKeyboard.DeviceInspector;

internal sealed class DeviceInspectionReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;
    public List<HidDeviceSnapshot> HidDevices { get; init; } = [];
    public List<string> ComPorts { get; init; } = [];
    public List<SerialProbeResult> SerialProbeResults { get; init; } = [];
    public List<MatchedYmmKeyboardCandidate> MatchedYmmKeyboardCandidates { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}

internal sealed class HidDeviceSnapshot
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
    public string IdentityKind { get; init; } = "other";

    public string ToLine()
    {
        return $"HID kind={IdentityKind} VID:PID={VendorId:X4}:{ProductId:X4} Usage={UsagePage:X4}:{Usage:X4} In={MaxInputReportLength} Out={MaxOutputReportLength} Product=\"{ProductName}\" Maker=\"{Manufacturer}\" Serial=\"{SerialNumber}\" Path=\"{DevicePath}\"";
    }
}

internal sealed class MatchedYmmKeyboardCandidate
{
    public string IdentityKind { get; init; } = string.Empty;
    public string TransportType { get; init; } = "HID";
    public int Vid { get; init; }
    public int Pid { get; init; }
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public string ComPort { get; init; } = string.Empty;
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public string DevicePath { get; init; } = string.Empty;
    public string MatchReason { get; init; } = string.Empty;
    public List<string> MatchReasons { get; init; } = [];
}

internal sealed class SerialProbeResult
{
    public string PortName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public List<string> Events { get; set; } = [];
    public List<string> Lines { get; set; } = [];
    public string? Error { get; set; }
}
