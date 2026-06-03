using YMMKeyboardPlugin.Hid;

namespace YMMKeyboardPlugin.Diagnostics;

public sealed class PluginConnectionDiagnosticReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string PluginVersion { get; init; } = string.Empty;
    public string YmmVersion { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string ScanMode { get; init; } = string.Empty;
    public ConfiguredDeviceIdentity ConfiguredDeviceIdentity { get; init; } = new();
    public HidEnumerationDiagnosticResult RawHidEnumeration { get; init; } = new();
    public List<DetectedHidDeviceDiagnostic> DetectedHidDevices { get; init; } = [];
    public List<string> DetectedComPorts { get; init; } = [];
    public List<ConnectionCandidateDiagnostic> ConnectionCandidates { get; init; } = [];
    public ConnectionCandidateDiagnostic? SelectedCandidate { get; init; }
    public List<ConnectionCandidateDiagnostic> RejectedCandidates { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
}

public sealed class ConfiguredDeviceIdentity
{
    public string ConnectionMode { get; init; } = string.Empty;
    public string? HidVendorId { get; init; }
    public string? HidProductId { get; init; }
    public string HidProductNameFilter { get; init; } = string.Empty;
    public string HidManufacturerFilter { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public List<string> StartupPortNames { get; init; } = [];
}

public sealed class DetectedHidDeviceDiagnostic
{
    public int Vid { get; init; }
    public int Pid { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
    public string IdentityKind { get; init; } = string.Empty;
}
