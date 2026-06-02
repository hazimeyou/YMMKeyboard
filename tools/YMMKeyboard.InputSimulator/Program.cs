using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = InputSimulatorOptions.Parse(args);
var simulator = new InputSimulator(options);
var report = simulator.Run();
var writer = new SimulationReportWriter(options);
writer.Write(report);

Console.WriteLine($"InputSimulator completed. output={writer.OutputPath}");
Console.WriteLine($"mode={report.Mode}, scenarios={report.Scenarios.Count}, diagnostics={report.GeneratedDiagnostics.Count}, issues={report.Issues.Count}");

if (report.Issues.Count > 0)
    Environment.ExitCode = 1;

internal sealed class InputSimulatorOptions
{
    public string? ScenarioPath { get; init; }
    public string? ReplayPath { get; init; }
    public string? BatchPath { get; init; }
    public string? OutputDir { get; init; }
    public string Format { get; init; } = "markdown";

    public static InputSimulatorOptions Parse(string[] args)
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

        var modeCount = new[] { scenario, replay, batch }.Count(value => !string.IsNullOrWhiteSpace(value));
        if (modeCount != 1)
            throw new ArgumentException("Specify exactly one of --scenario, --replay, or --batch.");

        if (format is not ("text" or "json" or "markdown"))
            throw new ArgumentException("--format must be text, json, or markdown.");

        return new InputSimulatorOptions
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
            : Path.Combine(Environment.CurrentDirectory, "tmp", "input-simulator");

    public string ResolveDiagnosticsDirectory()
        => Path.Combine(Environment.CurrentDirectory, "tmp", "input-diagnostics");
}

internal sealed class InputSimulator
{
    private readonly InputSimulatorOptions options;

    public InputSimulator(InputSimulatorOptions options)
    {
        this.options = options;
    }

    public InputSimulationReport Run()
    {
        var report = options.ScenarioPath is not null
            ? RunScenario(options.ScenarioPath)
            : options.ReplayPath is not null
                ? RunReplay(options.ReplayPath)
                : RunBatch(options.BatchPath!);

        return report;
    }

    private InputSimulationReport RunScenario(string scenarioPath)
    {
        var scenario = InputScenario.Parse(scenarioPath);
        var diagnostics = BuildDiagnostics(scenario);
        var diagnosticsPath = WriteDiagnostics(scenario, diagnostics);

        return new InputSimulationReport
        {
            GeneratedAt = DateTimeOffset.Now,
            Mode = "simulate",
            Source = scenarioPath,
            Scenarios = [new InputSimulationScenarioResult
            {
                ScenarioName = scenario.Name,
                ScenarioPath = scenarioPath,
                TransportType = scenario.TransportType,
                Input = scenario.Input,
                Status = "success",
                DiagnosticsPath = diagnosticsPath,
                EventCount = diagnostics.Events.Count,
                IssuesCount = diagnostics.Summary.IssuesCount,
            }],
            GeneratedDiagnostics = [diagnosticsPath],
            Issues = [],
        };
    }

    private InputSimulationReport RunReplay(string replayPath)
    {
        var diagnostics = InputDiagnosticsReport.Parse(replayPath);
        var issues = ValidateReplay(diagnostics, replayPath).ToList();

        return new InputSimulationReport
        {
            GeneratedAt = DateTimeOffset.Now,
            Mode = "replay",
            Source = replayPath,
            Scenarios = [new InputSimulationScenarioResult
            {
                ScenarioName = Path.GetFileNameWithoutExtension(replayPath),
                ScenarioPath = replayPath,
                TransportType = InferReplayTransport(diagnostics),
                Input = InferReplayInput(diagnostics),
                Status = issues.Count == 0 ? "success" : "failed",
                DiagnosticsPath = replayPath,
                EventCount = diagnostics.Events.Count,
                IssuesCount = diagnostics.Summary.IssuesCount,
            }],
            GeneratedDiagnostics = [],
            Issues = issues,
        };
    }

    private InputSimulationReport RunBatch(string batchPath)
    {
        if (!Directory.Exists(batchPath))
            throw new DirectoryNotFoundException($"Scenario directory does not exist: {batchPath}");

        var results = new List<InputSimulationScenarioResult>();
        var generatedDiagnostics = new List<string>();
        var issues = new List<SimulationIssue>();

        foreach (var scenarioPath in Directory.GetFiles(batchPath, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var scenario = InputScenario.Parse(scenarioPath);
            var diagnostics = BuildDiagnostics(scenario);
            var diagnosticsPath = WriteDiagnostics(scenario, diagnostics);

            generatedDiagnostics.Add(diagnosticsPath);
            results.Add(new InputSimulationScenarioResult
            {
                ScenarioName = scenario.Name,
                ScenarioPath = scenarioPath,
                TransportType = scenario.TransportType,
                Input = scenario.Input,
                Status = "success",
                DiagnosticsPath = diagnosticsPath,
                EventCount = diagnostics.Events.Count,
                IssuesCount = diagnostics.Summary.IssuesCount,
            });
        }

        if (results.Count == 0)
            issues.Add(SimulationIssue.Create("NoScenarios", "No scenario JSON files were found.", batchPath, string.Empty, string.Empty));

        return new InputSimulationReport
        {
            GeneratedAt = DateTimeOffset.Now,
            Mode = "batch",
            Source = batchPath,
            Scenarios = results,
            GeneratedDiagnostics = generatedDiagnostics,
            Issues = issues,
        };
    }

    private InputDiagnosticsReport BuildDiagnostics(InputScenario scenario)
    {
        var events = new List<InputDiagnosticEvent>();
        var transportType = NormalizeTransportType(scenario.TransportType);
        var sourceDevice = scenario.SourceDevice ?? $"virtual-{transportType.ToLowerInvariant()}-01";
        var inputId = scenario.InputId ?? $"scenario:{scenario.Name}";

        events.Add(new InputDiagnosticEvent
        {
            EventType = "InputReceived",
            Timestamp = DateTimeOffset.Now,
            TransportType = transportType,
            SourceDevice = sourceDevice,
            RawInput = scenario.Input,
            InputId = inputId,
        });

        var accepted = IsSupportedTransport(transportType);
        var rejectReason = accepted ? string.Empty : "unsupportedTransport";
        events.Add(new InputDiagnosticEvent
        {
            EventType = "InputFiltered",
            Timestamp = DateTimeOffset.Now,
            TransportType = transportType,
            SourceDevice = sourceDevice,
            RawInput = scenario.Input,
            InputId = inputId,
            FilterName = "transport-validation",
            Accepted = accepted,
            RejectReason = rejectReason,
        });

        if (accepted)
        {
            var mappedAction = MapAction(scenario.Input);
            events.Add(new InputDiagnosticEvent
            {
                EventType = "InputMapped",
                Timestamp = DateTimeOffset.Now,
                TransportType = transportType,
                SourceDevice = sourceDevice,
                RawInput = scenario.Input,
                InputId = inputId,
                MappedAction = mappedAction,
                MappingSource = "scenario",
            });

            if (mappedAction.StartsWith("Macro:", StringComparison.OrdinalIgnoreCase))
            {
                var macroName = mappedAction["Macro:".Length..];
                events.Add(new InputDiagnosticEvent
                {
                    EventType = "MacroResolved",
                    Timestamp = DateTimeOffset.Now,
                    TransportType = transportType,
                    SourceDevice = sourceDevice,
                    RawInput = scenario.Input,
                    InputId = inputId,
                    MacroName = macroName,
                    StepCount = 3,
                    ResolutionResult = "Resolved",
                });
            }

            events.Add(new InputDiagnosticEvent
            {
                EventType = "DispatchPrepared",
                Timestamp = DateTimeOffset.Now,
                TransportType = transportType,
                SourceDevice = sourceDevice,
                RawInput = scenario.Input,
                InputId = inputId,
                DispatchType = "Prepared",
                Target = "YMMKeyboardPlugin",
                PayloadSummary = mappedAction,
            });
        }

        return new InputDiagnosticsReport
        {
            GeneratedAt = DateTimeOffset.Now,
            AppVersion = "InputSimulationFoundationRC1",
            PluginVersion = "YMMKeyboardPlugin",
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            Source = scenario.Name,
            Summary = new InputDiagnosticSummary
            {
                EventCount = events.Count,
                MacroCount = events.Count(e => e.EventType == "MacroResolved"),
                MappedActionCount = events.Count(e => e.EventType == "InputMapped"),
                RejectedCount = events.Count(e => e.EventType == "InputFiltered" && e.Accepted == false),
                IssuesCount = 0,
            },
            Events = events,
        };
    }

    private IEnumerable<SimulationIssue> ValidateReplay(InputDiagnosticsReport report, string replayPath)
    {
        if (report.Events.Count == 0)
        {
            yield return SimulationIssue.Create("EmptyReplay", "No input-diagnostics events were found.", replayPath, string.Empty, string.Empty);
            yield break;
        }

        if (!report.Events.Any(eventItem => eventItem.EventType == "InputReceived"))
            yield return SimulationIssue.Create("MissingEvent", "InputReceived is missing.", replayPath, string.Empty, string.Empty);

        var accepted = report.Events.FirstOrDefault(eventItem => eventItem.EventType == "InputFiltered")?.Accepted;
        var receivedIndex = IndexOfEvent(report, "InputReceived");
        var filteredIndex = IndexOfEvent(report, "InputFiltered");
        var mappedIndex = IndexOfEvent(report, "InputMapped");
        var macroIndex = IndexOfEvent(report, "MacroResolved");
        var dispatchIndex = IndexOfEvent(report, "DispatchPrepared");
        var hasMapped = report.Events.Any(eventItem => eventItem.EventType == "InputMapped");
        var hasMacro = report.Events.Any(eventItem => eventItem.EventType == "MacroResolved");
        var hasDispatch = report.Events.Any(eventItem => eventItem.EventType == "DispatchPrepared");

        if (receivedIndex >= 0 && filteredIndex >= 0 && receivedIndex > filteredIndex)
            yield return SimulationIssue.Create("ReplayOrder", "InputFiltered appeared before InputReceived.", replayPath, string.Empty, string.Empty);

        if (accepted == false)
        {
            if (hasMapped || hasMacro || hasDispatch)
                yield return SimulationIssue.Create("ReplayOrder", "Rejected input should not continue to mapping or dispatch.", replayPath, string.Empty, string.Empty);
            yield break;
        }

        if (!hasMapped)
            yield return SimulationIssue.Create("MissingEvent", "InputMapped is missing.", replayPath, string.Empty, string.Empty);

        if (mappedIndex >= 0 && filteredIndex >= 0 && mappedIndex < filteredIndex)
            yield return SimulationIssue.Create("ReplayOrder", "InputMapped appeared before InputFiltered.", replayPath, string.Empty, string.Empty);

        if (macroIndex >= 0 && mappedIndex >= 0 && macroIndex < mappedIndex)
            yield return SimulationIssue.Create("ReplayOrder", "MacroResolved appeared before InputMapped.", replayPath, string.Empty, string.Empty);

        if (dispatchIndex >= 0 && macroIndex >= 0 && dispatchIndex < macroIndex)
            yield return SimulationIssue.Create("ReplayOrder", "DispatchPrepared appeared before MacroResolved.", replayPath, string.Empty, string.Empty);

        if (report.Events.Any(eventItem => eventItem.EventType == "MacroResolved") && !hasDispatch)
            yield return SimulationIssue.Create("MissingEvent", "DispatchPrepared is missing after macro resolution.", replayPath, string.Empty, string.Empty);

        if (!hasDispatch)
            yield return SimulationIssue.Create("MissingEvent", "DispatchPrepared is missing.", replayPath, string.Empty, string.Empty);
    }

    private static int IndexOfEvent(InputDiagnosticsReport report, string eventType)
        => report.Events.FindIndex(eventItem => eventItem.EventType == eventType);

    private string WriteDiagnostics(InputScenario scenario, InputDiagnosticsReport diagnostics)
    {
        var directory = options.ResolveDiagnosticsDirectory();
        Directory.CreateDirectory(directory);

        var outputPath = Path.Combine(directory, $"input-diagnostics-{scenario.Name}.json");
        File.WriteAllText(outputPath, InputDiagnosticsFormatters.ToJson(diagnostics), Encoding.UTF8);
        return outputPath;
    }

    private static string NormalizeTransportType(string transportType)
    {
        return transportType.Trim().ToLowerInvariant() switch
        {
            "hid" => "HID",
            "serial" or "com" => "Serial",
            _ => "Unknown",
        };
    }

    private static bool IsSupportedTransport(string transportType)
        => transportType is "HID" or "Serial";

    private static string MapAction(string input)
    {
        return input.Trim().ToUpperInvariant() switch
        {
            "KEY_A" => "KeyPress:A",
            "CTRL+A" => "Chord:Ctrl+A",
            "MACRO_01" => "Macro:MACRO_01",
            _ => $"Passthrough:{input.Trim()}",
        };
    }

    private static string InferReplayTransport(InputDiagnosticsReport report)
        => report.Events.FirstOrDefault(eventItem => !string.IsNullOrWhiteSpace(eventItem.TransportType))?.TransportType ?? string.Empty;

    private static string InferReplayInput(InputDiagnosticsReport report)
        => report.Events.FirstOrDefault(eventItem => !string.IsNullOrWhiteSpace(eventItem.RawInput))?.RawInput ?? string.Empty;
}

internal sealed class SimulationReportWriter
{
    private readonly InputSimulatorOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public SimulationReportWriter(InputSimulatorOptions options)
    {
        this.options = options;
    }

    public void Write(InputSimulationReport report)
    {
        var directory = options.ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        var jsonPath = Path.Combine(directory, "simulation-report.json");
        var markdownPath = Path.Combine(directory, "simulation-report.md");

        File.WriteAllText(jsonPath, InputSimulationFormatters.ToJson(report), Encoding.UTF8);
        File.WriteAllText(markdownPath, InputSimulationFormatters.ToMarkdown(report), Encoding.UTF8);

        OutputPath = options.Format switch
        {
            "json" => jsonPath,
            "text" => markdownPath,
            _ => markdownPath,
        };
    }
}

internal static class InputSimulationFormatters
{
    public static string ToJson(InputSimulationReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }

    public static string ToMarkdown(InputSimulationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Input Simulation Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- mode: {report.Mode}");
        sb.AppendLine($"- source: {report.Source}");
        sb.AppendLine($"- scenarios: {report.Scenarios.Count}");
        sb.AppendLine($"- generatedDiagnostics: {report.GeneratedDiagnostics.Count}");
        sb.AppendLine($"- issues: {report.Issues.Count}");
        sb.AppendLine();

        sb.AppendLine("## Scenarios");
        if (report.Scenarios.Count == 0)
        {
            sb.AppendLine("- none");
            sb.AppendLine();
        }
        else
        {
            foreach (var scenario in report.Scenarios)
            {
                sb.AppendLine($"- {scenario.ScenarioName} | transport={scenario.TransportType} | input={scenario.Input} | status={scenario.Status} | diagnostics={scenario.DiagnosticsPath} | events={scenario.EventCount} | issues={scenario.IssuesCount}");
            }

            sb.AppendLine();
        }

        if (report.GeneratedDiagnostics.Count > 0)
        {
            sb.AppendLine("## Generated Diagnostics");
            foreach (var path in report.GeneratedDiagnostics)
                sb.AppendLine($"- {path}");
            sb.AppendLine();
        }

        sb.AppendLine("## Issues");
        if (report.Issues.Count == 0)
        {
            sb.AppendLine("- none");
            sb.AppendLine();
        }
        else
        {
            foreach (var issue in report.Issues)
                sb.AppendLine($"- [{issue.Category}] {issue.Message} | source={issue.Source} | transport={issue.TransportType} | detail={issue.Detail}");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

internal sealed class InputSimulationReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public List<InputSimulationScenarioResult> Scenarios { get; init; } = [];
    public List<string> GeneratedDiagnostics { get; init; } = [];
    public List<SimulationIssue> Issues { get; init; } = [];
}

internal sealed class InputSimulationScenarioResult
{
    public string ScenarioName { get; init; } = string.Empty;
    public string ScenarioPath { get; init; } = string.Empty;
    public string TransportType { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string DiagnosticsPath { get; init; } = string.Empty;
    public int EventCount { get; init; }
    public int IssuesCount { get; init; }
}

internal sealed class SimulationIssue
{
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string TransportType { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    public static SimulationIssue Create(string category, string message, string source, string transportType, string detail)
    {
        return new SimulationIssue
        {
            Category = category,
            Message = message,
            Source = source,
            TransportType = transportType,
            Detail = detail,
        };
    }
}

internal sealed class InputScenario
{
    public string Name { get; init; } = string.Empty;
    public string TransportType { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string? SourceDevice { get; init; }
    public string? InputId { get; init; }

    public static InputScenario Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        if (!root.TryGetProperty("transportType", out var transportType) || transportType.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"Scenario is missing transportType: {path}");

        if (!root.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"Scenario is missing input: {path}");

        return new InputScenario
        {
            Name = Path.GetFileNameWithoutExtension(path),
            TransportType = transportType.GetString() ?? string.Empty,
            Input = input.GetString() ?? string.Empty,
            SourceDevice = GetOptionalString(root, "sourceDevice"),
            InputId = GetOptionalString(root, "inputId"),
        };
    }

    private static string? GetOptionalString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

internal sealed class InputDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string PluginVersion { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public InputDiagnosticSummary Summary { get; init; } = new();
    public List<InputDiagnosticEvent> Events { get; init; } = [];

    public static InputDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new InputDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            AppVersion = GetString(root, "appVersion"),
            PluginVersion = GetString(root, "pluginVersion"),
            MachineName = GetString(root, "machineName"),
            OsVersion = GetString(root, "osVersion"),
            Source = GetString(root, "source"),
            Summary = ParseSummary(root),
            Events = ParseEvents(root),
        };
    }

    private static InputDiagnosticSummary ParseSummary(JsonElement root)
    {
        if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Object)
            return new InputDiagnosticSummary();

        return new InputDiagnosticSummary
        {
            EventCount = GetInt(summary, "eventCount"),
            MacroCount = GetInt(summary, "macroCount"),
            MappedActionCount = GetInt(summary, "mappedActionCount"),
            RejectedCount = GetInt(summary, "rejectedCount"),
            IssuesCount = GetInt(summary, "issuesCount"),
        };
    }

    private static List<InputDiagnosticEvent> ParseEvents(JsonElement root)
    {
        var list = new List<InputDiagnosticEvent>();
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in events.EnumerateArray())
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

internal static class InputDiagnosticsFormatters
{
    public static string ToJson(InputDiagnosticsReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }
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
