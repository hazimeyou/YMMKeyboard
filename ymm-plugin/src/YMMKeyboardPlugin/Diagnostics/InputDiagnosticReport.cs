namespace YMMKeyboardPlugin.Diagnostics;

public sealed class InputDiagnosticReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string PluginVersion { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public InputDiagnosticSummary Summary { get; init; } = new();
    public List<InputDiagnosticEvent> Events { get; init; } = [];
}
