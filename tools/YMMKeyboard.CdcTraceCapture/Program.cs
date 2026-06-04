using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;

var options = CdcTraceCaptureOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);

var logPath = Path.Combine(options.OutputDirectory, "cdc-trace.log");
var jsonPath = Path.Combine(options.OutputDirectory, "cdc-trace.json");

using var logWriter = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };

void Write(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

var runner = new CdcTraceCaptureRunner(options, Write);
var report = await runner.RunAsync();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
};
File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));

Write($"LOG={logPath}");
Write($"JSON={jsonPath}");
Environment.ExitCode = report.ExitCode;

sealed class CdcTraceCaptureRunner
{
    private static readonly Regex ComPattern = new(@"\((COM\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VidPidPattern = new(@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SerialPattern = new(@"\\([^\\]+)$", RegexOptions.Compiled);
    private static readonly Regex FirmwareInfoPattern = new(
        @"FW_INFO\s+FW_ID=(?<id>\S+)\s+FW_VERSION=(?<version>\S+)\s+FW_BUILD_TIME=(?<build>.+?)\s+FW_FEATURES=(?<features>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly CdcTraceCaptureOptions options;
    private readonly Action<string> write;

    public CdcTraceCaptureRunner(CdcTraceCaptureOptions options, Action<string> write)
    {
        this.options = options;
        this.write = write;
    }

    public async Task<CdcTraceCaptureReport> RunAsync()
    {
        var report = new CdcTraceCaptureReport
        {
            GeneratedAt = DateTimeOffset.Now,
            MachineName = Environment.MachineName,
            OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            AppVersion = GetAppVersion(),
            Options = options,
        };

        write("CdcTraceCapture START");
        write($"Options: port={options.PortName ?? "(auto)"}, vid={options.VidHex}, pid={options.PidHex}, durationSec={options.DurationSec}, timeoutMs={options.TimeoutMs}");

        var ports = EnumeratePorts();
        report.Ports = ports;

        write($"Enumerated COM ports: {ports.Count}");
        foreach (var port in ports)
            write($"PORT[{port.Index}] {port.ToLine()}");

        var candidates = ports
            .Where(port => options.Matches(port))
            .OrderByDescending(port => port.VendorId == options.Vid && port.ProductId == options.Pid)
            .ThenBy(port => port.PortName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        report.FilteredPortCount = candidates.Count;
        write($"Filtered candidates: {candidates.Count}");
        foreach (var port in candidates)
            write($"CANDIDATE[{port.Index}] {port.ToLine()}");

        if (candidates.Count == 0)
        {
            report.ExitCode = 1;
            report.Conclusion = "no_matching_port";
            write("No matching COM port found. EXIT");
            return report;
        }

        CdcPortSnapshot? selected = null;
        if (!string.IsNullOrWhiteSpace(options.PortName))
        {
            selected = candidates.FirstOrDefault(p => string.Equals(p.PortName, options.PortName, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                report.ExitCode = 1;
                report.Conclusion = "requested_port_not_found";
                write($"Requested port {options.PortName} not found among candidates. EXIT");
                return report;
            }
        }
        else
        {
            selected = candidates.First();
        }

        report.SelectedPort = selected;
        report.SelectedPortName = selected.PortName;
        write($"SELECTED {selected.ToLine()}");

        using var durationCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSec));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            durationCts.Cancel();
        };

        var runtime = new CdcTraceCaptureRuntime
        {
            SelectedPortName = selected.PortName,
            SelectedPnpDeviceId = selected.PnpDeviceId,
            SelectedVid = selected.VendorId,
            SelectedPid = selected.ProductId,
        };

        if (!await TryOpenAndReadAsync(selected, durationCts.Token, runtime, report))
        {
            report.ExitCode = 1;
            report.Conclusion = "open_failed_or_no_data";
            report.Runtime = runtime;
            return report;
        }

        report.Runtime = runtime;
        report.ExitCode = 0;
        report.Conclusion = runtime.LineCount > 0 ? "data_received" : "no_data_received";
        write("CdcTraceCapture END");
        return report;
    }

    private async Task<bool> TryOpenAndReadAsync(
        CdcPortSnapshot portSnapshot,
        CancellationToken token,
        CdcTraceCaptureRuntime runtime,
        CdcTraceCaptureReport report)
    {
        try
        {
            using var port = new SerialPort(portSnapshot.PortName, 115200)
            {
                Encoding = Encoding.ASCII,
                NewLine = "\n",
                ReadTimeout = options.TimeoutMs,
                WriteTimeout = options.TimeoutMs,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
            };

            port.Open();
            runtime.OpenSucceeded = true;
            runtime.ReadLoopStarted = true;
            runtime.OpenedPortName = portSnapshot.PortName;
            runtime.OpenedDescription = portSnapshot.Description;
            runtime.OpenedManufacturer = portSnapshot.Manufacturer;
            runtime.OpenedPnpDeviceId = portSnapshot.PnpDeviceId;
            runtime.OpenedVid = portSnapshot.VendorId;
            runtime.OpenedPid = portSnapshot.ProductId;
            runtime.OpenedSerialEstimate = portSnapshot.SerialEstimate;

            write($"OPEN_OK port={portSnapshot.PortName}");
            write($"OPEN_INFO {portSnapshot.ToLine()}");

            port.DtrEnable = false;
            await Task.Delay(30, token);
            port.DtrEnable = true;
            await Task.Delay(120, token);
            port.DiscardInBuffer();

            var stopwatch = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(options.DurationSec))
                    break;

                runtime.ReadAttemptCount++;

                string line;
                try
                {
                    line = port.ReadLine();
                }
                catch (TimeoutException)
                {
                    runtime.ReadTimeoutCount++;
                    runtime.LastException = "TimeoutException";
                    continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    runtime.LastException = $"{ex.GetType().Name}: {ex.Message}";
                    write($"READ_FAIL port={portSnapshot.PortName} error={runtime.LastException}");
                    break;
                }

                var normalized = NormalizeLine(line);
                if (normalized.Length == 0)
                    continue;

                runtime.LineCount++;
                runtime.LastLineAtUtc = DateTimeOffset.UtcNow;
                runtime.FirstLineAtUtc ??= runtime.LastLineAtUtc;
                MaybeCaptureFirmwareInfo(normalized, report, runtime);
                report.Lines.Add(new CdcTraceCaptureLine
                {
                    Index = runtime.LineCount,
                    Timestamp = DateTimeOffset.Now,
                    Text = normalized,
                    Keywords = DetectKeywords(normalized),
                });

                write($"LINE[{runtime.LineCount}] {normalized}");
                var keywords = DetectKeywords(normalized);
                foreach (var keyword in keywords)
                {
                    runtime.KeywordCounts.TryGetValue(keyword, out var count);
                    runtime.KeywordCounts[keyword] = count + 1;
                }
            }

            write($"READ_SUMMARY openSucceeded={runtime.OpenSucceeded} readLoopStarted={runtime.ReadLoopStarted} lineCount={runtime.LineCount} readAttemptCount={runtime.ReadAttemptCount} readTimeoutCount={runtime.ReadTimeoutCount} lastException={runtime.LastException}");
            return true;
        }
        catch (Exception ex)
        {
            runtime.LastException = $"{ex.GetType().Name}: {ex.Message}";
            write($"RUN_FAIL error={runtime.LastException}");
            return false;
        }
    }

    private static List<CdcPortSnapshot> EnumeratePorts()
    {
        var result = new List<CdcPortSnapshot>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Description, Manufacturer, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = Safe(() => obj["Name"]?.ToString());
                var description = Safe(() => obj["Description"]?.ToString());
                var manufacturer = Safe(() => obj["Manufacturer"]?.ToString());
                var pnp = Safe(() => obj["PNPDeviceID"]?.ToString());

                var match = ComPattern.Match(name);
                if (!match.Success)
                    continue;

                var portName = match.Groups[1].Value.ToUpperInvariant();
                var (vid, pid) = ParseVidPid(pnp);
                var serial = ParseSerialEstimate(pnp);

                result.Add(new CdcPortSnapshot
                {
                    Index = result.Count,
                    PortName = portName,
                    Description = description,
                    Manufacturer = manufacturer,
                    PnpDeviceId = pnp,
                    VendorId = vid,
                    ProductId = pid,
                    SerialEstimate = serial,
                });
            }
        }
        catch
        {
            // Fall back to no entries if WMI is unavailable.
        }

        return result
            .OrderByDescending(p => p.VendorId == 0x2E8A && p.ProductId == 0x4020)
            .ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
            .Select((p, index) => p with { Index = index })
            .ToList();
    }

    private static (int Vid, int Pid) ParseVidPid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0);

        var match = VidPidPattern.Match(value);
        if (!match.Success)
            return (0, 0);

        return (ParseHex(match.Groups[1].Value), ParseHex(match.Groups[2].Value));
    }

    private static string ParseSerialEstimate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var match = SerialPattern.Match(value);
        if (match.Success)
            return match.Groups[1].Value;

        return string.Empty;
    }

    private static int ParseHex(string value)
        => int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string NormalizeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var sb = new StringBuilder(line.Length);
        foreach (var ch in line)
        {
            if (!char.IsControl(ch) || ch == '\t')
                sb.Append(ch);
        }

        return sb.ToString().Trim();
    }

    private static List<string> DetectKeywords(string line)
    {
        var keywords = new List<string>();
        if (line.Contains("FW_INFO", StringComparison.OrdinalIgnoreCase))
            keywords.Add("FW_INFO");
        if (line.Contains("FW_ID", StringComparison.OrdinalIgnoreCase))
            keywords.Add("FW_ID");
        if (line.Contains("FW_VERSION", StringComparison.OrdinalIgnoreCase))
            keywords.Add("FW_VERSION");
        if (line.Contains("FW_FEATURES", StringComparison.OrdinalIgnoreCase))
            keywords.Add("FW_FEATURES");
        if (line.Contains("HID_STATUS", StringComparison.OrdinalIgnoreCase))
            keywords.Add("HID_STATUS");
        if (line.Contains("HID_TEST", StringComparison.OrdinalIgnoreCase))
            keywords.Add("HID_TEST");
        if (line.Contains("HID_DIAG", StringComparison.OrdinalIgnoreCase))
            keywords.Add("HID_DIAG");
        if (line.Contains("HB:", StringComparison.OrdinalIgnoreCase))
            keywords.Add("HB:");
        if (line.Contains("P/R:", StringComparison.OrdinalIgnoreCase))
            keywords.Add("P/R:");
        if (line.Contains("SW_", StringComparison.OrdinalIgnoreCase))
            keywords.Add("SW_");
        return keywords;
    }

    private static void MaybeCaptureFirmwareInfo(string line, CdcTraceCaptureReport report, CdcTraceCaptureRuntime runtime)
    {
        if (!line.Contains("FW_INFO", StringComparison.OrdinalIgnoreCase))
            return;

        var match = FirmwareInfoPattern.Match(line);
        runtime.FirmwareInfoDetected = true;
        runtime.FirmwareInfoCount++;

        if (!match.Success)
        {
            runtime.LastFirmwareInfoLine = line;
            return;
        }

        runtime.FirmwareId = match.Groups["id"].Value;
        runtime.FirmwareVersion = match.Groups["version"].Value;
        runtime.FirmwareBuildTime = match.Groups["build"].Value;
        runtime.FirmwareFeatures = match.Groups["features"].Value;
        runtime.LastFirmwareInfoLine = line;

        report.FirmwareInfoDetected = true;
        report.FirmwareId = runtime.FirmwareId;
        report.FirmwareVersion = runtime.FirmwareVersion;
        report.FirmwareBuildTime = runtime.FirmwareBuildTime;
        report.FirmwareFeatures = runtime.FirmwareFeatures;
    }

    private static string GetAppVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string Safe(Func<string?> getter)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

sealed record CdcPortSnapshot
{
    public int Index { get; init; }
    public string PortName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string PnpDeviceId { get; init; } = string.Empty;
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string SerialEstimate { get; init; } = string.Empty;

    public string ToLine()
        => $"port={PortName} description={Description} manufacturer={Manufacturer} pnpDeviceId={PnpDeviceId} vid={VendorId:X4} pid={ProductId:X4} serial={SerialEstimate}";
}

sealed class CdcTraceCaptureOptions
{
    public string? PortName { get; private init; }
    public string? VidHex { get; private init; }
    public string? PidHex { get; private init; }
    public int Vid { get; private init; }
    public int Pid { get; private init; }
    public int DurationSec { get; private init; } = 30;
    public int TimeoutMs { get; private init; } = 500;
    public string OutputDirectory { get; private init; } = Path.Combine(Environment.CurrentDirectory, "tmp", "cdc-trace-capture");

    public bool Matches(CdcPortSnapshot port)
    {
        if (!string.IsNullOrWhiteSpace(PortName))
            return string.Equals(port.PortName, PortName, StringComparison.OrdinalIgnoreCase);

        return port.VendorId == Vid && port.ProductId == Pid;
    }

    public static CdcTraceCaptureOptions Parse(string[] args)
    {
        string? portName = null;
        string? vidHex = null;
        string? pidHex = null;
        var durationSec = 30;
        var timeoutMs = 500;
        string? outputDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--port":
                    portName = next();
                    break;
                case "--vid":
                    vidHex = next();
                    break;
                case "--pid":
                    pidHex = next();
                    break;
                case "--duration-sec":
                    durationSec = int.TryParse(next(), out var ds) ? Math.Max(1, ds) : 30;
                    break;
                case "--timeout-ms":
                    timeoutMs = int.TryParse(next(), out var tm) ? Math.Max(1, tm) : 500;
                    break;
                case "--output-dir":
                    outputDir = next();
                    break;
            }
        }

        return new CdcTraceCaptureOptions
        {
            PortName = portName,
            VidHex = vidHex,
            PidHex = pidHex,
            Vid = ParseHex16(vidHex),
            Pid = ParseHex16(pidHex),
            DurationSec = durationSec,
            TimeoutMs = timeoutMs,
            OutputDirectory = string.IsNullOrWhiteSpace(outputDir)
                ? Path.Combine(Environment.CurrentDirectory, "tmp", "cdc-trace-capture")
                : outputDir!,
        };
    }

    private static int ParseHex16(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];

        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

sealed class CdcTraceCaptureReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public CdcTraceCaptureOptions Options { get; set; } = new();
    public List<CdcPortSnapshot> Ports { get; set; } = [];
    public int FilteredPortCount { get; set; }
    public CdcPortSnapshot? SelectedPort { get; set; }
    public string? SelectedPortName { get; set; }
    public bool FirmwareInfoDetected { get; set; }
    public string? FirmwareId { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? FirmwareBuildTime { get; set; }
    public string? FirmwareFeatures { get; set; }
    public CdcTraceCaptureRuntime? Runtime { get; set; }
    public List<CdcTraceCaptureLine> Lines { get; set; } = [];
    public int ExitCode { get; set; }
    public string Conclusion { get; set; } = string.Empty;
}

sealed class CdcTraceCaptureRuntime
{
    public string SelectedPortName { get; set; } = string.Empty;
    public string SelectedPnpDeviceId { get; set; } = string.Empty;
    public int SelectedVid { get; set; }
    public int SelectedPid { get; set; }
    public bool OpenSucceeded { get; set; }
    public bool ReadLoopStarted { get; set; }
    public int ReadAttemptCount { get; set; }
    public int ReadTimeoutCount { get; set; }
    public int LineCount { get; set; }
    public int FirmwareInfoCount { get; set; }
    public DateTimeOffset? FirstLineAtUtc { get; set; }
    public DateTimeOffset? LastLineAtUtc { get; set; }
    public string? OpenedPortName { get; set; }
    public string? OpenedDescription { get; set; }
    public string? OpenedManufacturer { get; set; }
    public string? OpenedPnpDeviceId { get; set; }
    public int OpenedVid { get; set; }
    public int OpenedPid { get; set; }
    public string? OpenedSerialEstimate { get; set; }
    public string? LastException { get; set; }
    public bool FirmwareInfoDetected { get; set; }
    public string? FirmwareId { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? FirmwareBuildTime { get; set; }
    public string? FirmwareFeatures { get; set; }
    public string? LastFirmwareInfoLine { get; set; }
    public Dictionary<string, int> KeywordCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

sealed class CdcTraceCaptureLine
{
    public int Index { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
}
