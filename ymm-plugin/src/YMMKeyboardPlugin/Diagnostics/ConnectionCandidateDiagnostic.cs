namespace YMMKeyboardPlugin.Diagnostics;

public sealed class ConnectionCandidateDiagnostic
{
    public string TransportType { get; init; } = string.Empty;
    public int? Vid { get; init; }
    public int? Pid { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public string ComPort { get; init; } = string.Empty;
    public int? UsagePage { get; init; }
    public int? Usage { get; init; }
    public int MatchScore { get; init; }
    public List<string> MatchReasons { get; init; } = [];
    public List<string> RejectReasons { get; init; } = [];
    public bool Selected { get; init; }
}
