using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = DiagnosticsReplayOptions.Parse(args);
var replay = new DiagnosticsReplay(options);
var report = replay.Run();
var writer = new DiagnosticsReplayWriter(options);
writer.Write(report);

Console.WriteLine($"DiagnosticsReplay completed. output={writer.OutputPath}");
Console.WriteLine($"timelineCount={report.Timeline.Count}, deviceCount={report.DeviceSummaryCount}, pluginCount={report.PluginSummaryCount}, inputCount={report.InputEventCount}, macroCount={report.MacroEventCount}, dispatchCount={report.DispatchEventCount}, issues={report.Issues.Count}");

if (report.Issues.Count > 0)
    Environment.ExitCode = 1;

internal sealed class DiagnosticsReplayOptions
{
    public string DevicePath { get; init; } = string.Empty;
    public string PluginPath { get; init; } = string.Empty;
    public string InputPath { get; init; } = string.Empty;
    public string MacroPath { get; init; } = string.Empty;
    public string DispatchPath { get; init; } = string.Empty;
    public string? OutputDir { get; init; }
    public string Format { get; init; } = "markdown";

    public static DiagnosticsReplayOptions Parse(string[] args)
    {
        string? device = null;
        string? plugin = null;
        string? input = null;
        string? macro = null;
        string? dispatch = null;
        string? outputDir = null;
        var format = "markdown";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--device":
                    device = Next();
                    break;
                case "--plugin":
                    plugin = Next();
                    break;
                case "--input":
                    input = Next();
                    break;
                case "--macro":
                    macro = Next();
                    break;
                case "--dispatch":
                    dispatch = Next();
                    break;
                case "--output-dir":
                    outputDir = Next();
                    break;
                case "--format":
                    format = (Next() ?? format).ToLowerInvariant();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(plugin) || string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(macro) || string.IsNullOrWhiteSpace(dispatch))
            throw new ArgumentException("--device, --plugin, --input, --macro, and --dispatch are required.");

        if (format is not ("text" or "json" or "markdown"))
            throw new ArgumentException("--format must be text, json, or markdown.");

        return new DiagnosticsReplayOptions
        {
            DevicePath = device!,
            PluginPath = plugin!,
            InputPath = input!,
            MacroPath = macro!,
            DispatchPath = dispatch!,
            OutputDir = outputDir,
            Format = format,
        };
    }

    public string ResolveOutputDirectory()
        => !string.IsNullOrWhiteSpace(OutputDir)
            ? OutputDir!
            : Path.Combine(Environment.CurrentDirectory, "tmp", "diagnostics-replay");
}

internal sealed class DiagnosticsReplay
{
    private readonly DiagnosticsReplayOptions options;

    public DiagnosticsReplay(DiagnosticsReplayOptions options)
    {
        this.options = options;
    }

    public DiagnosticsReplayReport Run()
    {
        var device = DeviceInspectorReport.Parse(options.DevicePath);
        var plugin = PluginDiagnosticsReport.Parse(options.PluginPath);
        var input = InputDiagnosticsReport.Parse(options.InputPath);
        var macro = MacroDiagnosticsReport.Parse(options.MacroPath);
        var dispatch = DispatchDiagnosticsReport.Parse(options.DispatchPath);

        var issues = new List<DiagnosticsIssue>();
        var timeline = new List<ReplayTimelineEntry>();

        issues.AddRange(ValidateDevice(device, options.DevicePath));
        issues.AddRange(ValidatePlugin(plugin, options.PluginPath));
        issues.AddRange(ValidateInput(input, options.InputPath));
        issues.AddRange(ValidateMacro(macro, options.MacroPath));
        issues.AddRange(ValidateDispatch(dispatch, options.DispatchPath));

        timeline.Add(new ReplayTimelineEntry
        {
            Timestamp = device.GeneratedAt,
            Phase = "Device",
            EventType = "DeviceInspectorLoaded",
            Source = Path.GetFileName(options.DevicePath),
            Summary = $"hid={device.HidDevices.Count}; com={device.ComPorts.Count}; candidates={device.MatchedYmmKeyboardCandidates.Count}",
        });

        timeline.Add(new ReplayTimelineEntry
        {
            Timestamp = plugin.GeneratedAt,
            Phase = "Plugin",
            EventType = "PluginDiagnosticsLoaded",
            Source = Path.GetFileName(options.PluginPath),
            Summary = $"candidates={plugin.ConnectionCandidates.Count}; selected={(plugin.SelectedCandidate is null ? "none" : "present")}",
        });

        timeline.AddRange(input.Events.Select(item => new ReplayTimelineEntry
        {
            Timestamp = item.Timestamp,
            Phase = "Input",
            EventType = item.EventType,
            Source = Path.GetFileName(options.InputPath),
            Summary = BuildInputSummary(item),
        }));

        timeline.AddRange(macro.Events.Select(item => new ReplayTimelineEntry
        {
            Timestamp = item.Timestamp,
            Phase = "Macro",
            EventType = item.EventType,
            Source = Path.GetFileName(options.MacroPath),
            Summary = BuildMacroSummary(item),
        }));

        timeline.AddRange(dispatch.Events.Select(item => new ReplayTimelineEntry
        {
            Timestamp = item.Timestamp,
            Phase = "Dispatch",
            EventType = item.EventType,
            Source = Path.GetFileName(options.DispatchPath),
            Summary = BuildDispatchSummary(item),
        }));

        timeline = timeline
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => PhaseRank(entry.Phase))
            .ThenBy(entry => entry.EventType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (timeline.Count == 0)
            issues.Add(DiagnosticsIssue.Create("EmptyTimeline", "Replay produced no timeline entries.", string.Empty, string.Empty));

        return new DiagnosticsReplayReport
        {
            GeneratedAt = DateTimeOffset.Now,
            DeviceSource = Path.GetFileName(options.DevicePath),
            PluginSource = Path.GetFileName(options.PluginPath),
            InputSource = Path.GetFileName(options.InputPath),
            MacroSource = Path.GetFileName(options.MacroPath),
            DispatchSource = Path.GetFileName(options.DispatchPath),
            DeviceSummaryCount = device.HidDevices.Count + device.ComPorts.Count,
            PluginSummaryCount = plugin.ConnectionCandidates.Count,
            InputEventCount = input.Events.Count,
            MacroEventCount = macro.Events.Count,
            DispatchEventCount = dispatch.Events.Count,
            Timeline = timeline,
            Issues = issues,
        };
    }

    private static IEnumerable<DiagnosticsIssue> ValidateDevice(DeviceInspectorReport report, string source)
    {
        if (report.HidDevices.Count == 0 && report.ComPorts.Count == 0)
            yield return DiagnosticsIssue.Create("EmptyDevice", "Device report has no devices or ports.", source, string.Empty);
    }

    private static IEnumerable<DiagnosticsIssue> ValidatePlugin(PluginDiagnosticsReport report, string source)
    {
        if (report.ConnectionCandidates.Count == 0)
            yield return DiagnosticsIssue.Create("EmptyPlugin", "Plugin report has no candidates.", source, string.Empty);
    }

    private static IEnumerable<DiagnosticsIssue> ValidateInput(InputDiagnosticsReport report, string source)
    {
        if (report.Events.Count == 0)
            yield return DiagnosticsIssue.Create("EmptyInput", "Input report has no events.", source, string.Empty);
    }

    private static IEnumerable<DiagnosticsIssue> ValidateMacro(MacroDiagnosticsReport report, string source)
    {
        if (report.Events.Count == 0)
            yield return DiagnosticsIssue.Create("EmptyMacro", "Macro report has no events.", source, string.Empty);
    }

    private static IEnumerable<DiagnosticsIssue> ValidateDispatch(DispatchDiagnosticsReport report, string source)
    {
        if (report.Events.Count == 0)
            yield return DiagnosticsIssue.Create("EmptyDispatch", "Dispatch report has no events.", source, string.Empty);
    }

    private static string BuildInputSummary(InputDiagnosticEvent item)
    {
        return item.EventType switch
        {
            "InputReceived" => $"inputId={item.InputId}; raw={item.RawInput}",
            "InputFiltered" => $"accepted={item.Accepted?.ToString() ?? "null"}; reason={item.RejectReason}",
            "InputMapped" => $"mappedAction={item.MappedAction}; source={item.MappingSource}",
            "MacroResolved" => $"macroName={item.MacroName}; steps={item.StepCount?.ToString() ?? "null"}; result={item.ResolutionResult}",
            "DispatchPrepared" => $"dispatchType={item.DispatchType}; target={item.Target}; payload={item.PayloadSummary}",
            _ => $"inputId={item.InputId}",
        };
    }

    private static string BuildMacroSummary(MacroDiagnosticEvent item)
    {
        return item.EventType switch
        {
            "MacroLookup" => $"macroId={item.MacroId}; lookup={item.LookupSource}/{item.LookupResult}",
            "MacroBinding" => $"macroId={item.MacroId}; input={item.Input}; binding={item.BindingName}/{item.BindingResult}",
            "MacroExpanded" => $"macroId={item.MacroId}; stepCount={item.StepCount}; steps={string.Join(" | ", item.ExpandedSteps)}",
            "MacroValidation" => $"macroId={item.MacroId}; valid={item.Valid?.ToString() ?? "null"}; warnings={string.Join(" | ", item.Warnings)}; errors={string.Join(" | ", item.Errors)}",
            "MacroPlanCreated" => $"macroId={item.MacroId}; planId={item.PlanId}; actionCount={item.ActionCount?.ToString() ?? "null"}",
            _ => item.MacroId,
        };
    }

    private static string BuildDispatchSummary(DispatchDiagnosticEvent item)
    {
        return item.EventType switch
        {
            "DispatchPlanCreated" => $"dispatchId={item.DispatchId}; target={item.Target}; actionCount={item.ActionCount?.ToString() ?? "null"}",
            "DispatchActionGenerated" => $"dispatchId={item.DispatchId}; actionType={item.ActionType}; payload={item.PayloadSummary}",
            "DispatchValidation" => $"dispatchId={item.DispatchId}; validation={item.ValidationResult}",
            "DispatchRejected" => $"dispatchId={item.DispatchId}; reason={item.RejectReason}",
            "DispatchReady" => $"dispatchId={item.DispatchId}; ready={item.ReadyState?.ToString() ?? "null"}",
            _ => item.DispatchId,
        };
    }

    private static int PhaseRank(string phase)
        => phase switch
        {
            "Device" => 0,
            "Plugin" => 1,
            "Input" => 2,
            "Macro" => 3,
            "Dispatch" => 4,
            _ => 99,
        };
}

internal sealed class DiagnosticsReplayWriter
{
    private readonly DiagnosticsReplayOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public DiagnosticsReplayWriter(DiagnosticsReplayOptions options)
    {
        this.options = options;
    }

    public void Write(DiagnosticsReplayReport report)
    {
        var directory = options.ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        var jsonPath = Path.Combine(directory, "replay-report.json");
        var markdownPath = Path.Combine(directory, "replay-report.md");

        File.WriteAllText(jsonPath, DiagnosticsReplayFormatters.ToJson(report), Encoding.UTF8);
        File.WriteAllText(markdownPath, DiagnosticsReplayFormatters.ToMarkdown(report), Encoding.UTF8);

        OutputPath = options.Format switch
        {
            "json" => jsonPath,
            "text" => markdownPath,
            _ => markdownPath,
        };
    }
}

internal static class DiagnosticsReplayFormatters
{
    public static string ToJson(DiagnosticsReplayReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }

    public static string ToMarkdown(DiagnosticsReplayReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Diagnostics Replay Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- deviceSource: {report.DeviceSource}");
        sb.AppendLine($"- pluginSource: {report.PluginSource}");
        sb.AppendLine($"- inputSource: {report.InputSource}");
        sb.AppendLine($"- macroSource: {report.MacroSource}");
        sb.AppendLine($"- dispatchSource: {report.DispatchSource}");
        sb.AppendLine($"- deviceCount: {report.DeviceSummaryCount}");
        sb.AppendLine($"- pluginCount: {report.PluginSummaryCount}");
        sb.AppendLine($"- inputCount: {report.InputEventCount}");
        sb.AppendLine($"- macroCount: {report.MacroEventCount}");
        sb.AppendLine($"- dispatchCount: {report.DispatchEventCount}");
        sb.AppendLine($"- timelineCount: {report.Timeline.Count}");
        sb.AppendLine($"- issues: {report.Issues.Count}");
        sb.AppendLine();
        sb.AppendLine("## Timeline");
        if (report.Timeline.Count == 0)
        {
            sb.AppendLine("- none");
            sb.AppendLine();
        }
        else
        {
            foreach (var item in report.Timeline)
                sb.AppendLine($"- {item.Timestamp:O} [{item.Phase}] {item.EventType} | source={item.Source} | {item.Summary}");
            sb.AppendLine();
        }
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

internal sealed class DiagnosticsReplayReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string DeviceSource { get; set; } = string.Empty;
    public string PluginSource { get; set; } = string.Empty;
    public string InputSource { get; set; } = string.Empty;
    public string MacroSource { get; set; } = string.Empty;
    public string DispatchSource { get; set; } = string.Empty;
    public int DeviceSummaryCount { get; set; }
    public int PluginSummaryCount { get; set; }
    public int InputEventCount { get; set; }
    public int MacroEventCount { get; set; }
    public int DispatchEventCount { get; set; }
    public List<ReplayTimelineEntry> Timeline { get; set; } = [];
    public List<DiagnosticsIssue> Issues { get; set; } = [];
}

internal sealed class ReplayTimelineEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}


internal sealed class DeviceInspectorReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<object> HidDevices { get; set; } = [];
    public List<string> ComPorts { get; set; } = [];
    public List<object> MatchedYmmKeyboardCandidates { get; set; } = [];

    public static DeviceInspectorReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new DeviceInspectorReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            HidDevices = GetObjectList(root, "hidDevices"),
            ComPorts = GetStringList(root, "comPorts"),
            MatchedYmmKeyboardCandidates = GetObjectList(root, "matchedYmmKeyboardCandidates"),
        };
    }

    private static List<object> GetObjectList(JsonElement root, string name)
    {
        var list = new List<object>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.ToString()!);

        return list;
    }

    private static List<string> GetStringList(JsonElement root, string name)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);

        return list;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}

internal sealed class PluginDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<object> ConnectionCandidates { get; set; } = [];
    public object? SelectedCandidate { get; set; }

    public static PluginDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new PluginDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            ConnectionCandidates = GetObjectList(root, "connectionCandidates"),
            SelectedCandidate = root.TryGetProperty("selectedCandidate", out var selected) && selected.ValueKind != JsonValueKind.Null ? selected.ToString() : null,
        };
    }

    private static List<object> GetObjectList(JsonElement root, string name)
    {
        var list = new List<object>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.ToString()!);

        return list;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}

internal sealed class InputDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<InputDiagnosticEvent> Events { get; set; } = [];

    public static InputDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new InputDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            Events = ParseEvents(root),
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

internal sealed class MacroDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<MacroDiagnosticEvent> Events { get; set; } = [];

    public static MacroDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new MacroDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            Events = ParseEvents(root),
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

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

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

    private static DateTimeOffset? GetDateTimeOffset(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}

internal sealed class DispatchDiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public List<DispatchDiagnosticEvent> Events { get; set; } = [];

    public static DispatchDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        return new DispatchDiagnosticsReport
        {
            GeneratedAt = GetDateTimeOffset(root, "generatedAt") ?? DateTimeOffset.Now,
            Events = ParseEvents(root),
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

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

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

internal sealed class InputDiagnosticEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string TransportType { get; set; } = string.Empty;
    public string SourceDevice { get; set; } = string.Empty;
    public string RawInput { get; set; } = string.Empty;
    public string InputId { get; set; } = string.Empty;
    public string FilterName { get; set; } = string.Empty;
    public bool? Accepted { get; set; }
    public string RejectReason { get; set; } = string.Empty;
    public string MappedAction { get; set; } = string.Empty;
    public string MappingSource { get; set; } = string.Empty;
    public string MacroName { get; set; } = string.Empty;
    public int? StepCount { get; set; }
    public string ResolutionResult { get; set; } = string.Empty;
    public string DispatchType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string PayloadSummary { get; set; } = string.Empty;
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
