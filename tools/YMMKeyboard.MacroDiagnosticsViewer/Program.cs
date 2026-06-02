using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = MacroDiagnosticsOptions.Parse(args);
var viewer = new MacroDiagnosticsViewer(options);
var report = viewer.Run();
var writer = new MacroDiagnosticsWriter(options);
writer.Write(report);

Console.WriteLine($"MacroDiagnosticsViewer completed. output={writer.OutputPath}");
Console.WriteLine($"eventCount={report.Summary.EventCount}, macroLookupCount={report.Summary.MacroLookupCount}, macroBindingCount={report.Summary.MacroBindingCount}, macroExpandedCount={report.Summary.MacroExpandedCount}, macroPlanCount={report.Summary.MacroPlanCount}, issues={report.Summary.IssuesCount}");

if (report.Summary.IssuesCount > 0)
    Environment.ExitCode = 1;

internal sealed class MacroDiagnosticsOptions
{
    public string? ScenarioPath { get; init; }
    public string? ReplayPath { get; init; }
    public string? BatchPath { get; init; }
    public string? OutputDir { get; init; }
    public string Format { get; init; } = "markdown";

    public static MacroDiagnosticsOptions Parse(string[] args)
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

        return new MacroDiagnosticsOptions
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
            : Path.Combine(Environment.CurrentDirectory, "tmp", "macro-diagnostics");
}

internal sealed class MacroDiagnosticsViewer
{
    private readonly MacroDiagnosticsOptions options;

    public MacroDiagnosticsViewer(MacroDiagnosticsOptions options)
    {
        this.options = options;
    }

    public MacroDiagnosticsReport Run()
    {
        return options.ScenarioPath is not null
            ? RunScenario(options.ScenarioPath)
            : options.ReplayPath is not null
                ? RunReplay(options.ReplayPath)
                : RunBatch(options.BatchPath!);
    }

    private MacroDiagnosticsReport RunScenario(string scenarioPath)
    {
        var scenario = MacroScenario.Parse(scenarioPath);
        var report = MacroDiagnosticsReport.FromScenario(scenario, scenarioPath, "scenario");
        return report;
    }

    private MacroDiagnosticsReport RunReplay(string replayPath)
    {
        var report = MacroDiagnosticsReport.Parse(replayPath);
        report.Mode = "replay";
        report.Issues = MacroReplayValidator.Validate(report, replayPath).ToList();
        report.Summary = report.BuildSummary();
        return report;
    }

    private MacroDiagnosticsReport RunBatch(string batchPath)
    {
        if (!Directory.Exists(batchPath))
            throw new DirectoryNotFoundException($"Scenario directory does not exist: {batchPath}");

        var scenarios = Directory.GetFiles(batchPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(MacroScenario.Parse)
            .ToList();

        var report = MacroDiagnosticsReport.FromScenarios(scenarios, batchPath, "batch");
        return report;
    }
}

internal sealed class MacroDiagnosticsWriter
{
    private readonly MacroDiagnosticsOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public MacroDiagnosticsWriter(MacroDiagnosticsOptions options)
    {
        this.options = options;
    }

    public void Write(MacroDiagnosticsReport report)
    {
        var directory = options.ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        var baseName = report.Mode == "replay"
            ? "macro-diagnostics-replay"
            : $"macro-diagnostics-{report.SourceName}";

        var jsonPath = Path.Combine(directory, $"{baseName}.json");
        var markdownPath = Path.Combine(directory, $"{baseName}.md");

        File.WriteAllText(jsonPath, MacroDiagnosticsFormatters.ToJson(report), Encoding.UTF8);
        File.WriteAllText(markdownPath, MacroDiagnosticsFormatters.ToMarkdown(report), Encoding.UTF8);

        OutputPath = options.Format switch
        {
            "json" => jsonPath,
            "text" => markdownPath,
            _ => markdownPath,
        };
    }
}

internal static class MacroReplayValidator
{
    public static IEnumerable<DiagnosticsIssue> Validate(MacroDiagnosticsReport report, string source)
    {
        if (report.Events.Count == 0)
        {
            yield return DiagnosticsIssue.Create("EmptyReplay", "No macro diagnostics events were found.", source, string.Empty);
            yield break;
        }

        var lookup = IndexOf(report, "MacroLookup");
        var binding = IndexOf(report, "MacroBinding");
        var expanded = IndexOf(report, "MacroExpanded");
        var validation = IndexOf(report, "MacroValidation");
        var plan = IndexOf(report, "MacroPlanCreated");

        if (lookup < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "MacroLookup is missing.", source, string.Empty);
        if (binding < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "MacroBinding is missing.", source, string.Empty);
        if (expanded < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "MacroExpanded is missing.", source, string.Empty);
        if (validation < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "MacroValidation is missing.", source, string.Empty);
        if (plan < 0)
            yield return DiagnosticsIssue.Create("MissingEvent", "MacroPlanCreated is missing.", source, string.Empty);

        if (lookup >= 0 && binding >= 0 && lookup > binding)
            yield return DiagnosticsIssue.Create("ReplayOrder", "MacroBinding appeared before MacroLookup.", source, string.Empty);
        if (binding >= 0 && expanded >= 0 && binding > expanded)
            yield return DiagnosticsIssue.Create("ReplayOrder", "MacroExpanded appeared before MacroBinding.", source, string.Empty);
        if (expanded >= 0 && validation >= 0 && expanded > validation)
            yield return DiagnosticsIssue.Create("ReplayOrder", "MacroValidation appeared before MacroExpanded.", source, string.Empty);
        if (validation >= 0 && plan >= 0 && validation > plan)
            yield return DiagnosticsIssue.Create("ReplayOrder", "MacroPlanCreated appeared before MacroValidation.", source, string.Empty);

        if (report.Summary.EventCount != report.Events.Count)
            yield return DiagnosticsIssue.Create("SummaryMismatch", "Event count does not match summary.", source, $"summary={report.Summary.EventCount}; actual={report.Events.Count}");

        if (report.Issues.Count > 0)
            yield return DiagnosticsIssue.Create("NestedIssues", "Replay report contains issues.", source, $"issues={report.Issues.Count}");
    }

    private static int IndexOf(MacroDiagnosticsReport report, string eventType)
        => report.Events.FindIndex(item => item.EventType == eventType);
}

internal sealed class MacroDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public MacroDiagnosticsSummary Summary { get; set; } = new();
    public List<MacroDiagnosticEvent> Events { get; set; } = [];
    public List<DiagnosticsIssue> Issues { get; set; } = [];

    public static MacroDiagnosticsReport FromScenario(MacroScenario scenario, string source, string mode)
    {
        return FromScenarios([scenario], source, mode);
    }

    public static MacroDiagnosticsReport FromScenarios(IReadOnlyList<MacroScenario> scenarios, string source, string mode)
    {
        var events = new List<MacroDiagnosticEvent>();
        var issues = new List<DiagnosticsIssue>();

        foreach (var scenario in scenarios)
        {
            var scenarioEvents = BuildEvents(scenario, source);
            events.AddRange(scenarioEvents.Events);
            issues.AddRange(scenarioEvents.Issues);
        }

        var report = new MacroDiagnosticsReport
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

    public static MacroDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new MacroDiagnosticsReport
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

    public MacroDiagnosticsSummary BuildSummary()
    {
        return new MacroDiagnosticsSummary
        {
            EventCount = Events.Count,
            MacroLookupCount = Events.Count(item => item.EventType == "MacroLookup"),
            MacroBindingCount = Events.Count(item => item.EventType == "MacroBinding"),
            MacroExpandedCount = Events.Count(item => item.EventType == "MacroExpanded"),
            MacroValidationCount = Events.Count(item => item.EventType == "MacroValidation"),
            MacroPlanCount = Events.Count(item => item.EventType == "MacroPlanCreated"),
            IssuesCount = Issues.Count,
        };
    }

    private static (List<MacroDiagnosticEvent> Events, List<DiagnosticsIssue> Issues) BuildEvents(MacroScenario scenario, string source)
    {
        var events = new List<MacroDiagnosticEvent>();
        var lookupResult = scenario.LookupResult;
        var bindingResult = scenario.BindingResult;
        var valid = scenario.Valid;
        var stepCount = scenario.ExpandedSteps.Count;
        var actionCount = scenario.ActionCount ?? stepCount;

        events.Add(new MacroDiagnosticEvent
        {
            EventType = "MacroLookup",
            Timestamp = DateTimeOffset.Now,
            MacroId = scenario.MacroId,
            LookupSource = scenario.LookupSource,
            LookupResult = lookupResult,
        });
        events.Add(new MacroDiagnosticEvent
        {
            EventType = "MacroBinding",
            Timestamp = DateTimeOffset.Now,
            MacroId = scenario.MacroId,
            Input = scenario.Input,
            BindingName = scenario.BindingName,
            BindingResult = bindingResult,
        });
        events.Add(new MacroDiagnosticEvent
        {
            EventType = "MacroExpanded",
            Timestamp = DateTimeOffset.Now,
            MacroId = scenario.MacroId,
            StepCount = stepCount,
            ExpandedSteps = scenario.ExpandedSteps,
        });
        events.Add(new MacroDiagnosticEvent
        {
            EventType = "MacroValidation",
            Timestamp = DateTimeOffset.Now,
            MacroId = scenario.MacroId,
            Valid = valid,
            Warnings = scenario.Warnings,
            Errors = scenario.Errors,
        });
        events.Add(new MacroDiagnosticEvent
        {
            EventType = "MacroPlanCreated",
            Timestamp = DateTimeOffset.Now,
            MacroId = scenario.MacroId,
            PlanId = scenario.PlanId ?? $"{scenario.MacroId}-plan",
            ActionCount = actionCount,
        });

        return (events, []);
    }

    private static MacroDiagnosticsSummary ParseSummary(JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Object)
            return new MacroDiagnosticsSummary();

        return new MacroDiagnosticsSummary
        {
            EventCount = GetInt(summary, "eventCount"),
            MacroLookupCount = GetInt(summary, "macroLookupCount"),
            MacroBindingCount = GetInt(summary, "macroBindingCount"),
            MacroExpandedCount = GetInt(summary, "macroExpandedCount"),
            MacroValidationCount = GetInt(summary, "macroValidationCount"),
            MacroPlanCount = GetInt(summary, "macroPlanCount"),
            IssuesCount = GetInt(summary, "issuesCount"),
        };
    }

    private static List<MacroDiagnosticEvent> ParseEvents(JsonElement root)
    {
        var list = new List<MacroDiagnosticEvent>();
        if (!root.TryGetProperty("events", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new MacroDiagnosticEvent
            {
                EventType = GetString(item, "eventType"),
                Timestamp = GetDateTimeOffset(item, "timestamp") ?? DateTimeOffset.Now,
                MacroId = GetString(item, "macroId"),
                Input = GetString(item, "input"),
                LookupSource = GetString(item, "lookupSource"),
                LookupResult = GetString(item, "lookupResult"),
                BindingName = GetString(item, "bindingName"),
                BindingResult = GetString(item, "bindingResult"),
                StepCount = GetNullableInt(item, "stepCount"),
                ExpandedSteps = GetStringList(item, "expandedSteps"),
                Valid = GetNullableBool(item, "valid"),
                Warnings = GetStringList(item, "warnings"),
                Errors = GetStringList(item, "errors"),
                PlanId = GetString(item, "planId"),
                ActionCount = GetNullableInt(item, "actionCount"),
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

    private static List<string> GetStringList(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);

        return list;
    }
}

internal static class MacroDiagnosticsFormatters
{
    public static string ToJson(MacroDiagnosticsReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }

    public static string ToMarkdown(MacroDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Macro Diagnostics Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- source: {report.Source}");
        sb.AppendLine($"- mode: {report.Mode}");
        sb.AppendLine($"- eventCount: {report.Summary.EventCount}");
        sb.AppendLine($"- macroLookupCount: {report.Summary.MacroLookupCount}");
        sb.AppendLine($"- macroBindingCount: {report.Summary.MacroBindingCount}");
        sb.AppendLine($"- macroExpandedCount: {report.Summary.MacroExpandedCount}");
        sb.AppendLine($"- macroPlanCount: {report.Summary.MacroPlanCount}");
        sb.AppendLine($"- issues: {report.Summary.IssuesCount}");
        sb.AppendLine();
        sb.AppendLine("## Events");
        foreach (var item in report.Events)
        {
            sb.AppendLine($"- [{item.EventType}] macroId={item.MacroId} lookup={item.LookupSource}/{item.LookupResult} binding={item.BindingName}/{item.BindingResult} steps={item.StepCount?.ToString() ?? "null"} plan={item.PlanId} actions={item.ActionCount?.ToString() ?? "null"}");
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

internal sealed class MacroScenario
{
    public string MacroId { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string LookupSource { get; init; } = string.Empty;
    public string LookupResult { get; init; } = string.Empty;
    public string BindingName { get; init; } = string.Empty;
    public string BindingResult { get; init; } = string.Empty;
    public List<string> ExpandedSteps { get; init; } = [];
    public bool Valid { get; init; } = true;
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public string? PlanId { get; init; }
    public int? ActionCount { get; init; }

    public static MacroScenario Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new MacroScenario
        {
            MacroId = GetString(root, "macroId"),
            Input = GetString(root, "input"),
            LookupSource = GetString(root, "lookupSource"),
            LookupResult = GetString(root, "lookupResult"),
            BindingName = GetString(root, "bindingName"),
            BindingResult = GetString(root, "bindingResult"),
            ExpandedSteps = GetStringList(root, "expandedSteps"),
            Valid = GetNullableBool(root, "valid") ?? true,
            Warnings = GetStringList(root, "warnings"),
            Errors = GetStringList(root, "errors"),
            PlanId = GetNullableString(root, "planId"),
            ActionCount = GetNullableInt(root, "actionCount"),
        };
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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

    private static List<string> GetStringList(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);

        return list;
    }
}

internal sealed class MacroDiagnosticsSummary
{
    public int EventCount { get; set; }
    public int MacroLookupCount { get; set; }
    public int MacroBindingCount { get; set; }
    public int MacroExpandedCount { get; set; }
    public int MacroValidationCount { get; set; }
    public int MacroPlanCount { get; set; }
    public int IssuesCount { get; set; }
}

internal sealed class MacroDiagnosticEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string MacroId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string LookupSource { get; set; } = string.Empty;
    public string LookupResult { get; set; } = string.Empty;
    public string BindingName { get; set; } = string.Empty;
    public string BindingResult { get; set; } = string.Empty;
    public int? StepCount { get; set; }
    public List<string> ExpandedSteps { get; set; } = [];
    public bool? Valid { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string PlanId { get; set; } = string.Empty;
    public int? ActionCount { get; set; }
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
