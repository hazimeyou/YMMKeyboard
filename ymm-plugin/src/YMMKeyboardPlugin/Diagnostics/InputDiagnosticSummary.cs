namespace YMMKeyboardPlugin.Diagnostics;

public sealed class InputDiagnosticSummary
{
    public int EventCount { get; init; }
    public int MacroCount { get; init; }
    public int MappedActionCount { get; init; }
    public int RejectedCount { get; init; }
    public int IssuesCount { get; init; }
}
