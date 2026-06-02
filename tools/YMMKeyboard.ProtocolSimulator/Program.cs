using System.Text;
using System.Text.Json;

var options = ProtocolSimulatorOptions.Parse(args);
var simulator = new ProtocolSimulator(options);
var report = simulator.Run();
var writer = new SimulationReportWriter(options);
writer.Write(report);

Console.WriteLine($"ProtocolSimulator completed. output={writer.OutputPath}");
Console.WriteLine($"replayedCandidates={report.ReplayedCandidates.Count}, selected={report.SimulatedSelectedCandidate?.Key ?? "none"}, issues={report.Issues.Count}");

internal sealed class ProtocolSimulatorOptions
{
    public string InspectorPath { get; init; } = string.Empty;
    public string PluginPath { get; init; } = string.Empty;
    public string? OutputPath { get; init; }
    public string Format { get; init; } = "markdown";

    public static ProtocolSimulatorOptions Parse(string[] args)
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

        return new ProtocolSimulatorOptions
        {
            InspectorPath = inspector!,
            PluginPath = plugin!,
            OutputPath = output,
            Format = format,
        };
    }
}

internal sealed class ProtocolSimulator
{
    private readonly ProtocolSimulatorOptions options;

    public ProtocolSimulator(ProtocolSimulatorOptions options)
    {
        this.options = options;
    }

    public ProtocolSimulationReport Run()
    {
        var inspector = InspectorDiagnosticsReport.Parse(options.InspectorPath);
        var plugin = PluginDiagnosticsReport.Parse(options.PluginPath);

        var candidates = plugin.ConnectionCandidates
            .Select(candidate => SimulatedCandidate.Replay(candidate, plugin.ConfiguredDeviceIdentity, inspector))
            .OrderByDescending(candidate => candidate.SimulatedSelected)
            .ThenByDescending(candidate => candidate.SimulatedMatchScore)
            .ThenBy(candidate => candidate.TransportType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var simulatedSelected = candidates
            .Where(candidate => candidate.SimulatedSelected)
            .OrderByDescending(candidate => candidate.SimulatedMatchScore)
            .FirstOrDefault();

        if (simulatedSelected is null && candidates.Count > 0)
            simulatedSelected = candidates.OrderByDescending(candidate => candidate.SimulatedMatchScore).First();

        var issues = new List<SimulationIssue>();
        issues.AddRange(CompareScores(plugin.ConnectionCandidates, candidates));
        issues.AddRange(CompareSelection(plugin.SelectedCandidate, simulatedSelected));
        issues.AddRange(BuildInventoryWarnings(inspector));

        return new ProtocolSimulationReport
        {
            GeneratedAt = DateTimeOffset.Now,
            InspectorSource = Path.GetFileName(options.InspectorPath),
            PluginSource = Path.GetFileName(options.PluginPath),
            ReadOnlyMode = true,
            VirtualHidDevices = inspector.HidDevices,
            VirtualComPorts = inspector.ComPorts,
            ReplayedCandidates = candidates,
            ReportedSelectedCandidate = plugin.SelectedCandidate is null ? null : SimulatedCandidate.FromReported(plugin.SelectedCandidate),
            SimulatedSelectedCandidate = simulatedSelected,
            Issues = issues,
        };
    }

    private static IEnumerable<SimulationIssue> CompareScores(
        IReadOnlyList<ConnectionCandidateSnapshot> reported,
        IReadOnlyList<SimulatedCandidate> replayed)
    {
        var replayedByKey = replayed.ToDictionary(candidate => candidate.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in reported)
        {
            var key = ConnectionCandidateSnapshot.BuildKey(candidate.TransportType, candidate.Vid, candidate.Pid, candidate.ProductName, candidate.Manufacturer, candidate.Serial, candidate.ComPort, candidate.UsagePage, candidate.Usage);
            if (!replayedByKey.TryGetValue(key, out var simulated))
                continue;

            if (candidate.MatchScore != simulated.SimulatedMatchScore)
            {
                yield return SimulationIssue.Create(
                    "ScoreDrift",
                    $"Match score changed during replay. reported={candidate.MatchScore}, simulated={simulated.SimulatedMatchScore}",
                    key,
                    candidate.TransportType,
                    $"reported={candidate.MatchScore}; simulated={simulated.SimulatedMatchScore}");
            }
        }
    }

    private static IEnumerable<SimulationIssue> CompareSelection(ConnectionCandidateSnapshot? reported, SimulatedCandidate? simulated)
    {
        if (reported is null && simulated is null)
            yield break;

        var reportedKey = reported is null
            ? "none"
            : ConnectionCandidateSnapshot.BuildKey(reported.TransportType, reported.Vid, reported.Pid, reported.ProductName, reported.Manufacturer, reported.Serial, reported.ComPort, reported.UsagePage, reported.Usage);

        var simulatedKey = simulated?.Key ?? "none";
        if (!string.Equals(reportedKey, simulatedKey, StringComparison.OrdinalIgnoreCase))
        {
            yield return SimulationIssue.Create(
                "SelectionDrift",
                "Selected candidate changed during replay.",
                simulatedKey,
                simulated?.TransportType ?? string.Empty,
                $"reported={reportedKey}; simulated={simulatedKey}");
        }
    }

    private static IEnumerable<SimulationIssue> BuildInventoryWarnings(InspectorDiagnosticsReport inspector)
    {
        if (inspector.HidDevices.Count == 0)
            yield return SimulationIssue.Create("InventoryWarning", "No virtual HID devices were loaded.", "", "HID", "");

        if (inspector.ComPorts.Count == 0)
            yield return SimulationIssue.Create("InventoryWarning", "No virtual COM ports were loaded.", "", "COM", "");
    }
}

internal sealed class SimulationReportWriter
{
    private readonly ProtocolSimulatorOptions options;
    public string OutputPath { get; private set; } = string.Empty;

    public SimulationReportWriter(ProtocolSimulatorOptions options)
    {
        this.options = options;
    }

    public void Write(ProtocolSimulationReport report)
    {
        var output = ResolveOutputPath();
        var directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        OutputPath = output;
        switch (options.Format)
        {
            case "json":
                File.WriteAllText(output, SimulationFormatters.ToJson(report), Encoding.UTF8);
                break;
            case "text":
                File.WriteAllText(output, SimulationFormatters.ToText(report), Encoding.UTF8);
                break;
            default:
                File.WriteAllText(output, SimulationFormatters.ToMarkdown(report), Encoding.UTF8);
                break;
        }
    }

    private string ResolveOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
            return options.OutputPath!;

        var dir = Path.Combine(Environment.CurrentDirectory, "tmp", "protocol-simulator");
        var ext = options.Format switch
        {
            "json" => ".json",
            "text" => ".txt",
            _ => ".md",
        };

        return Path.Combine(dir, $"report{ext}");
    }
}

internal static class SimulationFormatters
{
    public static string ToMarkdown(ProtocolSimulationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Protocol Simulation Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"- inspectorSource: {report.InspectorSource}");
        sb.AppendLine($"- pluginSource: {report.PluginSource}");
        sb.AppendLine($"- readOnly: {report.ReadOnlyMode}");
        sb.AppendLine($"- virtualHidDevices: {report.VirtualHidDevices.Count}");
        sb.AppendLine($"- virtualComPorts: {report.VirtualComPorts.Count}");
        sb.AppendLine($"- replayedCandidates: {report.ReplayedCandidates.Count}");
        sb.AppendLine($"- selectedCandidate: {report.SimulatedSelectedCandidate?.Key ?? "none"}");
        sb.AppendLine($"- issues: {report.Issues.Count}");
        sb.AppendLine();

        AppendDevices(sb, "Virtual HID Devices", report.VirtualHidDevices.Select(device => device.ToString()));
        AppendDevices(sb, "Virtual COM Ports", report.VirtualComPorts);
        AppendDevices(sb, "Replayed Candidates", report.ReplayedCandidates.Select(candidate => candidate.ToString()));

        if (report.ReportedSelectedCandidate is not null)
        {
            sb.AppendLine("## Reported Selected Candidate");
            sb.AppendLine(report.ReportedSelectedCandidate.ToString());
            sb.AppendLine();
        }

        if (report.SimulatedSelectedCandidate is not null)
        {
            sb.AppendLine("## Simulated Selected Candidate");
            sb.AppendLine(report.SimulatedSelectedCandidate.ToString());
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
                sb.AppendLine($"- [{issue.Category}] {issue.Message} | key={issue.Key} | transport={issue.TransportType} | detail={issue.Detail}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string ToText(ProtocolSimulationReport report) => ToMarkdown(report);

    public static string ToJson(ProtocolSimulationReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return JsonSerializer.Serialize(report, options);
    }

    private static void AppendDevices(StringBuilder sb, string title, IEnumerable<string> lines)
    {
        sb.AppendLine($"## {title}");
        var any = false;
        foreach (var line in lines)
        {
            any = true;
            sb.AppendLine($"- {line}");
        }

        if (!any)
            sb.AppendLine("- none");

        sb.AppendLine();
    }
}

internal sealed class ProtocolSimulationReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string InspectorSource { get; init; } = string.Empty;
    public string PluginSource { get; init; } = string.Empty;
    public bool ReadOnlyMode { get; init; }
    public List<VirtualHidDevice> VirtualHidDevices { get; init; } = [];
    public List<string> VirtualComPorts { get; init; } = [];
    public List<SimulatedCandidate> ReplayedCandidates { get; init; } = [];
    public SimulatedCandidate? ReportedSelectedCandidate { get; init; }
    public SimulatedCandidate? SimulatedSelectedCandidate { get; init; }
    public List<SimulationIssue> Issues { get; init; } = [];
}

internal sealed class SimulationIssue
{
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string TransportType { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    public static SimulationIssue Create(string category, string message, string key, string transportType, string detail)
    {
        return new SimulationIssue
        {
            Category = category,
            Message = message,
            Key = key,
            TransportType = transportType,
            Detail = detail,
        };
    }
}

internal sealed class VirtualHidDevice
{
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string DevicePath { get; init; } = string.Empty;
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public string IdentityKind { get; init; } = string.Empty;

    public override string ToString()
        => $"HID {VendorId:X4}:{ProductId:X4} product={ProductName} maker={Manufacturer} serial={SerialNumber} usage={UsagePage:X4}:{Usage:X4} kind={IdentityKind}";
}

internal sealed class InspectorDiagnosticsReport
{
    public List<VirtualHidDevice> HidDevices { get; init; } = [];
    public List<string> ComPorts { get; init; } = [];

    public static InspectorDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return new InspectorDiagnosticsReport
        {
            HidDevices = ParseHidDevices(root),
            ComPorts = ParseStringArray(root, "comPorts"),
        };
    }

    private static List<VirtualHidDevice> ParseHidDevices(JsonElement root)
    {
        var list = new List<VirtualHidDevice>();
        if (!root.TryGetProperty("hidDevices", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            list.Add(new VirtualHidDevice
            {
                VendorId = GetInt(item, "vendorId"),
                ProductId = GetInt(item, "productId"),
                ProductName = GetString(item, "productName"),
                Manufacturer = GetString(item, "manufacturer"),
                SerialNumber = GetString(item, "serialNumber"),
                DevicePath = GetString(item, "devicePath"),
                MaxInputReportLength = GetInt(item, "maxInputReportLength"),
                MaxOutputReportLength = GetInt(item, "maxOutputReportLength"),
                UsagePage = GetInt(item, "usagePage"),
                Usage = GetInt(item, "usage"),
                IdentityKind = GetString(item, "identityKind"),
            });
        }

        return list;
    }

    private static List<string> ParseStringArray(JsonElement root, string name)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                list.Add(item.GetString() ?? string.Empty);
        }

        return list;
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}

internal sealed class PluginDiagnosticsReport
{
    public ConfiguredDeviceIdentity ConfiguredDeviceIdentity { get; init; } = new();
    public List<ConnectionCandidateSnapshot> ConnectionCandidates { get; init; } = [];
    public ConnectionCandidateSnapshot? SelectedCandidate { get; init; }

    public static PluginDiagnosticsReport Parse(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return new PluginDiagnosticsReport
        {
            ConfiguredDeviceIdentity = ParseConfiguredDeviceIdentity(root),
            ConnectionCandidates = ParseCandidates(root),
            SelectedCandidate = ParseCandidate(root, "selectedCandidate"),
        };
    }

    private static ConfiguredDeviceIdentity ParseConfiguredDeviceIdentity(JsonElement root)
    {
        if (!root.TryGetProperty("configuredDeviceIdentity", out var obj) || obj.ValueKind != JsonValueKind.Object)
            return new ConfiguredDeviceIdentity();

        return new ConfiguredDeviceIdentity
        {
            ConnectionMode = GetString(obj, "connectionMode"),
            HidVendorId = GetNullableString(obj, "hidVendorId"),
            HidProductId = GetNullableString(obj, "hidProductId"),
            HidProductNameFilter = GetString(obj, "hidProductNameFilter"),
            HidManufacturerFilter = GetString(obj, "hidManufacturerFilter"),
            PortName = GetString(obj, "portName"),
            StartupPortNames = ParseNestedStringArray(obj, "startupPortNames"),
        };
    }

    private static List<ConnectionCandidateSnapshot> ParseCandidates(JsonElement root)
    {
        var list = new List<ConnectionCandidateSnapshot>();
        if (!root.TryGetProperty("connectionCandidates", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(ParseCandidate(item));

        return list;
    }

    private static ConnectionCandidateSnapshot? ParseCandidate(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null)
            return null;

        return ParseCandidate(item);
    }

    private static ConnectionCandidateSnapshot ParseCandidate(JsonElement item)
    {
        return new ConnectionCandidateSnapshot
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
            MatchReasons = ParseNestedStringArray(item, "matchReasons"),
            RejectReasons = ParseNestedStringArray(item, "rejectReasons"),
            Selected = GetBool(item, "selected"),
        };
    }

    private static List<string> ParseNestedStringArray(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);

        return list;
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static int GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static int? GetNullableInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static bool GetBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

internal sealed class ConfiguredDeviceIdentity
{
    public string ConnectionMode { get; init; } = string.Empty;
    public string? HidVendorId { get; init; }
    public string? HidProductId { get; init; }
    public string HidProductNameFilter { get; init; } = string.Empty;
    public string HidManufacturerFilter { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public List<string> StartupPortNames { get; init; } = [];
}

internal sealed class ConnectionCandidateSnapshot
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

    public static string BuildKey(string transportType, int? vid, int? pid, string product, string maker, string serial, string comPort, int? usagePage, int? usage)
    {
        if (transportType.Equals("COM", StringComparison.OrdinalIgnoreCase) || transportType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            return $"serial:{Normalize(comPort)}";

        return $"{transportType}:{vid?.ToString("X4") ?? "0000"}:{pid?.ToString("X4") ?? "0000"}:{Normalize(product)}:{Normalize(maker)}:{Normalize(serial)}:{usagePage?.ToString("X4") ?? "0000"}:{usage?.ToString("X4") ?? "0000"}";
    }

    public string Key => BuildKey(TransportType, Vid, Pid, ProductName, Manufacturer, Serial, ComPort, UsagePage, Usage);

    public override string ToString()
        => $"{TransportType} key={Key} score={MatchScore} selected={Selected} reasons={string.Join("|", MatchReasons)} rejects={string.Join("|", RejectReasons)}";

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

internal sealed class SimulatedCandidate
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
    public int ReportedMatchScore { get; init; }
    public int SimulatedMatchScore { get; set; }
    public List<string> MatchReasons { get; set; } = [];
    public List<string> RejectReasons { get; set; } = [];
    public bool ReportedSelected { get; init; }
    public bool SimulatedSelected { get; set; }

    public static SimulatedCandidate Replay(
        ConnectionCandidateSnapshot reported,
        ConfiguredDeviceIdentity settings,
        InspectorDiagnosticsReport inspector)
    {
        var simulated = new SimulatedCandidate
        {
            Key = reported.Key,
            TransportType = reported.TransportType,
            Vid = reported.Vid,
            Pid = reported.Pid,
            ProductName = reported.ProductName,
            Manufacturer = reported.Manufacturer,
            Serial = reported.Serial,
            ComPort = reported.ComPort,
            UsagePage = reported.UsagePage,
            Usage = reported.Usage,
            ReportedMatchScore = reported.MatchScore,
            ReportedSelected = reported.Selected,
        };

        var matchReasons = new List<string>();
        var rejectReasons = new List<string>();
        var score = 0;

        if (reported.TransportType.Equals("HID", StringComparison.OrdinalIgnoreCase))
        {
            if (settings.ConnectionMode.Equals("Hid", StringComparison.OrdinalIgnoreCase))
                matchReasons.Add("connectionMode=HID");

            if (TryParseHex(settings.HidVendorId) is int explicitVid)
            {
                if (reported.Vid == explicitVid)
                {
                    score += 3000;
                    matchReasons.Add($"vid={explicitVid:X4}");
                }
                else
                {
                    rejectReasons.Add($"vidMismatch expected={explicitVid:X4} actual={reported.Vid?.ToString("X4") ?? "0000"}");
                }
            }

            if (TryParseHex(settings.HidProductId) is int explicitPid)
            {
                if (reported.Pid == explicitPid)
                {
                    score += 3000;
                    matchReasons.Add($"pid={explicitPid:X4}");
                }
                else
                {
                    rejectReasons.Add($"pidMismatch expected={explicitPid:X4} actual={reported.Pid?.ToString("X4") ?? "0000"}");
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.HidProductNameFilter))
            {
                if (reported.ProductName.Contains(settings.HidProductNameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1200;
                    matchReasons.Add($"productName~{settings.HidProductNameFilter}");
                }
                else
                {
                    rejectReasons.Add($"productNameMismatch filter={settings.HidProductNameFilter}");
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.HidManufacturerFilter))
            {
                if (reported.Manufacturer.Contains(settings.HidManufacturerFilter, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1200;
                    matchReasons.Add($"manufacturer~{settings.HidManufacturerFilter}");
                }
                else
                {
                    rejectReasons.Add($"manufacturerMismatch filter={settings.HidManufacturerFilter}");
                }
            }

            if (string.IsNullOrWhiteSpace(settings.HidVendorId)
                && string.IsNullOrWhiteSpace(settings.HidProductId)
                && string.IsNullOrWhiteSpace(settings.HidProductNameFilter)
                && string.IsNullOrWhiteSpace(settings.HidManufacturerFilter))
            {
                if (IsLikelyYmmHid(reported, inspector))
                {
                    score += 2500;
                    matchReasons.Add("implicitYmmHeuristic");
                }
                else
                {
                    rejectReasons.Add("implicitYmmHeuristicMismatch");
                }
            }

            if (reported.UsagePage == 0xFF00 && reported.Usage == 0x0001)
            {
                score += 5000;
                matchReasons.Add("usagePage=FF00 usage=0001");
            }

            if (TryFindHidDevice(reported, inspector) is { } hidDevice)
            {
                if (hidDevice.MaxInputReportLength > 0)
                    score += hidDevice.MaxInputReportLength;
                if (hidDevice.MaxOutputReportLength > 0)
                    score += 100;
            }

            if (reported.Selected && rejectReasons.Count == 0)
                score = Math.Max(score, 10000);
        }
        else if (reported.TransportType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
        {
            if (settings.ConnectionMode.Equals("Com", StringComparison.OrdinalIgnoreCase))
                matchReasons.Add("connectionMode=COM");

            if (settings.PortName.Equals(reported.ComPort, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
                matchReasons.Add("configuredPort");
            }
            else if (settings.StartupPortNames.Contains(reported.ComPort, StringComparer.OrdinalIgnoreCase))
            {
                score += 1000;
                matchReasons.Add("startupPort");
            }
            else if (!string.IsNullOrWhiteSpace(reported.ComPort))
            {
                score += 100;
                matchReasons.Add("detectedComPort");
            }

            if (!settings.ConnectionMode.Equals("Com", StringComparison.OrdinalIgnoreCase))
                rejectReasons.Add("mode=HID");
        }
        else
        {
            rejectReasons.Add("unsupportedTransport");
        }

        simulated.MatchReasons = matchReasons;
        simulated.RejectReasons = rejectReasons;
        simulated.SimulatedMatchScore = score;
        simulated.SimulatedSelected = reported.TransportType.Equals("HID", StringComparison.OrdinalIgnoreCase)
            ? settings.ConnectionMode.Equals("Hid", StringComparison.OrdinalIgnoreCase) && rejectReasons.Count == 0
            : reported.TransportType.Equals("Serial", StringComparison.OrdinalIgnoreCase)
                ? settings.ConnectionMode.Equals("Com", StringComparison.OrdinalIgnoreCase) && rejectReasons.Count == 0
                : false;

        return simulated;
    }

    public static SimulatedCandidate FromReported(ConnectionCandidateSnapshot candidate)
    {
        return new SimulatedCandidate
        {
            Key = candidate.Key,
            TransportType = candidate.TransportType,
            Vid = candidate.Vid,
            Pid = candidate.Pid,
            ProductName = candidate.ProductName,
            Manufacturer = candidate.Manufacturer,
            Serial = candidate.Serial,
            ComPort = candidate.ComPort,
            UsagePage = candidate.UsagePage,
            Usage = candidate.Usage,
            ReportedMatchScore = candidate.MatchScore,
            SimulatedMatchScore = candidate.MatchScore,
            MatchReasons = candidate.MatchReasons,
            RejectReasons = candidate.RejectReasons,
            ReportedSelected = candidate.Selected,
            SimulatedSelected = candidate.Selected,
        };
    }

    public override string ToString()
        => $"{TransportType} key={Key} reported={ReportedMatchScore} simulated={SimulatedMatchScore} selected={SimulatedSelected} reasons={string.Join("|", MatchReasons)} rejects={string.Join("|", RejectReasons)}";

    private static bool IsLikelyYmmHid(ConnectionCandidateSnapshot candidate, InspectorDiagnosticsReport inspector)
    {
        if (candidate.Vid == 0x2E8A)
            return true;

        return inspector.HidDevices.Any(device =>
            device.VendorId == candidate.Vid
            && device.ProductId == candidate.Pid
            && device.IdentityKind is "formal" or "temporary" or "likely-ymm");
    }

    private static VirtualHidDevice? TryFindHidDevice(ConnectionCandidateSnapshot candidate, InspectorDiagnosticsReport inspector)
    {
        return inspector.HidDevices.FirstOrDefault(device =>
            device.VendorId == candidate.Vid
            && device.ProductId == candidate.Pid
            && string.Equals(device.ProductName, candidate.ProductName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(device.Manufacturer, candidate.Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(device.SerialNumber, candidate.Serial, StringComparison.OrdinalIgnoreCase)
            && device.UsagePage == candidate.UsagePage
            && device.Usage == candidate.Usage);
    }

    private static int? TryParseHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var parsed)
            ? parsed
            : null;
    }
}
