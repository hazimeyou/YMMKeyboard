using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Management;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HidSharp;

var options = ComHidCorrelationProbeOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);

var outputBaseName = options.SnapshotOnly ? "snapshot" : "correlation";
var logPath = Path.Combine(options.OutputDirectory, $"{outputBaseName}.log");
var jsonPath = Path.Combine(options.OutputDirectory, $"{outputBaseName}.json");
var mdPath = Path.Combine(options.OutputDirectory, $"{outputBaseName}.md");

using var logWriter = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };

void Write(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

var runner = new ComHidCorrelationProbeRunner(options, Write);
var report = await runner.RunAsync();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
};
File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));
File.WriteAllText(mdPath, BuildMarkdown(report), new UTF8Encoding(false));

Write($"LOG={logPath}");
Write($"JSON={jsonPath}");
Write($"MD={mdPath}");
Environment.ExitCode = report.ExitCode;

static string BuildMarkdown(ComHidCorrelationProbeReport report)
{
    var sb = new StringBuilder();
    sb.AppendLine(report.Options.SnapshotOnly
        ? "# USB Interface Snapshot Result"
        : "# COM/HID Correlation Probe Result");
    sb.AppendLine();
    sb.AppendLine("## Current State");
    sb.AppendLine();
    sb.AppendLine($"- Conclusion: `{report.Conclusion}`");
    sb.AppendLine($"- COM selected: `{report.SelectedComPortName ?? "n/a"}`");
    sb.AppendLine($"- HID selected: `{report.SelectedHidPath ?? "n/a"}`");
    sb.AppendLine();
    sb.AppendLine("## Summary");
    sb.AppendLine();
    sb.AppendLine($"- `comLineCount`: `{report.ComLineCount}`");
    sb.AppendLine($"- `hidReportCount`: `{report.HidReportCount}`");
    sb.AppendLine($"- `testHidCount`: `{report.TestHidCount}`");
    sb.AppendLine($"- `swReportCount`: `{report.SwReportCount}`");
    sb.AppendLine($"- `otherReportCount`: `{report.OtherReportCount}`");
    sb.AppendLine($"- `swDiagCount`: `{report.SwDiagCount}`");
    sb.AppendLine($"- `swDiagSentTrueCount`: `{report.SwDiagSentTrueCount}`");
    sb.AppendLine($"- `swDiagSentFalseCount`: `{report.SwDiagSentFalseCount}`");
    sb.AppendLine($"- `correlatedSwCount`: `{report.CorrelatedSwCount}`");
    sb.AppendLine();
    if (report.Options.SnapshotOnly)
    {
        sb.AppendLine("## Interface Stability");
        sb.AppendLine();
        sb.AppendLine($"- `snapshotCount`: `{report.SnapshotCount}`");
        sb.AppendLine($"- `hidPresentCount`: `{report.HidPresentCount}`");
        sb.AppendLine($"- `comPresentCount`: `{report.ComPresentCount}`");
        sb.AppendLine($"- `bothPresentCount`: `{report.BothPresentCount}`");
        sb.AppendLine($"- `hidMissingCount`: `{report.HidMissingCount}`");
        sb.AppendLine($"- `comMissingCount`: `{report.ComMissingCount}`");
        sb.AppendLine();
        sb.AppendLine("## Snapshots");
        sb.AppendLine();
        if (report.Snapshots.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var snapshot in report.Snapshots)
            {
                sb.AppendLine($"- `{snapshot.Timestamp:HH:mm:ss}` COM=`{snapshot.ComCount}` HID=`{snapshot.HidCount}` both=`{(snapshot.BothPresent ? "yes" : "no")}`");
            }
        }
    }
    else
    {
        sb.AppendLine("## Correlated Events");
        sb.AppendLine();
        if (report.CorrelatedPairs.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var pair in report.CorrelatedPairs)
            {
                sb.AppendLine($"- COM `{pair.ComTime:HH:mm:ss.fff}` => `{pair.ComPayload}`");
                sb.AppendLine($"  HID `{pair.HidTime:HH:mm:ss.fff}` => `{pair.HidPayload}`");
            }
        }
    }
    sb.AppendLine();
    sb.AppendLine("## Conclusion");
    sb.AppendLine();
    sb.AppendLine(report.Conclusion switch
    {
        "button_hid_path_confirmed" => "- button HID path is confirmed",
        "button_hid_path_not_confirmed" => "- button HID path is not confirmed yet",
        "button_button_send_failure" => "- firmware reported button send failure",
        "button_not_observed" => "- button input was not observed in CDC",
        "snapshot_only_completed" => "- snapshot completed",
        _ => "- probe completed",
    });
    return sb.ToString();
}

sealed class ComHidCorrelationProbeRunner
{
    private static readonly Regex ComPattern = new(@"\((COM\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SwDiagPattern = new(@"button=SW_00\b.*sendResult=(?<result>true|false)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HidButtonPattern = new(@"button=(?<button>SW_00|TEST_HID)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ComHidCorrelationProbeOptions options;
    private readonly Action<string> write;

    public ComHidCorrelationProbeRunner(ComHidCorrelationProbeOptions options, Action<string> write)
    {
        this.options = options;
        this.write = write;
    }

    public async Task<ComHidCorrelationProbeReport> RunAsync()
    {
        var report = new ComHidCorrelationProbeReport
        {
            GeneratedAt = DateTimeOffset.Now,
            MachineName = Environment.MachineName,
            OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            AppVersion = GetAppVersion(),
            Options = options,
        };

        write("ComHidCorrelationProbe START");
        write($"Options: port={options.PortName ?? "(auto)"}, vid={options.VidHex}, pid={options.PidHex}, durationSec={options.DurationSec}, timeoutMs={options.TimeoutMs}");

        if (options.SnapshotOnly)
            return await RunSnapshotOnlyAsync(report);

        var ports = EnumeratePorts();
        report.Ports = ports;
        write($"Enumerated COM ports: {ports.Count}");
        foreach (var port in ports)
            write($"PORT[{port.Index}] {port.ToLine()}");

        var comCandidates = ports.Where(options.Matches).OrderByDescending(p => p.VendorId == options.Vid && p.ProductId == options.Pid).ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase).ToList();
        report.FilteredComPortCount = comCandidates.Count;
        foreach (var port in comCandidates)
            write($"COM_CANDIDATE[{port.Index}] {port.ToLine()}");

        if (comCandidates.Count == 0)
        {
            report.ExitCode = 1;
            report.Conclusion = "no_matching_com_port";
            write("No matching COM port found. EXIT");
            return report;
        }

        CdcPortSnapshot selectedCom;
        if (!string.IsNullOrWhiteSpace(options.PortName))
        {
            selectedCom = comCandidates.FirstOrDefault(p => string.Equals(p.PortName, options.PortName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Requested port {options.PortName} not found among candidates.");
        }
        else
        {
            selectedCom = comCandidates.First();
        }

        report.SelectedComPort = selectedCom;
        report.SelectedComPortName = selectedCom.PortName;
        write($"SELECTED_COM {selectedCom.ToLine()}");

        using var durationCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSec));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            durationCts.Cancel();
        };

        var hidDevices = await EnumerateHidDevicesWithRetryAsync(options.Vid, options.Pid, durationCts.Token);
        report.HidDevices = hidDevices;
        write($"Enumerated HID devices: {hidDevices.Count}");
        foreach (var device in hidDevices)
            write($"HID_ENUM[{device.Index}] {device.ToLine()}");

        var hidCandidates = hidDevices.Where(d => d.VendorId == options.Vid && d.ProductId == options.Pid).ToList();
        report.FilteredHidDeviceCount = hidCandidates.Count;
        foreach (var device in hidCandidates)
            write($"HID_CANDIDATE[{device.Index}] {device.ToLine()}");

        if (hidCandidates.Count == 0)
        {
            report.ExitCode = 1;
            report.Conclusion = "no_matching_hid_device";
            write("No matching HID device found. EXIT");
            return report;
        }

        if (options.Index < 0 || options.Index >= hidCandidates.Count)
        {
            report.ExitCode = 1;
            report.Conclusion = "hid_index_out_of_range";
            write($"HID selected index {options.Index} is out of range. EXIT");
            return report;
        }

        var selectedHid = hidCandidates[options.Index];
        report.SelectedHidDevice = selectedHid;
        report.SelectedHidPath = selectedHid.DevicePath;
        write($"SELECTED_HID index={options.Index} {selectedHid.ToLine()}");

        var shared = new ComHidCorrelationProbeRuntime
        {
            SelectedComPortName = selectedCom.PortName,
            SelectedComPnpDeviceId = selectedCom.PnpDeviceId,
            SelectedComVid = selectedCom.VendorId,
            SelectedComPid = selectedCom.ProductId,
            SelectedHidPath = selectedHid.DevicePath,
            SelectedHidVid = selectedHid.VendorId,
            SelectedHidPid = selectedHid.ProductId,
        };

        var events = new ConcurrentQueue<CorrelationEvent>();
        var hidTask = ReadHidAsync(selectedHid, durationCts.Token, shared, events);
        await Task.Delay(750, durationCts.Token);
        var comTask = ReadComAsync(selectedCom, durationCts.Token, shared, events);

        await Task.WhenAll(hidTask, comTask);

        var orderedEvents = events.OrderBy(e => e.Timestamp).ThenBy(e => e.Source, StringComparer.OrdinalIgnoreCase).ToList();
        report.Events = orderedEvents;
        PopulateSummary(report, shared, orderedEvents);

        report.ExitCode = report.CorrelatedSwCount > 0 ? 0 : 0;
        report.Conclusion = report.CorrelatedSwCount > 0
            ? "button_hid_path_confirmed"
            : report.SwDiagSentTrueCount > 0
                ? "button_hid_path_not_confirmed"
                : report.SwDiagCount > 0
                    ? "button_button_send_failure"
                    : "button_not_observed";

        write($"CORRELATION_SUMMARY comLineCount={report.ComLineCount} hidReportCount={report.HidReportCount} testHidCount={report.TestHidCount} swReportCount={report.SwReportCount} otherReportCount={report.OtherReportCount} swDiagCount={report.SwDiagCount} swDiagSentTrueCount={report.SwDiagSentTrueCount} swDiagSentFalseCount={report.SwDiagSentFalseCount} correlatedSwCount={report.CorrelatedSwCount}");
        write("ComHidCorrelationProbe END");
        return report;
    }

    private async Task<ComHidCorrelationProbeReport> RunSnapshotOnlyAsync(ComHidCorrelationProbeReport report)
    {
        var snapshots = new List<UsbInterfaceSnapshotRecord>();
        var deadline = DateTimeOffset.Now.AddSeconds(options.DurationSec);
        var index = 0;

        while (DateTimeOffset.Now < deadline)
        {
            index++;
            var comPorts = EnumeratePorts();
            var hidDevices = EnumerateHidDevices();
            var matchingCom = comPorts.Where(options.Matches).ToList();
            var matchingHid = hidDevices.Where(d => d.VendorId == options.Vid && d.ProductId == options.Pid).ToList();
            var bothPresent = matchingCom.Count > 0 && matchingHid.Count > 0;

            snapshots.Add(new UsbInterfaceSnapshotRecord
            {
                Timestamp = DateTimeOffset.Now,
                ComCount = matchingCom.Count,
                HidCount = matchingHid.Count,
                BothPresent = bothPresent,
                ComPorts = matchingCom,
                HidDevices = matchingHid,
            });

            write($"SNAPSHOT[{index}] COM={matchingCom.Count} HID={matchingHid.Count} BOTH={(bothPresent ? "yes" : "no")}");

            try
            {
                await Task.Delay(1000);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        report.Snapshots = snapshots;
        report.SnapshotCount = snapshots.Count;
        report.HidPresentCount = snapshots.Count(s => s.HidCount > 0);
        report.ComPresentCount = snapshots.Count(s => s.ComCount > 0);
        report.BothPresentCount = snapshots.Count(s => s.BothPresent);
        report.HidMissingCount = snapshots.Count(s => s.HidCount == 0);
        report.ComMissingCount = snapshots.Count(s => s.ComCount == 0);
        report.ExitCode = 0;
        report.Conclusion = "snapshot_only_completed";
        write($"SNAPSHOT_SUMMARY snapshotCount={report.SnapshotCount} hidPresentCount={report.HidPresentCount} comPresentCount={report.ComPresentCount} bothPresentCount={report.BothPresentCount} hidMissingCount={report.HidMissingCount} comMissingCount={report.ComMissingCount}");
        write("ComHidCorrelationProbe END");
        return report;
    }

    private void PopulateSummary(ComHidCorrelationProbeReport report, ComHidCorrelationProbeRuntime shared, List<CorrelationEvent> events)
    {
        report.ComLineCount = shared.ComLineCount;
        report.HidReportCount = shared.HidReportCount;
        report.TestHidCount = shared.TestHidCount;
        report.SwReportCount = shared.SwReportCount;
        report.OtherReportCount = shared.OtherReportCount;
        report.SwDiagCount = events.Count(e => e.Source.Equals("COM", StringComparison.OrdinalIgnoreCase) && e.Type.Equals("HID_DIAG", StringComparison.OrdinalIgnoreCase) && e.Payload.Contains("button=SW_00", StringComparison.OrdinalIgnoreCase));
        report.SwDiagSentTrueCount = events.Count(e => e.Source.Equals("COM", StringComparison.OrdinalIgnoreCase) && e.Type.Equals("HID_DIAG", StringComparison.OrdinalIgnoreCase) && IsSwDiagSent(e.Payload, true));
        report.SwDiagSentFalseCount = events.Count(e => e.Source.Equals("COM", StringComparison.OrdinalIgnoreCase) && e.Type.Equals("HID_DIAG", StringComparison.OrdinalIgnoreCase) && IsSwDiagSent(e.Payload, false));
        report.CorrelatedPairs = CorrelateSw(events);
        report.CorrelatedSwCount = report.CorrelatedPairs.Count;
        report.Runtime = shared;
    }

    private static bool IsSwDiagSent(string payload, bool sent)
    {
        var match = SwDiagPattern.Match(payload);
        return match.Success && bool.TryParse(match.Groups["result"].Value, out var parsed) && parsed == sent;
    }

    private static List<CorrelationPair> CorrelateSw(List<CorrelationEvent> events)
    {
        var comSw = events
            .Where(e => e.Source.Equals("COM", StringComparison.OrdinalIgnoreCase)
                && e.Type.Equals("HID_DIAG", StringComparison.OrdinalIgnoreCase)
                && e.Payload.Contains("button=SW_00", StringComparison.OrdinalIgnoreCase)
                && e.Payload.Contains("sendResult=true", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Timestamp)
            .ToList();

        var hidSw = events
            .Where(e => e.Source.Equals("HID", StringComparison.OrdinalIgnoreCase) && e.Type.Equals("SW", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Timestamp)
            .ToList();

        var pairs = new List<CorrelationPair>();
        var used = new bool[hidSw.Count];
        foreach (var comEvent in comSw)
        {
            for (var i = 0; i < hidSw.Count; i++)
            {
                if (used[i])
                    continue;

                var hidEvent = hidSw[i];
                if (hidEvent.Timestamp < comEvent.Timestamp.AddMilliseconds(-250))
                    continue;

                if (hidEvent.Timestamp > comEvent.Timestamp.AddSeconds(2))
                    break;

                pairs.Add(new CorrelationPair
                {
                    ComTime = comEvent.Timestamp,
                    ComPayload = comEvent.Payload,
                    HidTime = hidEvent.Timestamp,
                    HidPayload = hidEvent.Payload,
                });
                used[i] = true;
                break;
            }
        }

        return pairs;
    }

    private async Task ReadComAsync(CdcPortSnapshot portSnapshot, CancellationToken token, ComHidCorrelationProbeRuntime runtime, ConcurrentQueue<CorrelationEvent> events)
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
            runtime.ComOpenSucceeded = true;
            runtime.ComReadLoopStarted = true;
            runtime.OpenedComPortName = portSnapshot.PortName;
            runtime.OpenedComPnpDeviceId = portSnapshot.PnpDeviceId;
            runtime.OpenedComVid = portSnapshot.VendorId;
            runtime.OpenedComPid = portSnapshot.ProductId;

            write($"COM_OPEN_OK port={portSnapshot.PortName}");
            write($"COM_OPEN_INFO {portSnapshot.ToLine()}");

            await Task.Delay(120, token);
            port.DiscardInBuffer();

            var stopwatch = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(options.DurationSec))
                    break;

                runtime.ComReadAttemptCount++;
                string line;
                try
                {
                    line = port.ReadLine();
                }
                catch (TimeoutException)
                {
                    runtime.ComReadTimeoutCount++;
                    runtime.LastComException = "TimeoutException";
                    continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    runtime.LastComException = $"{ex.GetType().Name}: {ex.Message}";
                    write($"COM_READ_FAIL port={portSnapshot.PortName} error={runtime.LastComException}");
                    break;
                }

                var normalized = NormalizeLine(line);
                if (normalized.Length == 0)
                    continue;

                runtime.ComLineCount++;
                runtime.LastComLineAtUtc = DateTimeOffset.UtcNow;
                runtime.FirstComLineAtUtc ??= runtime.LastComLineAtUtc;

                var type = ClassifyComLine(normalized);
                events.Enqueue(new CorrelationEvent
                {
                    Timestamp = DateTimeOffset.Now,
                    Source = "COM",
                    Type = type,
                    Payload = normalized,
                });

                write($"COM_LINE[{runtime.ComLineCount}] {normalized}");
                if (normalized.Contains("FW_INFO", StringComparison.OrdinalIgnoreCase))
                    runtime.FirmwareInfoSeen = true;
            }
        }
        catch (Exception ex)
        {
            runtime.LastComException = $"{ex.GetType().Name}: {ex.Message}";
            write($"COM_RUN_FAIL error={runtime.LastComException}");
        }
    }

    private async Task ReadHidAsync(HidConsoleProbeDevice device, CancellationToken token, ComHidCorrelationProbeRuntime runtime, ConcurrentQueue<CorrelationEvent> events)
    {
        try
        {
            if (!device.TryOpen(out var stream))
            {
                runtime.LastHidException = "TryOpen failed";
                write($"HID_OPEN_FAIL path={device.DevicePath}");
                return;
            }

            using (stream)
            {
                runtime.HidOpenSucceeded = true;
                runtime.HidReadLoopStarted = true;
                runtime.OpenedHidPath = device.DevicePath;
                runtime.OpenedHidVid = device.VendorId;
                runtime.OpenedHidPid = device.ProductId;
                runtime.OpenedHidProductName = device.ProductName;
                runtime.OpenedHidManufacturer = device.Manufacturer;
                runtime.OpenedHidSerial = device.SerialNumber;
                runtime.OpenedHidUsagePage = device.UsagePage;
                runtime.OpenedHidUsage = device.Usage;
                runtime.OpenedHidMaxInputReportLength = device.MaxInputReportLength;
                runtime.OpenedHidMaxOutputReportLength = device.MaxOutputReportLength;
                runtime.OpenedHidMaxFeatureReportLength = device.MaxFeatureReportLength;

                write($"HID_OPEN_OK path={device.DevicePath}");
                write($"HID_OPEN_INFO productName={device.ProductName} manufacturer={device.Manufacturer} serial={device.SerialNumber} usagePage=0x{device.UsagePage:X4} usage=0x{device.Usage:X4} in={device.MaxInputReportLength} out={device.MaxOutputReportLength} feature={device.MaxFeatureReportLength}");

                try
                {
                    stream.ReadTimeout = options.TimeoutMs;
                }
                catch
                {
                }

                var buffer = new byte[Math.Max(64, device.MaxInputReportLength)];
                var stopwatch = Stopwatch.StartNew();
                var sampleIndex = 0;
                while (!token.IsCancellationRequested)
                {
                    if (stopwatch.Elapsed >= TimeSpan.FromSeconds(options.DurationSec))
                        break;

                    runtime.HidReadAttemptCount++;

                    int length;
                    try
                    {
                        length = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (TimeoutException)
                    {
                        runtime.HidReadTimeoutCount++;
                        runtime.LastHidException = "TimeoutException";
                        continue;
                    }
                    catch (Exception ex)
                    {
                        runtime.LastHidException = $"{ex.GetType().Name}: {ex.Message}";
                        write($"HID_READ_FAIL path={device.DevicePath} error={runtime.LastHidException}");
                        break;
                    }

                    if (length <= 0)
                        break;

                    runtime.HidReportCount++;
                    runtime.LastHidReadAtUtc = DateTimeOffset.Now;
                    sampleIndex++;

                    var hex = ToHex(buffer, length);
                    var ascii = ToAscii(buffer, length);
                    var payload = StripReportIdPrefix(ascii);
                    var reportKind = ClassifyReport(payload);

                    if (reportKind == "TEST_HID")
                        runtime.TestHidCount++;
                    else if (reportKind == "SW")
                        runtime.SwReportCount++;
                    else if (reportKind == "OTHER")
                        runtime.OtherReportCount++;

                    events.Enqueue(new CorrelationEvent
                    {
                        Timestamp = DateTimeOffset.Now,
                        Source = "HID",
                        Type = reportKind,
                        Payload = payload,
                        Hex = hex,
                    });

                    write($"HID_REPORT[{sampleIndex}] kind={reportKind} len={length} payload={payload}");
                }
            }
        }
        catch (Exception ex)
        {
            runtime.LastHidException = $"{ex.GetType().Name}: {ex.Message}";
            write($"HID_RUN_FAIL error={runtime.LastHidException}");
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
        }

        try
        {
            result.AddRange(EnumeratePortsFromPnpDevice());
        }
        catch
        {
        }

        result = result
            .GroupBy(p => $"{p.PortName}|{p.PnpDeviceId}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return result
            .OrderByDescending(p => p.VendorId == 0x2E8A && p.ProductId == 0x4020)
            .ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
            .Select((p, index) => p with { Index = index })
            .ToList();
    }

    private static List<CdcPortSnapshot> EnumeratePortsFromPnpDevice()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-PnpDevice -Class Ports | Select-Object Status,FriendlyName,InstanceId | ConvertTo-Json -Depth 2\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start powershell.exe");
        var stdout = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrWhiteSpace(stdout))
            return [];

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        var items = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(root.EnumerateArray().ToArray());
        }
        else
        {
            items.Add(root);
        }

        var list = new List<CdcPortSnapshot>();
        foreach (var item in items)
        {
            var friendlyName = item.TryGetProperty("FriendlyName", out var friendlyProp) ? friendlyProp.GetString() ?? string.Empty : string.Empty;
            var instanceId = item.TryGetProperty("InstanceId", out var instanceProp) ? instanceProp.GetString() ?? string.Empty : string.Empty;

            var match = ComPattern.Match(friendlyName);
            if (!match.Success)
                continue;

            var portName = match.Groups[1].Value.ToUpperInvariant();
            var (vid, pid) = ParseVidPid(instanceId);
            var serial = ParseSerialEstimate(instanceId);

            list.Add(new CdcPortSnapshot
            {
                Index = 0,
                PortName = portName,
                Description = friendlyName,
                Manufacturer = string.Empty,
                PnpDeviceId = instanceId,
                VendorId = vid,
                ProductId = pid,
                SerialEstimate = serial,
            });
        }

        return list;
    }

    private static List<HidConsoleProbeDevice> EnumerateHidDevices()
    {
        return DeviceList.Local.GetHidDevices()
            .Select((device, index) => new HidConsoleProbeDevice
            {
                Index = index,
                VendorId = device.VendorID,
                ProductId = device.ProductID,
                ProductName = Safe(() => device.GetProductName()),
                Manufacturer = Safe(() => device.GetManufacturer()),
                SerialNumber = Safe(() => device.GetSerialNumber()),
                DevicePath = device.DevicePath ?? string.Empty,
                UsagePage = TryGetInt(device, "UsagePage"),
                Usage = TryGetInt(device, "Usage"),
                MaxInputReportLength = SafeInt(() => device.GetMaxInputReportLength()),
                MaxOutputReportLength = SafeInt(() => device.GetMaxOutputReportLength()),
                MaxFeatureReportLength = TryGetIntMethod(device, "GetMaxFeatureReportLength"),
            })
            .ToList();
    }

    private async Task<List<HidConsoleProbeDevice>> EnumerateHidDevicesWithRetryAsync(int vid, int pid, CancellationToken token)
    {
        var deadline = DateTimeOffset.Now.AddSeconds(10);
        List<HidConsoleProbeDevice> devices;

        do
        {
            devices = EnumerateHidDevices();
            if (devices.Any(d => d.VendorId == vid && d.ProductId == pid))
                return devices;

            try
            {
                await Task.Delay(250, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        while (!token.IsCancellationRequested && DateTimeOffset.Now < deadline);

        return devices;
    }

    private static string ClassifyComLine(string line)
    {
        if (line.Contains("HID_DIAG", StringComparison.OrdinalIgnoreCase))
            return "HID_DIAG";
        if (line.Contains("HID_STATUS", StringComparison.OrdinalIgnoreCase))
            return "HID_STATUS";
        if (line.Contains("HID_TEST", StringComparison.OrdinalIgnoreCase))
            return "HID_TEST";
        if (line.Contains("HB:", StringComparison.OrdinalIgnoreCase))
            return "HB";
        if (line.Contains("SW_00", StringComparison.OrdinalIgnoreCase))
            return "SW_00";
        if (line.Contains("FW_INFO", StringComparison.OrdinalIgnoreCase))
            return "FW_INFO";
        if (line.Contains("FW_VERSION", StringComparison.OrdinalIgnoreCase))
            return "FW_VERSION";
        return "OTHER";
    }

    private static string ClassifyReport(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "OTHER";
        if (payload.StartsWith("TEST_HID_", StringComparison.OrdinalIgnoreCase))
            return "TEST_HID";
        if (payload.Contains("SW_00", StringComparison.OrdinalIgnoreCase) || payload.StartsWith("YMMK:", StringComparison.OrdinalIgnoreCase))
            return "SW";
        return "OTHER";
    }

    private static string StripReportIdPrefix(string ascii)
    {
        if (string.IsNullOrEmpty(ascii))
            return string.Empty;

        return ascii[0] == '\u0001' ? ascii[1..] : ascii;
    }

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

    private static string ToHex(byte[] buffer, int length)
        => BitConverter.ToString(buffer, 0, length).Replace("-", " ", StringComparison.Ordinal);

    private static string ToAscii(byte[] buffer, int length)
        => Encoding.ASCII.GetString(buffer, 0, length).Trim('\0', '\r', '\n', ' ');

    private static (int Vid, int Pid) ParseVidPid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0);

        var match = Regex.Match(value, @"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (0, 0);

        return (ParseHex(match.Groups[1].Value), ParseHex(match.Groups[2].Value));
    }

    private static string ParseSerialEstimate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var match = Regex.Match(value, @"\\([^\\]+)$");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static int ParseHex(string value)
        => int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static int TryGetInt(HidDevice device, string propertyName)
    {
        try
        {
            var prop = device.GetType().GetProperty(propertyName);
            if (prop is not null)
                return Convert.ToInt32(prop.GetValue(device) ?? 0);
        }
        catch
        {
        }

        return 0;
    }

    private static int TryGetIntMethod(HidDevice device, string methodName)
    {
        try
        {
            var method = device.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method is not null)
                return Convert.ToInt32(method.Invoke(device, null) ?? 0);
        }
        catch
        {
        }

        return 0;
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

    private static int SafeInt(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(ComHidCorrelationProbeRunner).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}

sealed class ComHidCorrelationProbeOptions
{
    public string? PortName { get; private init; }
    public string VidHex { get; private init; } = "2E8A";
    public string PidHex { get; private init; } = "4020";
    public int Vid { get; private init; } = 0x2E8A;
    public int Pid { get; private init; } = 0x4020;
    public int Index { get; private init; }
    public int TimeoutMs { get; private init; } = 500;
    public int DurationSec { get; private init; } = 30;
    public bool SnapshotOnly { get; private init; }
    public string OutputDirectory { get; private init; } = Path.Combine(Environment.CurrentDirectory, "tmp", "com-hid-correlation");

    public static ComHidCorrelationProbeOptions Parse(string[] args)
    {
        string? portName = null;
        var vidHex = "2E8A";
        var pidHex = "4020";
        var index = 0;
        var timeoutMs = 500;
        var durationSec = 30;
        var snapshotOnly = false;
        string? outputDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (args[i])
            {
                case "--port":
                    portName = Next();
                    break;
                case "--vid":
                    vidHex = Next() ?? vidHex;
                    break;
                case "--pid":
                    pidHex = Next() ?? pidHex;
                    break;
                case "--index":
                    index = int.TryParse(Next(), out var parsedIndex) ? Math.Max(0, parsedIndex) : index;
                    break;
                case "--timeout-ms":
                    timeoutMs = int.TryParse(Next(), out var parsedTimeout) ? Math.Max(1, parsedTimeout) : timeoutMs;
                    break;
                case "--duration-sec":
                    durationSec = int.TryParse(Next(), out var parsedDuration) ? Math.Max(1, parsedDuration) : durationSec;
                    break;
                case "--output-dir":
                    outputDir = Next();
                    break;
                case "--snapshot-only":
                    snapshotOnly = true;
                    break;
            }
        }

        return new ComHidCorrelationProbeOptions
        {
            PortName = portName,
            VidHex = vidHex,
            PidHex = pidHex,
            Vid = ParseHex16(vidHex),
            Pid = ParseHex16(pidHex),
            Index = index,
            TimeoutMs = timeoutMs,
            DurationSec = durationSec,
            SnapshotOnly = snapshotOnly,
            OutputDirectory = string.IsNullOrWhiteSpace(outputDir)
                ? Path.Combine(Environment.CurrentDirectory, "tmp", snapshotOnly ? "usb-interface-snapshot" : "com-hid-correlation")
                : outputDir,
        };
    }

    public bool Matches(CdcPortSnapshot port)
    {
        if (!string.IsNullOrWhiteSpace(PortName))
            return string.Equals(port.PortName, PortName, StringComparison.OrdinalIgnoreCase);

        return port.VendorId == Vid && port.ProductId == Pid;
    }

    private static int ParseHex16(string value)
    {
        var s = value.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}

sealed class ComHidCorrelationProbeReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;
    public ComHidCorrelationProbeOptions Options { get; init; } = new();
    public List<CdcPortSnapshot> Ports { get; set; } = [];
    public int FilteredComPortCount { get; set; }
    public CdcPortSnapshot? SelectedComPort { get; set; }
    public string? SelectedComPortName { get; set; }
    public List<HidConsoleProbeDevice> HidDevices { get; set; } = [];
    public int FilteredHidDeviceCount { get; set; }
    public HidConsoleProbeDevice? SelectedHidDevice { get; set; }
    public string? SelectedHidPath { get; set; }
    public ComHidCorrelationProbeRuntime Runtime { get; set; } = new();
    public List<CorrelationEvent> Events { get; set; } = [];
    public List<CorrelationPair> CorrelatedPairs { get; set; } = [];
    public List<UsbInterfaceSnapshotRecord> Snapshots { get; set; } = [];
    public int SnapshotCount { get; set; }
    public int HidPresentCount { get; set; }
    public int ComPresentCount { get; set; }
    public int BothPresentCount { get; set; }
    public int HidMissingCount { get; set; }
    public int ComMissingCount { get; set; }
    public int ComLineCount { get; set; }
    public int HidReportCount { get; set; }
    public int TestHidCount { get; set; }
    public int SwReportCount { get; set; }
    public int OtherReportCount { get; set; }
    public int SwDiagCount { get; set; }
    public int SwDiagSentTrueCount { get; set; }
    public int SwDiagSentFalseCount { get; set; }
    public int CorrelatedSwCount { get; set; }
    public int ExitCode { get; set; }
    public string Conclusion { get; set; } = string.Empty;
}

sealed class ComHidCorrelationProbeRuntime
{
    public string SelectedComPortName { get; set; } = string.Empty;
    public string SelectedComPnpDeviceId { get; set; } = string.Empty;
    public int SelectedComVid { get; set; }
    public int SelectedComPid { get; set; }
    public string SelectedHidPath { get; set; } = string.Empty;
    public int SelectedHidVid { get; set; }
    public int SelectedHidPid { get; set; }
    public bool ComOpenSucceeded { get; set; }
    public bool ComReadLoopStarted { get; set; }
    public int ComReadAttemptCount { get; set; }
    public int ComReadTimeoutCount { get; set; }
    public int ComLineCount { get; set; }
    public DateTimeOffset? FirstComLineAtUtc { get; set; }
    public DateTimeOffset? LastComLineAtUtc { get; set; }
    public string? LastComException { get; set; }
    public bool HidOpenSucceeded { get; set; }
    public bool HidReadLoopStarted { get; set; }
    public int HidReadAttemptCount { get; set; }
    public int HidReadTimeoutCount { get; set; }
    public int HidReportCount { get; set; }
    public int TestHidCount { get; set; }
    public int SwReportCount { get; set; }
    public int OtherReportCount { get; set; }
    public DateTimeOffset? LastHidReadAtUtc { get; set; }
    public string? LastHidException { get; set; }
    public string OpenedComPortName { get; set; } = string.Empty;
    public string OpenedComPnpDeviceId { get; set; } = string.Empty;
    public int OpenedComVid { get; set; }
    public int OpenedComPid { get; set; }
    public string OpenedHidPath { get; set; } = string.Empty;
    public int OpenedHidVid { get; set; }
    public int OpenedHidPid { get; set; }
    public string OpenedHidProductName { get; set; } = string.Empty;
    public string OpenedHidManufacturer { get; set; } = string.Empty;
    public string OpenedHidSerial { get; set; } = string.Empty;
    public int OpenedHidUsagePage { get; set; }
    public int OpenedHidUsage { get; set; }
    public int OpenedHidMaxInputReportLength { get; set; }
    public int OpenedHidMaxOutputReportLength { get; set; }
    public int OpenedHidMaxFeatureReportLength { get; set; }
    public bool FirmwareInfoSeen { get; set; }
}

sealed class CorrelationEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Hex { get; set; }
}

sealed class CorrelationPair
{
    public DateTimeOffset ComTime { get; set; }
    public string ComPayload { get; set; } = string.Empty;
    public DateTimeOffset HidTime { get; set; }
    public string HidPayload { get; set; } = string.Empty;
}

sealed class UsbInterfaceSnapshotRecord
{
    public DateTimeOffset Timestamp { get; set; }
    public int ComCount { get; set; }
    public int HidCount { get; set; }
    public bool BothPresent { get; set; }
    public List<CdcPortSnapshot> ComPorts { get; set; } = [];
    public List<HidConsoleProbeDevice> HidDevices { get; set; } = [];
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

sealed class HidConsoleProbeDevice
{
    public int Index { get; init; }
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string DevicePath { get; init; } = string.Empty;
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
    public int MaxFeatureReportLength { get; init; }

    public bool TryOpen(out HidStream stream)
    {
        var device = DeviceList.Local.GetHidDevices().FirstOrDefault(d =>
            d.VendorID == VendorId
            && d.ProductID == ProductId
            && string.Equals(d.DevicePath ?? string.Empty, DevicePath, StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            stream = null!;
            return false;
        }

        return device.TryOpen(out stream);
    }

    public string ToLine()
        => $"path={DevicePath}, productName={ProductName}, manufacturer={Manufacturer}, serial={SerialNumber}, usagePage=0x{UsagePage:X4}, usage=0x{Usage:X4}, maxInputReportLength={MaxInputReportLength}, maxOutputReportLength={MaxOutputReportLength}, maxFeatureReportLength={MaxFeatureReportLength}";
}
