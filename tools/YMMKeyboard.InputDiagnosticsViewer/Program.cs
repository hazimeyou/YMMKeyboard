using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = InputDiagnosticsViewerOptions.Parse(args);
var viewer = new InputDiagnosticsViewer(options);
var report = viewer.Load();
var writer = new InputDiagnosticsViewerWriter(options);
writer.Write(report);

Console.WriteLine($"InputDiagnosticsViewer completed. output={writer.OutputPath}");
Console.WriteLine($"eventCount={report.Summary.EventCount}, macroCount={report.Summary.MacroCount}, mappedActionCount={report.Summary.MappedActionCount}, rejectedCount={report.Summary.RejectedCount}, issues={report.Summary.IssuesCount}");

internal sealed class InputDiagnosticsViewerOptions
{
    public string InputPath { get; init; } = string.Empty;
    public string? OutputPath { get; init; }
    public string Format { get; init; } = "markdown";

    public static InputDiagnosticsViewerOptions Parse(string[] args)
    {
        string? input = null;
        string? output = null;
        var format = "markdown";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--input":
                    input = Next();
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--format":
                    format = (Next() ?? format).ToLowerInvariant();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("--input is required.");
        if (format is not ("text" or "json" or "markdown"))
            throw new ArgumentException("--format must be text, json, or markdown.");

        return new InputDiagnosticsViewerOptions
        {
            InputPath = input!,
            OutputPath = output,
            Format = format,
        };
    }
}

internal sealed class InputDiagnosticsViewer
{
    private readonly InputDiagnosticsViewerOptions options;

    public InputDiagnosticsViewer(InputDiagnosticsViewerOptions options)
    {
        this.options = options;
    }

    public InputDiagnosticReport Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(options.InputPath));
        return ParseReport(doc.RootElement, Path.GetFileName(options.InputPath));
    }

    private static InputDiagnosticReport ParseReport(JsonElement root, string sourceName)
    {
        var events = ParseEvents(root);
        var summary = ParseSummary(root, events);

        return new InputDiagnosticReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            AppVersion = GetString(root, "appVersion"),
            PluginVersion = GetString(root, "pluginVersion"),
            MachineName = GetString(root, "machineName"),
            OsVersion = GetString(root, "osVersion"),
            Source = GetString(root, "source") is { Length: > 0 } source ? source : sourceName,
            Summary = summary,
            Events = events,
        };
    }

    private static InputDiagnosticSummary ParseSummary(JsonElement root, IReadOnlyList<InputDiagnosticEvent> events)
    {
        if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object)
        {
            return new InputDiagnosticSummary
            {
                EventCount = GetInt(summary, "eventCount", events.Count),
                MacroCount = GetInt(summary, "macroCount", events.Count(e => e.EventType == "MacroResolved")),
                MappedActionCount = GetInt(summary, "mappedActionCount", events.Count(e => e.EventType == "InputMapped")),
                RejectedCount = GetInt(summary, "rejectedCount", events.Count(e => e.EventType == "InputFiltered" && e.Accepted == false)),
                IssuesCount = GetInt(summary, "issuesCount", 0),
            };
        }

        return new InputDiagnosticSummary
        {
            EventCount = events.Count,
            MacroCount = events.Count(e => e.EventType == "MacroResolved"),
            MappedActionCount = events.Count(e => e.EventType == "InputMapped"),
            RejectedCount = events.Count(e => e.EventType == "InputFiltered" && e.Accepted == false),
            IssuesCount = 0,
        };
    }

    private static List<InputDiagnosticEvent> ParseEvents(JsonElement root)
    {
        var list = new List<InputDiagnosticEvent>();
        if (!root.TryGetProperty("events", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new InputDiagnosticEvent
            {
                EventType = GetString(item, "eventType"),
                Timestamp = GetDateTimeOffset(item, "timestamp") ?? DateTimeOffset.Now,
                TransportType = GetString(item, "transportType"),
                SourceDevice = GetString(item, "sourceDevice"),
                RawInput = GetString(item, "rawInput"),
                InputId = GetString(item, "inputId"),
                FilterName = GetString(item, "filterName"),
                Accepted = GetNullableBool(item, "accepted"),
                RejectReason = GetString(item, "rejectReason"),
                MappedAction = GetString(item, "mappedAction"),
                MappingSource = GetString(item, "mappingSource"),
                MacroName = GetString(item, "macroName"),
                StepCount = GetNullableInt(item, "stepCount"),
                ResolutionResult = GetString(item, "resolutionResult"),
                DispatchType = GetString(item, "dispatchType"),
                Target = GetString(item, "target"),
                PayloadSummary = GetString(item, "payloadSummary"),
            });
        }

        return list;
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement obj, string name, int fallback)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    private static int? GetNullableInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static bool? GetNullableBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}

internal sealed class InputDiagnosticsViewerWriter
{
    private readonly InputDiagnosticsViewerOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public InputDiagnosticsViewerWriter(InputDiagnosticsViewerOptions options)
    {
        this.options = options;
    }

    public void Write(InputDiagnosticReport report)
    {
        var output = ResolveOutputPath();
        var directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        OutputPath = output;

        switch (options.Format)
        {
            case "json":
                File.WriteAllText(output, ToJson(report), Encoding.UTF8);
                break;
            case "text":
                File.WriteAllText(output, ToText(report), Encoding.UTF8);
                break;
            default:
                File.WriteAllText(output, ToMarkdown(report), Encoding.UTF8);
                break;
        }
    }

    private string ResolveOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
            return options.OutputPath!;

        var dir = Path.Combine(Environment.CurrentDirectory, "tmp", "input-diagnostics-viewer");
        var ext = options.Format switch
        {
            "json" => ".json",
            "text" => ".txt",
            _ => ".md",
        };

        return Path.Combine(dir, $"report{ext}");
    }

    private static string ToMarkdown(InputDiagnosticReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Input Diagnostics Report");
        sb.AppendLine();
        AppendSummary(sb, report);
        AppendEvents(sb, report.Events);
        return sb.ToString();
    }

    private static string ToText(InputDiagnosticReport report)
        => ToMarkdown(report);

    private static string ToJson(InputDiagnosticReport report)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return JsonSerializer.Serialize(report, jsonOptions);
    }

    private static void AppendSummary(StringBuilder sb, InputDiagnosticReport report)
    {
        sb.AppendLine("## Summary");
        sb.AppendLine($"- source: {report.Source}");
        sb.AppendLine($"- eventCount: {report.Summary.EventCount}");
        sb.AppendLine($"- macroCount: {report.Summary.MacroCount}");
        sb.AppendLine($"- mappedActionCount: {report.Summary.MappedActionCount}");
        sb.AppendLine($"- rejectedCount: {report.Summary.RejectedCount}");
        sb.AppendLine($"- issues: {report.Summary.IssuesCount}");
        sb.AppendLine();
    }

    private static void AppendEvents(StringBuilder sb, IReadOnlyList<InputDiagnosticEvent> events)
    {
        sb.AppendLine("## Events");
        if (events.Count == 0)
        {
            sb.AppendLine("- none");
            sb.AppendLine();
            return;
        }

        foreach (var e in events)
        {
            sb.AppendLine($"- [{e.EventType}] inputId={e.InputId} transport={e.TransportType} source={e.SourceDevice} raw={e.RawInput}");

            if (!string.IsNullOrWhiteSpace(e.FilterName) || e.Accepted.HasValue)
                sb.AppendLine($"  - filter={e.FilterName} accepted={e.Accepted?.ToString() ?? "null"} rejectReason={e.RejectReason}");
            if (!string.IsNullOrWhiteSpace(e.MappedAction) || !string.IsNullOrWhiteSpace(e.MappingSource))
                sb.AppendLine($"  - mappedAction={e.MappedAction} mappingSource={e.MappingSource}");
            if (!string.IsNullOrWhiteSpace(e.MacroName) || e.StepCount.HasValue)
                sb.AppendLine($"  - macroName={e.MacroName} stepCount={e.StepCount?.ToString() ?? "null"} resolutionResult={e.ResolutionResult}");
            if (!string.IsNullOrWhiteSpace(e.DispatchType) || !string.IsNullOrWhiteSpace(e.Target))
                sb.AppendLine($"  - dispatchType={e.DispatchType} target={e.Target} payload={e.PayloadSummary}");
        }

        sb.AppendLine();
    }
}

internal sealed class InputDiagnosticReport
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

internal sealed class InputDiagnosticSummary
{
    public int EventCount { get; init; }
    public int MacroCount { get; init; }
    public int MappedActionCount { get; init; }
    public int RejectedCount { get; init; }
    public int IssuesCount { get; init; }
}

internal sealed class InputDiagnosticEvent
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
}
