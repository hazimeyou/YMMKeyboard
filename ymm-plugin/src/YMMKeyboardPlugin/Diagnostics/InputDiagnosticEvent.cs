namespace YMMKeyboardPlugin.Diagnostics;

public sealed class InputDiagnosticEvent
{
    public string EventType { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string TransportType { get; init; } = string.Empty;
    public string SourceDevice { get; init; } = string.Empty;
    public string RawInput { get; init; } = string.Empty;
    public string InputId { get; init; } = string.Empty;
    public string FilterName { get; init; } = string.Empty;
    public bool? Accepted { get; init; }
    public string RejectReason { get; init; } = string.Empty;
    public string MappedAction { get; init; } = string.Empty;
    public string MappingSource { get; init; } = string.Empty;
    public string MacroName { get; init; } = string.Empty;
    public int? StepCount { get; init; }
    public string ResolutionResult { get; init; } = string.Empty;
    public string DispatchType { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string PayloadSummary { get; init; } = string.Empty;
    public bool? Succeeded { get; init; }
    public string Result { get; init; } = string.Empty;
    public string ExceptionType { get; init; } = string.Empty;
    public string ExceptionMessage { get; init; } = string.Empty;
}
