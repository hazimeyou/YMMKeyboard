using System.Text;

var options = ComparerOptions.Parse(args);
var comparer = new DiagnosticsComparer(options);
var report = comparer.Compare();
var writer = new ComparisonReportWriter(options);
writer.Write(report);

Console.WriteLine($"DiagnosticsComparer completed. output={writer.OutputPath}");
Console.WriteLine($"issues={report.Summary.TotalIssues}, matchedHid={report.Summary.MatchedHidCount}, matchedPorts={report.Summary.MatchedComCount}, selected={report.Summary.SelectedCandidateCount}");

internal sealed class ComparerOptions
{
    public string InspectorPath { get; init; } = string.Empty;
    public string PluginPath { get; init; } = string.Empty;
    public string? OutputPath { get; init; }
    public string Format { get; init; } = "markdown";

    public static ComparerOptions Parse(string[] args)
    {
        string? inspector = null;
        string? plugin = null;
        string? output = null;
        var format = "markdown";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--inspector":
                    inspector = Next();
                    break;
                case "--plugin":
                    plugin = Next();
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--format":
                    format = (Next() ?? format).ToLowerInvariant();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(inspector))
            throw new ArgumentException("--inspector is required.");
        if (string.IsNullOrWhiteSpace(plugin))
            throw new ArgumentException("--plugin is required.");
        if (format is not ("text" or "json" or "markdown"))
            throw new ArgumentException("--format must be text, json, or markdown.");

        return new ComparerOptions
        {
            InspectorPath = inspector!,
            PluginPath = plugin!,
            OutputPath = output,
            Format = format,
        };
    }
}

internal sealed class DiagnosticsComparer
{
    private readonly ComparerOptions options;

    public DiagnosticsComparer(ComparerOptions options)
    {
        this.options = options;
    }

    public ComparisonReport Compare()
    {
        var inspector = InspectorReport.Parse(options.InspectorPath);
        var plugin = PluginReport.Parse(options.PluginPath);

        var hidMatches = CompareHidDevices(inspector, plugin);
        var comMatches = CompareComPorts(inspector, plugin);
        var candidateMatches = CompareCandidates(inspector, plugin);

        var issues = new List<ComparisonIssue>();
        issues.AddRange(hidMatches.Issues);
        issues.AddRange(comMatches.Issues);
        issues.AddRange(candidateMatches.Issues);
        issues.AddRange(CompareSelectedCandidate(inspector, plugin, candidateMatches));
        issues.AddRange(CompareRejectedCandidates(inspector, plugin, candidateMatches));
        issues.AddRange(CompareWarningsAndErrors(inspector, plugin));

        var summary = new ComparisonSummary
        {
            InspectorHidCount = inspector.HidDevices.Count,
            PluginHidCount = plugin.DetectedHidDevices.Count,
            InspectorComCount = inspector.ComPorts.Count,
            PluginComCount = plugin.DetectedComPorts.Count,
            MatchedHidCount = hidMatches.Matched.Count,
            InspectorOnlyHidCount = hidMatches.InspectorOnly.Count,
            PluginOnlyHidCount = hidMatches.PluginOnly.Count,
            MatchedComCount = comMatches.Matched.Count,
            InspectorOnlyComCount = comMatches.InspectorOnly.Count,
            PluginOnlyComCount = comMatches.PluginOnly.Count,
            MatchedCandidateCount = candidateMatches.Matched.Count,
            SelectedCandidateCount = plugin.SelectedCandidate is null ? 0 : 1,
            TotalIssues = issues.Count,
        };

        return new ComparisonReport
        {
            GeneratedAt = DateTimeOffset.Now,
            InspectorSource = inspector.SourceName,
            PluginSource = plugin.SourceName,
            Summary = summary,
            MatchedHidDevices = hidMatches.Matched,
            InspectorOnlyHidDevices = hidMatches.InspectorOnly,
            PluginOnlyHidDevices = hidMatches.PluginOnly,
            MatchedComPorts = comMatches.Matched,
            InspectorOnlyComPorts = comMatches.InspectorOnly,
            PluginOnlyComPorts = comMatches.PluginOnly,
            MatchedCandidates = candidateMatches.Matched,
            SelectedCandidate = plugin.SelectedCandidate,
            RejectedCandidates = plugin.RejectedCandidates,
            Issues = issues,
        };
    }

    private static ComparisonSet<NormalizedHidDevice> CompareHidDevices(InspectorReport inspector, PluginReport plugin)
    {
        var inspectorMap = inspector.HidDevices
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var pluginMap = plugin.DetectedHidDevices
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return ComparisonSet<NormalizedHidDevice>.Build(inspectorMap, pluginMap, "HID");
    }

    private static ComparisonSet<string> CompareComPorts(InspectorReport inspector, PluginReport plugin)
    {
        var inspectorMap = inspector.ComPorts.Select(p => p.ToUpperInvariant()).Distinct().ToDictionary(p => p, p => p);
        var pluginMap = plugin.DetectedComPorts.Select(p => p.ToUpperInvariant()).Distinct().ToDictionary(p => p, p => p);
        return ComparisonSet<string>.Build(inspectorMap, pluginMap, "COM");
    }

    private static CandidateComparisonResult CompareCandidates(InspectorReport inspector, PluginReport plugin)
    {
        var inspectorCandidates = inspector.MatchedYmmKeyboardCandidates
            .Where(c => !string.IsNullOrWhiteSpace(c.TransportType))
            .Select(c => c.ToCandidate())
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var pluginCandidates = plugin.ConnectionCandidates
            .Where(c => !string.IsNullOrWhiteSpace(c.TransportType))
            .Select(c => c.ToCandidate())
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = ComparisonSet<NormalizedCandidate>.Build(inspectorCandidates, pluginCandidates, "candidate");
        result.Issues.AddRange(FindScoreMismatch(inspectorCandidates, pluginCandidates));
        result.Issues.AddRange(FindIdentityMismatch(inspectorCandidates, pluginCandidates));
        result.Issues.AddRange(FindProtocolMismatch(inspectorCandidates, pluginCandidates));
        result.Issues.AddRange(FindMissingSerial(inspectorCandidates, pluginCandidates));
        result.Issues.AddRange(FindMissingHidUsage(inspectorCandidates, pluginCandidates));
        return new CandidateComparisonResult(result);
    }

    private static IEnumerable<ComparisonIssue> CompareSelectedCandidate(
        InspectorReport inspector,
        PluginReport plugin,
        CandidateComparisonResult candidateMatches)
    {
        if (plugin.SelectedCandidate is null)
            yield break;

        var key = plugin.SelectedCandidate.ToCandidate().Key;
        if (!candidateMatches.PluginByKey.ContainsKey(key))
        {
            yield return ComparisonIssue.Create(
                "Selected",
                "Plugin selected candidate is not present in the inspector/plugin comparison set.",
                key,
                plugin.SelectedCandidate.TransportType,
                plugin.SelectedCandidate.MatchScore.ToString());
        }
    }

    private static IEnumerable<ComparisonIssue> CompareRejectedCandidates(
        InspectorReport inspector,
        PluginReport plugin,
        CandidateComparisonResult candidateMatches)
    {
        foreach (var rejected in plugin.RejectedCandidates)
        {
            var candidate = rejected.ToCandidate();
            if (!candidateMatches.PluginByKey.ContainsKey(candidate.Key))
            {
                yield return ComparisonIssue.Create(
                    "Rejected",
                    "Rejected candidate was not present in plugin comparison set.",
                    candidate.Key,
                    candidate.TransportType,
                    rejected.MatchScore.ToString());
            }
        }
    }

    private static IEnumerable<ComparisonIssue> FindIdentityMismatch(
        IReadOnlyDictionary<string, NormalizedCandidate> inspectorCandidates,
        IReadOnlyDictionary<string, NormalizedCandidate> pluginCandidates)
    {
        var inspectorByCoarseKey = inspectorCandidates.Values
            .GroupBy(BuildCoarseKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var pluginByCoarseKey = pluginCandidates.Values
            .GroupBy(BuildCoarseKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var coarseKey in inspectorByCoarseKey.Keys.Intersect(pluginByCoarseKey.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var inspectorList = inspectorByCoarseKey[coarseKey];
            var pluginList = pluginByCoarseKey[coarseKey];

            foreach (var inspectorCandidate in inspectorList)
            {
                foreach (var pluginCandidate in pluginList)
                {
                    if (string.Equals(inspectorCandidate.Key, pluginCandidate.Key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IdentityDiffers(inspectorCandidate, pluginCandidate))
                    {
                        yield return ComparisonIssue.Create(
                            "IdentityMismatch",
                            $"Identity differs for the same coarse device key. inspector={inspectorCandidate.IdentitySummary()} plugin={pluginCandidate.IdentitySummary()}",
                            coarseKey,
                            pluginCandidate.TransportType,
                            $"inspectorKey={inspectorCandidate.Key}; pluginKey={pluginCandidate.Key}");
                    }
                }
            }
        }
    }

    private static IEnumerable<ComparisonIssue> CompareWarningsAndErrors(InspectorReport inspector, PluginReport plugin)
    {
        foreach (var warning in inspector.Warnings)
            yield return ComparisonIssue.Create("InspectorWarning", warning, "", "", "");

        foreach (var warning in plugin.Warnings)
            yield return ComparisonIssue.Create("PluginWarning", warning, "", "", "");

        foreach (var error in plugin.Errors)
            yield return ComparisonIssue.Create("PluginError", error, "", "", "");
    }

    private static IEnumerable<ComparisonIssue> FindScoreMismatch(
        IReadOnlyDictionary<string, NormalizedCandidate> inspectorCandidates,
        IReadOnlyDictionary<string, NormalizedCandidate> pluginCandidates)
    {
        foreach (var (key, pluginCandidate) in pluginCandidates)
        {
            if (!inspectorCandidates.TryGetValue(key, out var inspectorCandidate))
                continue;

            var expected = inspectorCandidate.ExpectedScore;
            if (expected <= 0)
                continue;

            if (pluginCandidate.MatchScore + 500 < expected)
            {
                yield return ComparisonIssue.Create(
                    "ScoreMismatch",
                    $"Score is lower than expected. expected>={expected}, actual={pluginCandidate.MatchScore}",
                    key,
                    pluginCandidate.TransportType,
                    pluginCandidate.MatchScore.ToString());
            }
        }
    }

    private static IEnumerable<ComparisonIssue> FindProtocolMismatch(
        IReadOnlyDictionary<string, NormalizedCandidate> inspectorCandidates,
        IReadOnlyDictionary<string, NormalizedCandidate> pluginCandidates)
    {
        foreach (var (key, pluginCandidate) in pluginCandidates)
        {
            if (!inspectorCandidates.TryGetValue(key, out var inspectorCandidate))
                continue;

            if (!string.Equals(inspectorCandidate.TransportType, pluginCandidate.TransportType, StringComparison.OrdinalIgnoreCase))
            {
                yield return ComparisonIssue.Create(
                    "ProtocolMismatch",
                    $"Transport differs. inspector={inspectorCandidate.TransportType}, plugin={pluginCandidate.TransportType}",
                    key,
                    pluginCandidate.TransportType,
                    pluginCandidate.MatchScore.ToString());
            }
        }
    }

    private static IEnumerable<ComparisonIssue> FindMissingSerial(
        IReadOnlyDictionary<string, NormalizedCandidate> inspectorCandidates,
        IReadOnlyDictionary<string, NormalizedCandidate> pluginCandidates)
    {
        foreach (var (key, pluginCandidate) in pluginCandidates)
        {
            if (!inspectorCandidates.TryGetValue(key, out var inspectorCandidate))
                continue;

            if (string.IsNullOrWhiteSpace(inspectorCandidate.Serial) || string.IsNullOrWhiteSpace(pluginCandidate.Serial))
            {
                yield return ComparisonIssue.Create(
                    "MissingSerial",
                    "Serial is missing on one side of the comparison.",
                    key,
                    pluginCandidate.TransportType,
                    pluginCandidate.Serial);
            }
        }
    }

    private static IEnumerable<ComparisonIssue> FindMissingHidUsage(
        IReadOnlyDictionary<string, NormalizedCandidate> inspectorCandidates,
        IReadOnlyDictionary<string, NormalizedCandidate> pluginCandidates)
    {
        foreach (var (key, pluginCandidate) in pluginCandidates)
        {
            if (!inspectorCandidates.TryGetValue(key, out var inspectorCandidate))
                continue;

            if (pluginCandidate.TransportType.Equals("HID", StringComparison.OrdinalIgnoreCase)
                && (pluginCandidate.UsagePage is null || pluginCandidate.Usage is null || pluginCandidate.UsagePage == 0 || pluginCandidate.Usage == 0))
            {
                yield return ComparisonIssue.Create(
                    "MissingHidUsage",
                    "HID usage page or usage is missing in plugin diagnostics.",
                    key,
                    pluginCandidate.TransportType,
                    $"{pluginCandidate.UsagePage?.ToString() ?? "null"}:{pluginCandidate.Usage?.ToString() ?? "null"}");
            }
        }
    }

    private static bool IdentityDiffers(NormalizedCandidate inspector, NormalizedCandidate plugin)
    {
        return !string.Equals(inspector.ProductName, plugin.ProductName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(inspector.Manufacturer, plugin.Manufacturer, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(inspector.Serial, plugin.Serial, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCoarseKey(NormalizedCandidate candidate)
    {
        if (candidate.TransportType.Equals("COM", StringComparison.OrdinalIgnoreCase)
            || candidate.TransportType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
        {
            return $"serial:{Normalize(candidate.ComPort)}";
        }

        return $"{candidate.TransportType}:{candidate.Vid?.ToString("X4") ?? "0000"}:{candidate.Pid?.ToString("X4") ?? "0000"}:{candidate.UsagePage?.ToString("X4") ?? "0000"}:{candidate.Usage?.ToString("X4") ?? "0000"}";
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

internal sealed class ComparisonSet<T>
{
    public List<T> Matched { get; init; } = [];
    public List<T> InspectorOnly { get; init; } = [];
    public List<T> PluginOnly { get; init; } = [];
    public List<ComparisonIssue> Issues { get; init; } = [];

    public static ComparisonSet<T> Build<TKey>(
        IReadOnlyDictionary<TKey, T> inspector,
        IReadOnlyDictionary<TKey, T> plugin,
        string label)
        where TKey : notnull
    {
        var result = new ComparisonSet<T>();
        var keys = new HashSet<TKey>(inspector.Keys);
        keys.UnionWith(plugin.Keys);

        foreach (var key in keys)
        {
            var inInspector = inspector.TryGetValue(key, out var inspectorValue);
            var inPlugin = plugin.TryGetValue(key, out var pluginValue);

            if (inInspector && inPlugin)
            {
                result.Matched.Add(inspectorValue!);
                continue;
            }

            if (inInspector)
                result.InspectorOnly.Add(inspectorValue!);
            if (inPlugin)
                result.PluginOnly.Add(pluginValue!);

            result.Issues.Add(ComparisonIssue.Create(
                inInspector
                    ? (label.Equals("HID", StringComparison.OrdinalIgnoreCase)
                        ? "HidVisibleButNotEvaluated"
                        : label.Equals("COM", StringComparison.OrdinalIgnoreCase)
                            ? "ComVisibleButNotEvaluated"
                            : "InspectorOnly")
                    : "PluginOnly",
                $"{label} exists only on {(inInspector ? "inspector" : "plugin")} side.",
                key!.ToString() ?? string.Empty,
                label,
                string.Empty));
        }

        return result;
    }
}

internal sealed class CandidateComparisonResult
{
    public CandidateComparisonResult(ComparisonSet<NormalizedCandidate> set)
    {
        Matched = set.Matched;
        InspectorOnly = set.InspectorOnly;
        PluginOnly = set.PluginOnly;
        Issues = set.Issues;
        InspectorByKey = set.Matched.Concat(set.InspectorOnly).ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        PluginByKey = set.Matched.Concat(set.PluginOnly).ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    public List<NormalizedCandidate> Matched { get; }
    public List<NormalizedCandidate> InspectorOnly { get; }
    public List<NormalizedCandidate> PluginOnly { get; }
    public List<ComparisonIssue> Issues { get; }
    public Dictionary<string, NormalizedCandidate> InspectorByKey { get; }
    public Dictionary<string, NormalizedCandidate> PluginByKey { get; }
}

internal sealed class ComparisonReportWriter
{
    private readonly ComparerOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public ComparisonReportWriter(ComparerOptions options)
    {
        this.options = options;
    }

    public void Write(ComparisonReport report)
    {
        var output = ResolveOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        OutputPath = output;

        switch (options.Format)
        {
            case "json":
                File.WriteAllText(output, ComparisonFormatters.ToJson(report), Encoding.UTF8);
                break;
            case "text":
                File.WriteAllText(output, ComparisonFormatters.ToText(report), Encoding.UTF8);
                break;
            default:
                File.WriteAllText(output, ComparisonFormatters.ToMarkdown(report), Encoding.UTF8);
                break;
        }
    }

    private string ResolveOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
            return options.OutputPath!;

        var dir = Path.Combine(Environment.CurrentDirectory, "tmp", "diagnostics-comparer");
        var ext = options.Format switch
        {
            "json" => ".json",
            "text" => ".txt",
            _ => ".md",
        };

        return Path.Combine(dir, $"report{ext}");
    }
}

internal static class ComparisonFormatters
{
    public static string ToMarkdown(ComparisonReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Diagnostics Comparison Report");
        sb.AppendLine();
        AppendSummary(sb, report);
        AppendIssueTable(sb, report.Issues);
        AppendCandidateSection(sb, "Matched HID", report.MatchedHidDevices.Select(x => x.ToString()));
        AppendCandidateSection(sb, "Inspector Only HID", report.InspectorOnlyHidDevices.Select(x => x.ToString()));
        AppendCandidateSection(sb, "Plugin Only HID", report.PluginOnlyHidDevices.Select(x => x.ToString()));
        AppendCandidateSection(sb, "Matched COM", report.MatchedComPorts);
        AppendCandidateSection(sb, "Inspector Only COM", report.InspectorOnlyComPorts);
        AppendCandidateSection(sb, "Plugin Only COM", report.PluginOnlyComPorts);
        AppendCandidateSection(sb, "Matched Candidates", report.MatchedCandidates.Select(x => x.ToString()));
        AppendCandidateSection(sb, "Rejected Candidates", report.RejectedCandidates.Select(x => x.ToString()));
        if (report.SelectedCandidate is not null)
        {
            sb.AppendLine("## Selected Candidate");
            sb.AppendLine(FormatConnectionCandidate(report.SelectedCandidate));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string ToText(ComparisonReport report)
    {
        return ToMarkdown(report);
    }

    public static string ToJson(ComparisonReport report)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        return System.Text.Json.JsonSerializer.Serialize(report, options);
    }

    private static void AppendSummary(StringBuilder sb, ComparisonReport report)
    {
        sb.AppendLine("## Summary");
        sb.AppendLine($"- inspectorSource: {report.InspectorSource}");
        sb.AppendLine($"- pluginSource: {report.PluginSource}");
        sb.AppendLine($"- matchedHid: {report.Summary.MatchedHidCount}");
        sb.AppendLine($"- matchedCom: {report.Summary.MatchedComCount}");
        sb.AppendLine($"- selectedCandidates: {report.Summary.SelectedCandidateCount}");
        sb.AppendLine($"- totalIssues: {report.Summary.TotalIssues}");
        sb.AppendLine();
    }

    private static void AppendIssueTable(StringBuilder sb, IReadOnlyList<ComparisonIssue> issues)
    {
        sb.AppendLine("## Issues");
        if (issues.Count == 0)
        {
            sb.AppendLine("No issues.");
            sb.AppendLine();
            return;
        }

        foreach (var issue in issues)
            sb.AppendLine($"- [{issue.Category}] {issue.Message} | key={issue.Key} | transport={issue.TransportType} | detail={issue.Detail}");

        sb.AppendLine();
    }

    private static void AppendCandidateSection(StringBuilder sb, string title, IEnumerable<string?> lines)
    {
        sb.AppendLine($"## {title}");
        var any = false;
        foreach (var line in lines)
        {
            any = true;
            sb.AppendLine($"- {line ?? string.Empty}");
        }
        if (!any)
            sb.AppendLine("- none");
        sb.AppendLine();
    }

    private static string FormatConnectionCandidate(ConnectionCandidateRecord candidate)
    {
        return $"transport={candidate.TransportType} vid={candidate.Vid?.ToString("X4") ?? "0000"} pid={candidate.Pid?.ToString("X4") ?? "0000"} product={candidate.ProductName} maker={candidate.Manufacturer} serial={candidate.Serial} com={candidate.ComPort} usage={candidate.UsagePage?.ToString("X4") ?? "0000"}:{candidate.Usage?.ToString("X4") ?? "0000"} score={candidate.MatchScore} selected={candidate.Selected} reasons={string.Join("|", candidate.MatchReasons)} rejects={string.Join("|", candidate.RejectReasons)}";
    }
}

internal sealed class ComparisonReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string InspectorSource { get; init; } = string.Empty;
    public string PluginSource { get; init; } = string.Empty;
    public ComparisonSummary Summary { get; init; } = new();
    public List<NormalizedHidDevice> MatchedHidDevices { get; init; } = [];
    public List<NormalizedHidDevice> InspectorOnlyHidDevices { get; init; } = [];
    public List<NormalizedHidDevice> PluginOnlyHidDevices { get; init; } = [];
    public List<string> MatchedComPorts { get; init; } = [];
    public List<string> InspectorOnlyComPorts { get; init; } = [];
    public List<string> PluginOnlyComPorts { get; init; } = [];
    public List<NormalizedCandidate> MatchedCandidates { get; init; } = [];
    public ConnectionCandidateRecord? SelectedCandidate { get; init; }
    public List<ConnectionCandidateRecord> RejectedCandidates { get; init; } = [];
    public List<ComparisonIssue> Issues { get; init; } = [];
}

internal sealed class ComparisonSummary
{
    public int InspectorHidCount { get; init; }
    public int PluginHidCount { get; init; }
    public int InspectorComCount { get; init; }
    public int PluginComCount { get; init; }
    public int MatchedHidCount { get; init; }
    public int InspectorOnlyHidCount { get; init; }
    public int PluginOnlyHidCount { get; init; }
    public int MatchedComCount { get; init; }
    public int InspectorOnlyComCount { get; init; }
    public int PluginOnlyComCount { get; init; }
    public int MatchedCandidateCount { get; init; }
    public int SelectedCandidateCount { get; init; }
    public int TotalIssues { get; init; }
}

internal sealed class ComparisonIssue
{
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string TransportType { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    public static ComparisonIssue Create(string category, string message, string key, string transportType, string detail)
    {
        return new ComparisonIssue
        {
            Category = category,
            Message = message,
            Key = key,
            TransportType = transportType,
            Detail = detail,
        };
    }
}

internal sealed class NormalizedHidDevice
{
    public string Key { get; init; } = string.Empty;
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

    public override string ToString()
    {
        return $"HID {Vid:X4}:{Pid:X4} product={ProductName} maker={Manufacturer} serial={Serial} usage={UsagePage:X4}:{Usage:X4} kind={IdentityKind}";
    }
}

internal sealed class NormalizedCandidate
{
    public string Key { get; init; } = string.Empty;
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
    public string IdentityKind { get; init; } = string.Empty;
    public string SourceKind { get; init; } = string.Empty;
    public int ExpectedScore { get; init; }

    public NormalizedCandidate ToCandidate() => this;

    public override string ToString()
    {
        return $"{TransportType} key={Key} score={MatchScore} selected={Selected} reasons={string.Join("|", MatchReasons)} rejects={string.Join("|", RejectReasons)}";
    }

    public string IdentitySummary()
        => $"{TransportType} {Vid?.ToString("X4") ?? "0000"}:{Pid?.ToString("X4") ?? "0000"} product={ProductName} maker={Manufacturer} serial={Serial} usage={UsagePage?.ToString("X4") ?? "0000"}:{Usage?.ToString("X4") ?? "0000"} port={ComPort}";

    public static string BuildKey(string transportType, int? vid, int? pid, string product, string maker, string serial, int? usagePage, int? usage, string comPort)
    {
        if (transportType.Equals("COM", StringComparison.OrdinalIgnoreCase) || transportType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            return $"serial:{Normalize(comPort)}";

        return $"{transportType}:{vid?.ToString("X4") ?? "0000"}:{pid?.ToString("X4") ?? "0000"}:{Normalize(product)}:{Normalize(maker)}:{Normalize(serial)}:{usagePage?.ToString("X4") ?? "0000"}:{usage?.ToString("X4") ?? "0000"}";
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    }

internal sealed class ConnectionCandidateRecord
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

    public NormalizedCandidate ToCandidate()
    {
        return new NormalizedCandidate
        {
            Key = NormalizedCandidate.BuildKey(TransportType, Vid, Pid, ProductName, Manufacturer, Serial, UsagePage, Usage, ComPort),
            TransportType = TransportType,
            Vid = Vid,
            Pid = Pid,
            ProductName = ProductName,
            Manufacturer = Manufacturer,
            Serial = Serial,
            ComPort = ComPort,
            UsagePage = UsagePage,
            Usage = Usage,
            MatchScore = MatchScore,
            MatchReasons = MatchReasons,
            RejectReasons = RejectReasons,
            Selected = Selected,
            ExpectedScore = ExpectedScore(),
            IdentityKind = IdentityKindFromFields(),
            SourceKind = "plugin",
        };
    }

    public override string ToString()
    {
        return $"{TransportType} vid={Vid?.ToString("X4") ?? "0000"} pid={Pid?.ToString("X4") ?? "0000"} product={ProductName} maker={Manufacturer} serial={Serial} com={ComPort} usage={UsagePage?.ToString("X4") ?? "0000"}:{Usage?.ToString("X4") ?? "0000"} score={MatchScore} selected={Selected} reasons={string.Join("|", MatchReasons)} rejects={string.Join("|", RejectReasons)}";
    }

    private int ExpectedScore()
    {
        if (TransportType.Equals("HID", StringComparison.OrdinalIgnoreCase))
        {
            if (Vid == 0x2E8A && Pid == 0x4020)
                return 10000;
            if (Vid == 0x2E8A && Pid == 0x101F)
                return 8000;
            if (UsagePage == 0xFF00 && Usage == 0x0001)
                return 7000;
            return 1500;
        }

        if (TransportType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(ComPort) ? 0 : 1000;

        return 0;
    }

    private string IdentityKindFromFields()
    {
        if (Vid == 0x2E8A && Pid == 0x4020)
            return "formal";
        if (Vid == 0x2E8A && Pid == 0x101F)
            return "temporary";
        if (Vid == 0x2E8A)
            return "likely-ymm";
        return "other";
    }

}

internal sealed class InspectorReport
{
    public string SourceName { get; init; } = string.Empty;
    public List<NormalizedHidDevice> HidDevices { get; init; } = [];
    public List<string> ComPorts { get; init; } = [];
    public List<NormalizedCandidate> MatchedYmmKeyboardCandidates { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public static InspectorReport Parse(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var report = new InspectorReport
        {
            SourceName = Path.GetFileName(path),
            HidDevices = ParseHidDevices(root),
            ComPorts = ParseStringArray(root, "comPorts"),
            MatchedYmmKeyboardCandidates = ParseInspectorCandidates(root),
            Warnings = ParseStringArray(root, "warnings"),
        };
        return report;
    }

    private static List<NormalizedHidDevice> ParseHidDevices(System.Text.Json.JsonElement root)
    {
        var list = new List<NormalizedHidDevice>();
        if (!root.TryGetProperty("hidDevices", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            var vid = GetInt(item, "vendorId");
            var pid = GetInt(item, "productId");
            var product = GetString(item, "productName");
            var maker = GetString(item, "manufacturer");
            var serial = GetString(item, "serialNumber");
            var usagePage = GetInt(item, "usagePage");
            var usage = GetInt(item, "usage");
            list.Add(new NormalizedHidDevice
            {
                Key = BuildKey("hid", vid, pid, product, maker, serial, usagePage, usage),
                Vid = vid,
                Pid = pid,
                ProductName = product,
                Manufacturer = maker,
                Serial = serial,
                UsagePage = usagePage,
                Usage = usage,
                MaxInputReportLength = GetInt(item, "maxInputReportLength"),
                MaxOutputReportLength = GetInt(item, "maxOutputReportLength"),
                IdentityKind = GetString(item, "identityKind"),
            });
        }
        return list;
    }

    private static List<NormalizedCandidate> ParseInspectorCandidates(System.Text.Json.JsonElement root)
    {
        var list = new List<NormalizedCandidate>();
        if (!root.TryGetProperty("matchedYmmKeyboardCandidates", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            var vid = GetInt(item, "vid");
            var pid = GetInt(item, "pid");
            var product = GetString(item, "productName");
            var maker = GetString(item, "manufacturer");
            var serial = GetString(item, "serial");
            var usagePage = GetNullableInt(item, "usagePage");
            var usage = GetNullableInt(item, "usage");
            var transport = GetString(item, "transportType");
            var comPort = GetString(item, "comPort");
            var key = NormalizedCandidate.BuildKey(transport, vid, pid, product, maker, serial, usagePage, usage, comPort);
            list.Add(new NormalizedCandidate
            {
                Key = key,
                TransportType = transport,
                Vid = vid,
                Pid = pid,
                ProductName = product,
                Manufacturer = maker,
                Serial = serial,
                ComPort = GetString(item, "comPort"),
                UsagePage = usagePage,
                Usage = usage,
                MatchScore = 0,
                MatchReasons = GetStringArray(item, "matchReasons"),
                RejectReasons = [],
                Selected = false,
                IdentityKind = GetString(item, "identityKind"),
                SourceKind = "inspector",
                ExpectedScore = ExpectedScore(GetString(item, "identityKind"), vid, pid, usagePage, usage),
            });
        }
        return list;
    }

    private static string BuildKey(string kind, int vid, int pid, string product, string maker, string serial, int usagePage, int usage)
    {
        return $"{kind}:{vid:X4}:{pid:X4}:{Normalize(product)}:{Normalize(maker)}:{Normalize(serial)}:{usagePage:X4}:{usage:X4}";
    }

    private static int ExpectedScore(string identityKind, int vid, int pid, int? usagePage, int? usage)
    {
        if (identityKind.Equals("formal", StringComparison.OrdinalIgnoreCase))
            return 10000;
        if (identityKind.Equals("temporary", StringComparison.OrdinalIgnoreCase))
            return 8000;
        if (identityKind.Equals("likely-ymm", StringComparison.OrdinalIgnoreCase))
            return 5000;
        if (identityKind.Equals("possible-ymm", StringComparison.OrdinalIgnoreCase))
            return 1500;
        if (vid == 0x2E8A && pid == 0x4020)
            return 10000;
        if (vid == 0x2E8A && pid == 0x101F)
            return 8000;
        if (usagePage == 0xFF00 && usage == 0x0001)
            return 7000;
        return 0;
    }

    private static List<string> ParseStringArray(System.Text.Json.JsonElement root, string name)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                list.Add(item.GetString() ?? string.Empty);
        }

        return list;
    }

    private static string GetString(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var parsed) ? parsed : 0;

    private static int? GetNullableInt(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var parsed) ? parsed : null;

    private static List<string> GetStringArray(System.Text.Json.JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;
        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);
        return list;
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

internal sealed class PluginReport
{
    public string SourceName { get; init; } = string.Empty;
    public List<NormalizedHidDevice> DetectedHidDevices { get; init; } = [];
    public List<string> DetectedComPorts { get; init; } = [];
    public List<ConnectionCandidateRecord> ConnectionCandidates { get; init; } = [];
    public ConnectionCandidateRecord? SelectedCandidate { get; init; }
    public List<ConnectionCandidateRecord> RejectedCandidates { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];

    public static PluginReport Parse(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var report = new PluginReport
        {
            SourceName = Path.GetFileName(path),
            DetectedHidDevices = ParseHidDevices(root),
            DetectedComPorts = GetStringArray(root, "detectedComPorts"),
            ConnectionCandidates = ParseCandidates(root),
            SelectedCandidate = ParseCandidate(root, "selectedCandidate"),
            RejectedCandidates = ParseCandidateArray(root, "rejectedCandidates"),
            Warnings = GetStringArray(root, "warnings"),
            Errors = GetStringArray(root, "errors"),
        };

        return report;
    }

    private static List<NormalizedHidDevice> ParseHidDevices(System.Text.Json.JsonElement root)
    {
        var list = new List<NormalizedHidDevice>();
        if (!root.TryGetProperty("detectedHidDevices", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            var vid = GetInt(item, "vid");
            var pid = GetInt(item, "pid");
            var product = GetString(item, "productName");
            var maker = GetString(item, "manufacturer");
            var serial = GetString(item, "serial");
            var usagePage = GetInt(item, "usagePage");
            var usage = GetInt(item, "usage");

            list.Add(new NormalizedHidDevice
            {
                Key = BuildKey("hid", vid, pid, product, maker, serial, usagePage, usage),
                Vid = vid,
                Pid = pid,
                ProductName = product,
                Manufacturer = maker,
                Serial = serial,
                UsagePage = usagePage,
                Usage = usage,
                MaxInputReportLength = GetInt(item, "maxInputReportLength"),
                MaxOutputReportLength = GetInt(item, "maxOutputReportLength"),
                IdentityKind = GetString(item, "identityKind"),
            });
        }

        return list;
    }

    private static List<ConnectionCandidateRecord> ParseCandidates(System.Text.Json.JsonElement root)
    {
        var list = new List<ConnectionCandidateRecord>();
        if (!root.TryGetProperty("connectionCandidates", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(ParseCandidate(item));
        return list;
    }

    private static ConnectionCandidateRecord? ParseCandidate(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var item) || item.ValueKind == System.Text.Json.JsonValueKind.Null)
            return null;
        return ParseCandidate(item);
    }

    private static List<ConnectionCandidateRecord> ParseCandidateArray(System.Text.Json.JsonElement root, string name)
    {
        var list = new List<ConnectionCandidateRecord>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(ParseCandidate(item));
        return list;
    }

    private static ConnectionCandidateRecord ParseCandidate(System.Text.Json.JsonElement item)
    {
        return new ConnectionCandidateRecord
        {
            TransportType = GetString(item, "transportType"),
            Vid = GetNullableInt(item, "vid"),
            Pid = GetNullableInt(item, "pid"),
            ProductName = GetString(item, "productName"),
            Manufacturer = GetString(item, "manufacturer"),
            Serial = GetString(item, "serial"),
            ComPort = GetString(item, "comPort"),
            UsagePage = GetNullableInt(item, "usagePage"),
            Usage = GetNullableInt(item, "usage"),
            MatchScore = GetInt(item, "matchScore"),
            MatchReasons = GetStringArray(item, "matchReasons"),
            RejectReasons = GetStringArray(item, "rejectReasons"),
            Selected = GetBool(item, "selected"),
        };
    }

    private static string BuildKey(string kind, int vid, int pid, string product, string maker, string serial, int usagePage, int usage)
    {
        return $"{kind}:{vid:X4}:{pid:X4}:{Normalize(product)}:{Normalize(maker)}:{Normalize(serial)}:{usagePage:X4}:{usage:X4}";
    }

    private static string GetString(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var parsed) ? parsed : 0;

    private static int? GetNullableInt(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var parsed) ? parsed : null;

    private static bool GetBool(System.Text.Json.JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True;

    private static List<string> GetStringArray(System.Text.Json.JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;
        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);
        return list;
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
