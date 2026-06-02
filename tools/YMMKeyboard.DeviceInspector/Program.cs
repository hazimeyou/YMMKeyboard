using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using HidSharp;
using Microsoft.Win32;
using YMMKeyboard.DeviceInspector;

using var app = new DeviceInspectorApp(DeviceInspectorOptions.Parse(args));
await app.RunAsync();

internal sealed class DeviceInspectorApp : IDisposable
{
    private static readonly Regex EventPattern = new(
        @"(?<uid>[0-9a-fA-F]+):(?<state>[PR]):SW_(?<switch>\d+)",
        RegexOptions.Compiled);

    private readonly DeviceInspectorOptions options;
    private readonly string logPath;
    private readonly StreamWriter logWriter;

    public DeviceInspectorApp(DeviceInspectorOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.LogDirectory);
        logPath = Path.Combine(options.LogDirectory, $"device-inspector_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        logWriter = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public async Task RunAsync()
    {
        Write("DeviceInspector START");
        Write($"LogFile={logPath}");
        Write($"FormalIdentity={DeviceIdentity.DescribeFormal()}");
        Write($"MachineName={Environment.MachineName}");
        Write($"OsVersion={RuntimeInformation.OSDescription}");
        Write($"AppVersion={GetAppVersion()}");
        Write($"ProbeSerial={options.ProbeSerial}, SerialProbeDurationMs={options.SerialProbeDurationMs}");
        Write(string.Empty);

        var hidDevices = EnumerateHidDevices();
        DumpHidDevices(hidDevices);
        Write(string.Empty);

        var comPorts = EnumerateComPorts().ToArray();
        DumpComPorts(comPorts);
        Write(string.Empty);

        var serialProbeResults = new List<SerialProbeResult>();
        if (options.ProbeSerial)
        {
            serialProbeResults = await ProbeSerialPortsAsync(comPorts);
            Write(string.Empty);
        }

        var matchedCandidates = BuildMatchedCandidates(hidDevices);
        var warnings = BuildWarnings(hidDevices, comPorts, serialProbeResults, matchedCandidates);

        Write("DeviceInspector END");
        Write($"Summary: hid={hidDevices.Count}, com={comPorts.Length}, serialProbe={options.ProbeSerial}");
        Write($"LogFile={logPath}");

        if (options.EmitJson)
        {
            var outputPath = string.IsNullOrWhiteSpace(options.OutputPath)
                ? JsonReportWriter.GetDefaultOutputPath(options.LogDirectory)
                : options.OutputPath!;

            var report = new DeviceInspectionReport
            {
                GeneratedAt = DateTimeOffset.Now,
                MachineName = Environment.MachineName,
                OsVersion = RuntimeInformation.OSDescription,
                AppVersion = GetAppVersion(),
                HidDevices = hidDevices.ToList(),
                ComPorts = comPorts.ToList(),
                SerialProbeResults = serialProbeResults,
                MatchedYmmKeyboardCandidates = matchedCandidates,
                Warnings = warnings,
            };

            JsonReportWriter.Write(outputPath, report);
            Write($"JSONReport={outputPath}");
        }
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private ReadOnlyCollection<HidDeviceSnapshot> EnumerateHidDevices()
    {
        var devices = DeviceList.Local.GetHidDevices()
            .Select(device =>
            {
                var productName = Safe(() => device.GetProductName());
                var manufacturer = Safe(() => device.GetManufacturer());
                var serial = Safe(() => device.GetSerialNumber());
                var usagePage = TryGetInt(device, "UsagePage");
                var usage = TryGetInt(device, "Usage");

                return new HidDeviceSnapshot
                {
                    VendorId = device.VendorID,
                    ProductId = device.ProductID,
                    ProductName = productName,
                    Manufacturer = manufacturer,
                    SerialNumber = serial,
                    DevicePath = device.DevicePath ?? string.Empty,
                    MaxInputReportLength = SafeInt(() => device.GetMaxInputReportLength()),
                    MaxOutputReportLength = SafeInt(() => device.GetMaxOutputReportLength()),
                    UsagePage = usagePage,
                    Usage = usage,
                    IdentityKind = DeviceIdentity.ClassifyHid(device.VendorID, device.ProductID, productName, manufacturer, usagePage, usage),
                };
            })
            .OrderBy(d => d.VendorId)
            .ThenBy(d => d.ProductId)
            .ThenBy(d => d.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Write($"HID count={devices.Length}");
        return Array.AsReadOnly(devices);
    }

    private void DumpHidDevices(IReadOnlyList<HidDeviceSnapshot> devices)
    {
        Write("== HID ==");
        foreach (var device in devices)
        {
            Write(device.ToLine());
        }
    }

    private IEnumerable<string> EnumerateComPorts()
    {
        var ports = TryGetSerialPorts();
        Write($"COM count={ports.Length}");
        return ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
    }

    private void DumpComPorts(IEnumerable<string> ports)
    {
        Write("== COM ==");
        foreach (var port in ports)
            Write($"COM port={port}");
    }

    private async Task<List<SerialProbeResult>> ProbeSerialPortsAsync(IEnumerable<string> ports)
    {
        var results = new List<SerialProbeResult>();
        Write("== SERIAL PROBE ==");
        foreach (var portName in ports)
        {
            results.Add(await ProbeOneSerialPortAsync(portName));
        }

        return results;
    }

    private async Task<SerialProbeResult> ProbeOneSerialPortAsync(string portName)
    {
        var result = new SerialProbeResult
        {
            PortName = portName,
            Status = "unknown",
        };

        try
        {
            using var port = new SerialPort(portName, 115200)
            {
                Encoding = Encoding.ASCII,
                NewLine = "\n",
                ReadTimeout = 500,
                WriteTimeout = 500,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
            };

            port.Open();
            port.DtrEnable = false;
            await Task.Delay(30);
            port.DtrEnable = true;
            await Task.Delay(120);
            port.DiscardInBuffer();

            var sw = Stopwatch.StartNew();
            var buffer = new StringBuilder();
            var lineCount = 0;
            while (sw.ElapsedMilliseconds < options.SerialProbeDurationMs)
            {
                string chunk;
                try
                {
                    chunk = port.ReadExisting();
                }
                catch (TimeoutException)
                {
                    await Task.Delay(50);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(chunk))
                {
                    await Task.Delay(50);
                    continue;
                }

                buffer.Append(chunk);
                foreach (var raw in buffer.ToString().Split('\n'))
                {
                    var line = NormalizeLine(raw);
                    if (line.Length == 0)
                        continue;

                    lineCount++;
                    Write($"SERIAL port={portName} line={line}");
                    result.Lines.Add(line);
                    var match = EventPattern.Match(line);
                    if (match.Success)
                    {
                        var uid = match.Groups["uid"].Value.ToLowerInvariant();
                        var state = match.Groups["state"].Value;
                        var switchId = match.Groups["switch"].Value;
                        Write($"SERIAL_EVENT port={portName} uid={uid} state={state} switch={switchId}");
                        result.Events.Add($"{uid}:{state}:SW_{switchId}");
                    }
                }

                buffer.Clear();
                await Task.Delay(50);
            }

            result.Status = "ok";
            result.LineCount = lineCount;
            Write($"SERIAL port={portName} probe=ok lines={lineCount}");
        }
        catch (Exception ex)
        {
            result.Status = "fail";
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
            Write($"SERIAL port={portName} probe=fail error={ex.GetType().Name}: {ex.Message}");
        }

        return result;
    }

    private static string[] TryGetSerialPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
                return ports;

            return GetPortsFromRegistry();
        }
        catch (PlatformNotSupportedException)
        {
            return GetPortsFromRegistry();
        }
        catch
        {
            return GetPortsFromRegistry();
        }
    }

    private static string[] GetPortsFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is null)
                return Array.Empty<string>();

            return key.GetValueNames()
                .Select(name => key.GetValue(name)?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        Console.WriteLine(line);
        logWriter.WriteLine(line);
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

    private static int TryGetInt(HidDevice device, string propertyName)
    {
        try
        {
            var prop = device.GetType().GetProperty(propertyName);
            if (prop is not null)
                return Convert.ToInt32(prop.GetValue(device) ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
        }

        return 0;
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

    private static List<MatchedYmmKeyboardCandidate> BuildMatchedCandidates(
        IReadOnlyList<HidDeviceSnapshot> hidDevices)
    {
        return hidDevices
            .Where(device => device.IdentityKind is not "other")
            .Select(device =>
            {
                var reason = device.IdentityKind switch
                {
                    "formal" => "matched formal identity",
                    _ => "matched by inspector heuristic",
                };

                return new MatchedYmmKeyboardCandidate
                {
                    IdentityKind = device.IdentityKind,
                    TransportType = "HID",
                    Vid = device.VendorId,
                    Pid = device.ProductId,
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    ProductName = device.ProductName,
                    Manufacturer = device.Manufacturer,
                    Serial = device.SerialNumber,
                    ComPort = string.Empty,
                    UsagePage = device.UsagePage,
                    Usage = device.Usage,
                    DevicePath = device.DevicePath,
                    MatchReason = reason,
                    MatchReasons = [reason],
                };
            })
            .OrderByDescending(candidate => candidate.IdentityKind == "formal")
            .ThenBy(candidate => candidate.VendorId)
            .ThenBy(candidate => candidate.ProductId)
            .ToList();
    }

    private static List<string> BuildWarnings(
        IReadOnlyList<HidDeviceSnapshot> hidDevices,
        IReadOnlyList<string> comPorts,
        IReadOnlyList<SerialProbeResult> serialProbeResults,
        IReadOnlyList<MatchedYmmKeyboardCandidate> matchedCandidates)
    {
        var warnings = new List<string>();

        if (hidDevices.Count == 0)
            warnings.Add("No HID devices were enumerated.");
        if (comPorts.Count == 0)
            warnings.Add("No COM ports were enumerated.");
        if (serialProbeResults.Count > 0 && serialProbeResults.All(r => r.Status != "ok"))
            warnings.Add("Serial probe did not succeed on any port.");
        if (matchedCandidates.Count == 0)
            warnings.Add("No YMM keyboard candidates were matched.");

        return warnings;
    }

    public void Dispose()
    {
        logWriter.Dispose();
    }
}
