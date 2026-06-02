using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = DispatchDiagnosticsOptions.Parse(args);
var viewer = new DispatchDiagnosticsViewer(options);
var report = viewer.Run();
var writer = new DispatchDiagnosticsWriter(options);
writer.Write(report);

Console.WriteLine($"DispatchDiagnosticsViewer completed. output={writer.OutputPath}");
Console.WriteLine($"eventCount={report.Summary.EventCount}, planCount={report.Summary.DispatchPlanCount}, actionCount={report.Summary.DispatchActionCount}, validationCount={report.Summary.DispatchValidationCount}, rejectedCount={report.Summary.DispatchRejectedCount}, readyCount={report.Summary.DispatchReadyCount}, issues={report.Summary.IssuesCount}");

if (report.Summary.IssuesCount > 0)
    Environment.ExitCode = 1;

internal sealed class DispatchDiagnosticsOptions
{
    public string? ScenarioPath { get; init; }
    public string? ReplayPath { get; init; }
    public string? BatchPath { get; init; }
    public string? OutputDir { get; init; }
    public string Format { get; init; } = "markdown";

    public static DispatchDiagnosticsOptions Parse(string[] args)
    {
        string? scenario = null;
        string? replay = null;
        string? batch = null;
        string? outputDir = null;
        var format = "markdown";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--scenario":
                    scenario = Next();
                    break;
                case "--replay":
                    replay = Next();
                    break;
                case "--batch":
                    batch = Next();
                    break;
                case "--output-dir":
                    outputDir = Next();
                    break;
                case "--format":
                    format = (Next() ?? format).ToLowerInvariant();
                    break;
            }
        }

        if (new[] { scenario, replay, batch }.Count(value => !string.IsNullOrWhiteSpace(value)) != 1)
            throw new ArgumentException("Specify exactly one of --scenario, --replay, or --batch.");

        if (format is not ("text" or "json" or "markdown"))
            throw new ArgumentException("--format must be text, json, or markdown.");

        return new DispatchDiagnosticsOptions
        {
            ScenarioPath = scenario,
            ReplayPath = replay,
            BatchPath = batch,
            OutputDir = outputDir,
            Format = format,
        };
    }

    public string ResolveOutputDirectory()
        => !string.IsNullOrWhiteSpace(OutputDir)
            ? OutputDir!
            : Path.Combine(Environment.CurrentDirectory, "tmp", "dispatch-diagnostics");
}

internal sealed class DispatchDiagnosticsViewer
{
    private readonly DispatchDiagnosticsOptions options;

    public DispatchDiagnosticsViewer(DispatchDiagnosticsOptions options)
    {
        this.options = options;
    }

    public DispatchDiagnosticsReport Run()
    {
        return options.ScenarioPath is not null
            ? RunScenario(options.ScenarioPath)
            : options.ReplayPath is not null
                ? RunReplay(options.ReplayPath)
                : RunBatch(options.BatchPath!);
    }

    private DispatchDiagnosticsReport RunScenario(string scenarioPath)
    {
        var scenario = DispatchScenario.Parse(scenarioPath);
        return DispatchDiagnosticsReport.FromScenario(scenario, scenarioPath, "scenario");
    }

    private DispatchDiagnosticsReport RunReplay(string replayPath)
    {
        var report = DispatchDiagnosticsReport.Parse(replayPath);
        report.Mode = "replay";
        report.Issues = DispatchReplayValidator.Validate(report, replayPath).ToList();
        report.Summary = report.BuildSummary();
        return report;
    }

    private DispatchDiagnosticsReport RunBatch(string batchPath)
    {
        if (!Directory.Exists(batchPath))
            throw new DirectoryNotFoundException($"Scenario directory does not exist: {batchPath}");

        var scenarios = Directory.GetFiles(batchPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(DispatchScenario.Parse)
            .ToList();

        return DispatchDiagnosticsReport.FromScenarios(scenarios, batchPath, "batch");
    }
}

internal sealed class DispatchDiagnosticsWriter
{
    private readonly DispatchDiagnosticsOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public DispatchDiagnosticsWriter(DispatchDiagnosticsOptions options)
    {
        this.options = options;
    }

    public void Write(DispatchDiagnosticsReport report)
    {
        var directory = options.ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        var baseName = report.Mode == "replay"
            ? "dispatch-diagnostics-replay"
            : $"dispatch-diagnostics-{report.SourceName}";

        var jsonPath = Path.Combine(directory, $"{baseName}.json");
        var markdownPath = Path.Combine(directory, $"{baseName}.md");

        File.WriteAllText(jsonPath, DispatchDiagnosticsFormatters.ToJson(report), Encoding.UTF8);
        File.WriteAllText(markdownPath, DispatchDiagnosticsFormatters.ToMarkdown(report), Encoding.UTF8);

        OutputPath = options.Format switch
        {
            "json" => jsonPath,
            "text" => markdownPath,
            _ => markdownPath,
        };
    }
}

internal static class DispatchReplayValidator
{
    public static IEnumerable<DiagnosticsIssue> Validate(DispatchDiagnosticsReport report, string source)
    {
        if (report.Events.Count == 0)
        {
            yield return DiagnosticsIssue.Create("EmptyReplay", "No dispatch diagnostics events were found.", source, string.Empty);
            yield break;
        }

        var plan = IndexOf(report, "DispatchPlanCreated");
        var action = IndexOf(report, "DispatchActionGenerated");
        var validation = IndexOf(report, "DispatchValidation");
        var rejected = IndexOf(report, "DispatchRejected");
        var ready = IndexOf(report, "DispatchReady");

        if (plan < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "DispatchPlanCreated is missing.", source, string.Empty);
        if (validation < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "DispatchValidation is missing.", source, string.Empty);

        if (plan >= 0 && action >= 0 && plan > action)
            yield return DiagnosticsIssue.Create("ReplayOrder", "DispatchActionGenerated appeared before DispatchPlanCreated.", source, string.Empty);
        if (action >= 0 && validation >= 0 && action > validation)
            yield return DiagnosticsIssue.Create("ReplayOrder", "DispatchValidation appeared before DispatchActionGenerated.", source, string.Empty);
        if (validation >= 0 && rejected >= 0 && validation > rejected)
            yield return DiagnosticsIssue.Create("ReplayOrder", "DispatchRejected appeared before DispatchValidation.", source, string.Empty);
        if (rejected >= 0 && ready >= 0 && rejected > ready)
            yield return DiagnosticsIssue.Create("ReplayOrder", "DispatchReady appeared before DispatchRejected.", source, string.Empty);

        if (report.Summary.EventCount != report.Events.Count)
            yield return DiagnosticsIssue.Create("SummaryMismatch", "Event count does not match summary.", source, $"summary={report.Summary.EventCount}; actual={report.Events.Count}");

        if (report.Issues.Count > 0)
            yield return DiagnosticsIssue.Create("NestedIssues", "Replay report contains issues.", source, $"issues={report.Issues.Count}");
    }

    private static int IndexOf(DispatchDiagnosticsReport report, string eventType)
        => report.Events.FindIndex(item => item.EventType == eventType);
}

internal sealed class DispatchDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DispatchDiagnosticsSummary Summary { get; set; } = new();
    public List<DispatchDiagnosticEvent> Events { get; set; } = [];
    public List<DiagnosticsIssue> Issues { get; set; } = [];

    public static DispatchDiagnosticsReport FromScenario(DispatchScenario scenario, string source, string mode)
        => FromScenarios([scenario], source, mode);

    public static DispatchDiagnosticsReport FromScenarios(IReadOnlyList<DispatchScenario> scenarios, string source, string mode)
    {
        var events = new List<DispatchDiagnosticEvent>();
        var issues = new List<DiagnosticsIssue>();

        foreach (var scenario in scenarios)
        {
            var generated = BuildEvents(scenario, source);
            events.AddRange(generated.Events);
            issues.AddRange(generated.Issues);
        }

        var report = new DispatchDiagnosticsReport
        {
            GeneratedAt = DateTimeOffset.Now,
            Source = source,
            SourceName = Path.GetFileNameWithoutExtension(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Mode = mode,
            Events = events,
            Issues = issues,
        };
        report.Summary = report.BuildSummary();
        return report;
    }

    public static DispatchDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new DispatchDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            Source = GetString(root, "source"),
            SourceName = GetString(root, "sourceName"),
            Mode = GetString(root, "mode"),
            Summary = ParseSummary(root),
            Events = ParseEvents(root),
            Issues = ParseIssues(root),
        };
    }

    public DispatchDiagnosticsSummary BuildSummary()
    {
        return new DispatchDiagnosticsSummary
        {
            EventCount = Events.Count,
            DispatchPlanCount = Events.Count(item => item.EventType == "DispatchPlanCreated"),
            DispatchActionCount = Events.Count(item => item.EventType == "DispatchActionGenerated"),
            DispatchValidationCount = Events.Count(item => item.EventType == "DispatchValidation"),
            DispatchRejectedCount = Events.Count(item => item.EventType == "DispatchRejected"),
            DispatchReadyCount = Events.Count(item => item.EventType == "DispatchReady"),
            IssuesCount = Issues.Count,
        };
    }

    private static (List<DispatchDiagnosticEvent> Events, List<DiagnosticsIssue> Issues) BuildEvents(DispatchScenario scenario, string source)
    {
        var events = new List<DispatchDiagnosticEvent>();
        var actionCount = scenario.Actions.Count;
        var validationResult = scenario.ValidationResult;
        var readyState = scenario.ReadyState ?? string.Equals(validationResult, "ok", StringComparison.OrdinalIgnoreCase);

        events.Add(new DispatchDiagnosticEvent
        {
            EventType = "DispatchPlanCreated",
            Timestamp = DateTimeOffset.Now,
            DispatchId = scenario.DispatchId,
            Target = scenario.Target,
            ActionCount = actionCount,
        });

        foreach (var action in scenario.Actions)
        {
            events.Add(new DispatchDiagnosticEvent
            {
                EventType = "DispatchActionGenerated",
                Timestamp = DateTimeOffset.Now,
                DispatchId = scenario.DispatchId,
                ActionType = action.ActionType,
                PayloadSummary = action.PayloadSummary,
            });
        }

        events.Add(new DispatchDiagnosticEvent
        {
            EventType = "DispatchValidation",
            Timestamp = DateTimeOffset.Now,
            DispatchId = scenario.DispatchId,
            ValidationResult = validationResult,
        });

        if (!string.Equals(validationResult, "ok", StringComparison.OrdinalIgnoreCase))
        {
            events.Add(new DispatchDiagnosticEvent
            {
                EventType = "DispatchRejected",
                Timestamp = DateTimeOffset.Now,
                DispatchId = scenario.DispatchId,
                RejectReason = scenario.RejectReason ?? "validation failed",
            });
        }

        if (readyState)
        {
            events.Add(new DispatchDiagnosticEvent
            {
                EventType = "DispatchReady",
                Timestamp = DateTimeOffset.Now,
                DispatchId = scenario.DispatchId,
                ReadyState = true,
            });
        }

        return (events, []);
    }

    private static DispatchDiagnosticsSummary ParseSummary(JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Object)
            return new DispatchDiagnosticsSummary();

        return new DispatchDiagnosticsSummary
        {
            EventCount = GetInt(summary, "eventCount"),
            DispatchPlanCount = GetInt(summary, "dispatchPlanCount"),
            DispatchActionCount = GetInt(summary, "dispatchActionCount"),
            DispatchValidationCount = GetInt(summary, "dispatchValidationCount"),
            DispatchRejectedCount = GetInt(summary, "dispatchRejectedCount"),
            DispatchReadyCount = GetInt(summary, "dispatchReadyCount"),
            IssuesCount = GetInt(summary, "issuesCount"),
        };
    }

    private static List<DispatchDiagnosticEvent> ParseEvents(JsonElement root)
    {
        var list = new List<DispatchDiagnosticEvent>();
        if (!root.TryGetProperty("events", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new DispatchDiagnosticEvent
            {
                EventType = GetString(item, "eventType"),
                Timestamp = GetDateTimeOffset(item, "timestamp") ?? DateTimeOffset.Now,
                DispatchId = GetString(item, "dispatchId"),
                Target = GetString(item, "target"),
                ActionCount = GetNullableInt(item, "actionCount"),
                ActionType = GetString(item, "actionType"),
                PayloadSummary = GetString(item, "payloadSummary"),
                ValidationResult = GetString(item, "validationResult"),
                RejectReason = GetString(item, "rejectReason"),
                ReadyState = GetNullableBool(item, "readyState"),
            });
        }

        return list;
    }

    private static List<DiagnosticsIssue> ParseIssues(JsonElement root)
    {
        var list = new List<DiagnosticsIssue>();
        if (!root.TryGetProperty("issues", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new DiagnosticsIssue
            {
                Category = GetString(item, "category"),
                Message = GetString(item, "message"),
                Source = GetString(item, "source"),
                Key = GetString(item, "key"),
                Detail = GetString(item, "detail"),
            });
        }

        return list;
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

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

internal static class DispatchDiagnosticsFormatters
{
    public static string ToJson(DispatchDiagnosticsReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }

    public static string ToMarkdown(DispatchDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Dispatch Diagnostics Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- source: {report.Source}");
        sb.AppendLine($"- mode: {report.Mode}");
        sb.AppendLine($"- eventCount: {report.Summary.EventCount}");
        sb.AppendLine($"- dispatchPlanCount: {report.Summary.DispatchPlanCount}");
        sb.AppendLine($"- dispatchActionCount: {report.Summary.DispatchActionCount}");
        sb.AppendLine($"- dispatchValidationCount: {report.Summary.DispatchValidationCount}");
        sb.AppendLine($"- dispatchRejectedCount: {report.Summary.DispatchRejectedCount}");
        sb.AppendLine($"- dispatchReadyCount: {report.Summary.DispatchReadyCount}");
        sb.AppendLine($"- issues: {report.Summary.IssuesCount}");
        sb.AppendLine();
        sb.AppendLine("## Events");
        foreach (var item in report.Events)
        {
            sb.AppendLine($"- [{item.EventType}] dispatchId={item.DispatchId} target={item.Target} actionCount={item.ActionCount?.ToString() ?? "null"} actionType={item.ActionType} payload={item.PayloadSummary} validation={item.ValidationResult} reject={item.RejectReason} ready={item.ReadyState?.ToString() ?? "null"}");
        }
        if (report.Events.Count == 0)
            sb.AppendLine("- none");
        sb.AppendLine();
        sb.AppendLine("## Issues");
        if (report.Issues.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var issue in report.Issues)
                sb.AppendLine($"- [{issue.Category}] {issue.Message} | source={issue.Source} | key={issue.Key} | detail={issue.Detail}");
        }
        sb.AppendLine();
        return sb.ToString();
    }
}

internal sealed class DispatchScenario
{
    public string DispatchId { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public int ActionCount { get; init; }
    public List<DispatchAction> Actions { get; init; } = [];
    public string ValidationResult { get; init; } = string.Empty;
    public string? RejectReason { get; init; }
    public bool? ReadyState { get; init; }

    public static DispatchScenario Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new DispatchScenario
        {
            DispatchId = GetString(root, "dispatchId"),
            Target = GetString(root, "target"),
            ActionCount = GetInt(root, "actionCount"),
            Actions = GetActions(root),
            ValidationResult = GetString(root, "validationResult"),
            RejectReason = GetNullableString(root, "rejectReason"),
            ReadyState = GetNullableBool(root, "readyState"),
        };
    }

    private static List<DispatchAction> GetActions(JsonElement root)
    {
        var list = new List<DispatchAction>();
        if (!root.TryGetProperty("actions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new DispatchAction
            {
                ActionType = GetString(item, "actionType"),
                PayloadSummary = GetString(item, "payloadSummary"),
            });
        }

        return list;
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string? GetNullableString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? GetNullableBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;
}

internal sealed class DispatchAction
{
    public string ActionType { get; init; } = string.Empty;
    public string PayloadSummary { get; init; } = string.Empty;
}

internal sealed class DispatchDiagnosticsSummary
{
    public int EventCount { get; set; }
    public int DispatchPlanCount { get; set; }
    public int DispatchActionCount { get; set; }
    public int DispatchValidationCount { get; set; }
    public int DispatchRejectedCount { get; set; }
    public int DispatchReadyCount { get; set; }
    public int IssuesCount { get; set; }
}

internal sealed class DispatchDiagnosticEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string DispatchId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public int? ActionCount { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadSummary { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
    public string RejectReason { get; set; } = string.Empty;
    public bool? ReadyState { get; set; }
}

internal sealed class DiagnosticsIssue
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    public static DiagnosticsIssue Create(string category, string message, string source, string key)
        => new() { Category = category, Message = message, Source = source, Key = key };

    public static DiagnosticsIssue Create(string category, string message, string source, string key, string detail)
        => new() { Category = category, Message = message, Source = source, Key = key, Detail = detail };
}
