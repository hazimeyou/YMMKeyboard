using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HidSharp;

var options = HidConsoleProbeOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);

var logPath = Path.Combine(options.OutputDirectory, "hid-console-probe.log");
var jsonPath = Path.Combine(options.OutputDirectory, "hid-console-probe.json");

using var logWriter = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };

void Write(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

var runner = new HidConsoleProbeRunner(options, Write);
var report = await runner.RunAsync();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
};
File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));

Write($"LOG={logPath}");
Write($"JSON={jsonPath}");
Environment.ExitCode = report.ExitCode;

sealed class HidConsoleProbeRunner
{
    private static readonly Encoding Ascii = Encoding.ASCII;
    private readonly HidConsoleProbeOptions options;
    private readonly Action<string> write;

    public HidConsoleProbeRunner(HidConsoleProbeOptions options, Action<string> write)
    {
        this.options = options;
        this.write = write;
    }

    public async Task<HidConsoleProbeReport> RunAsync()
    {
        var report = new HidConsoleProbeReport
        {
            GeneratedAt = DateTimeOffset.Now,
            MachineName = Environment.MachineName,
            OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            AppVersion = GetAppVersion(),
            Options = options,
        };

        write("HidConsoleProbe START");
        write($"Options: vid={options.VidHex}, pid={options.PidHex}, index={options.Index}, timeoutMs={options.TimeoutMs}, durationSec={options.DurationSec}");

        var devices = EnumerateDevices();
        report.Devices = devices;

        write($"Enumerated HID devices: {devices.Count}");
        foreach (var device in devices)
            write($"ENUM[{device.Index}] {device.ToLine()}");

        var filtered = devices
            .Where(d => d.VendorId == options.Vid && d.ProductId == options.Pid)
            .ToList();

        report.FilteredDeviceCount = filtered.Count;
        write($"Filtered candidates: {filtered.Count}");
        foreach (var device in filtered)
            write($"CANDIDATE[{device.Index}] {device.ToLine()}");

        if (filtered.Count == 0)
        {
            report.ExitCode = 1;
            report.Conclusion = "no_matching_device";
            write("No matching HID device found. EXIT");
            return report;
        }

        if (options.Index < 0 || options.Index >= filtered.Count)
        {
            report.ExitCode = 1;
            report.Conclusion = "index_out_of_range";
            write($"Selected index {options.Index} is out of range. EXIT");
            return report;
        }

        var selected = filtered[options.Index];
        report.SelectedDevice = selected;
        report.SelectedPath = selected.DevicePath;
        write($"SELECTED index={options.Index} {selected.ToLine()}");

        using var durationCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSec));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            durationCts.Cancel();
        };

        var runtime = new HidConsoleProbeRuntime
        {
            SelectedPath = selected.DevicePath,
            SelectedVid = selected.VendorId,
            SelectedPid = selected.ProductId,
            OpenSucceeded = false,
            ReadLoopStarted = false,
        };

        if (!await TryOpenAndReadAsync(selected, durationCts.Token, runtime, report))
        {
            report.ExitCode = 1;
            report.Conclusion = "open_failed_or_no_report";
            report.Runtime = runtime;
            return report;
        }

        report.Runtime = runtime;
        report.ExitCode = 0;
        report.Conclusion = runtime.ReadSuccessCount > 0 ? "report_received" : "no_report_received";
        write("HidConsoleProbe END");
        return report;
    }

    private async Task<bool> TryOpenAndReadAsync(
        HidConsoleProbeDevice device,
        CancellationToken token,
        HidConsoleProbeRuntime runtime,
        HidConsoleProbeReport report)
    {
        try
        {
            if (!device.TryOpen(out var stream))
            {
                runtime.LastException = "TryOpen failed";
                write($"OPEN_FAIL path={device.DevicePath}");
                return false;
            }

            using (stream)
            {
                runtime.OpenSucceeded = true;
                runtime.ReadLoopStarted = true;
                runtime.OpenedPath = device.DevicePath;
                runtime.OpenedVid = device.VendorId;
                runtime.OpenedPid = device.ProductId;
                runtime.OpenedProductName = device.ProductName;
                runtime.OpenedManufacturer = device.Manufacturer;
                runtime.OpenedSerial = device.SerialNumber;
                runtime.OpenedUsagePage = device.UsagePage;
                runtime.OpenedUsage = device.Usage;
                runtime.OpenedMaxInputReportLength = device.MaxInputReportLength;
                runtime.OpenedMaxOutputReportLength = device.MaxOutputReportLength;
                runtime.OpenedMaxFeatureReportLength = device.MaxFeatureReportLength;

                write($"OPEN_OK path={device.DevicePath}");
                write($"OPEN_INFO productName={device.ProductName} manufacturer={device.Manufacturer} serial={device.SerialNumber} usagePage=0x{device.UsagePage:X4} usage=0x{device.Usage:X4} in={device.MaxInputReportLength} out={device.MaxOutputReportLength} feature={device.MaxFeatureReportLength}");

                try
                {
                    stream.ReadTimeout = options.TimeoutMs;
                }
                catch
                {
                    // HidSharp implementations may not expose a writable timeout in the same way.
                }

                var reportBuffer = new byte[Math.Max(64, device.MaxInputReportLength)];
                var stopwatch = Stopwatch.StartNew();
                var sampleIndex = 0;

                while (!token.IsCancellationRequested)
                {
                    if (stopwatch.Elapsed >= TimeSpan.FromSeconds(options.DurationSec))
                        break;

                    runtime.ReadAttemptCount++;

                    int length;
                    try
                    {
                        length = await stream.ReadAsync(reportBuffer, 0, reportBuffer.Length, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (TimeoutException)
                    {
                        runtime.ReadTimeoutCount++;
                        runtime.LastException = "TimeoutException";
                        continue;
                    }
                    catch (Exception ex)
                    {
                        runtime.LastException = $"{ex.GetType().Name}: {ex.Message}";
                        write($"READ_FAIL path={device.DevicePath} error={runtime.LastException}");
                        break;
                    }

                    if (length <= 0)
                        break;

                    runtime.ReadSuccessCount++;
                    runtime.LastReadAtUtc = DateTimeOffset.Now;
                    sampleIndex++;

                    var hex = ToHex(reportBuffer, length);
                    var ascii = ToAscii(reportBuffer, length);
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    write($"[{timestamp}] len={length} data={hex} ascii={ascii}");

                    report.Samples.Add(new HidConsoleProbeSample
                    {
                        Index = sampleIndex,
                        Timestamp = DateTimeOffset.Now,
                        Length = length,
                        Hex = hex,
                        Ascii = ascii,
                    });
                }

                write($"READ_SUMMARY openSucceeded={runtime.OpenSucceeded} readLoopStarted={runtime.ReadLoopStarted} readAttemptCount={runtime.ReadAttemptCount} readSuccessCount={runtime.ReadSuccessCount} readTimeoutCount={runtime.ReadTimeoutCount} lastException={runtime.LastException}");
                return true;
            }
        }
        catch (Exception ex)
        {
            runtime.LastException = $"{ex.GetType().Name}: {ex.Message}";
            write($"RUN_FAIL error={runtime.LastException}");
            return false;
        }
    }

    private static List<HidConsoleProbeDevice> EnumerateDevices()
    {
        var devices = DeviceList.Local.GetHidDevices()
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

        return devices;
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(HidConsoleProbeRunner).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
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

    private static string ToHex(byte[] buffer, int length)
    {
        return BitConverter.ToString(buffer, 0, length).Replace("-", " ", StringComparison.Ordinal);
    }

    private static string ToAscii(byte[] buffer, int length)
    {
        return Ascii.GetString(buffer, 0, length).Trim('\0', '\r', '\n', ' ');
    }
}

sealed class HidConsoleProbeOptions
{
    public int Vid { get; private init; } = 0x2E8A;
    public int Pid { get; private init; } = 0x4020;
    public string VidHex { get; private init; } = "2E8A";
    public string PidHex { get; private init; } = "4020";
    public int Index { get; private init; }
    public int TimeoutMs { get; private init; } = 500;
    public int DurationSec { get; private init; } = 30;
    public string OutputDirectory { get; private init; } = Path.Combine(Environment.CurrentDirectory, "tmp", "hid-console-probe");

    public static HidConsoleProbeOptions Parse(string[] args)
    {
        var options = new HidConsoleProbeOptions();
        var vid = options.Vid;
        var pid = options.Pid;
        var vidHex = options.VidHex;
        var pidHex = options.PidHex;
        var index = options.Index;
        var timeoutMs = options.TimeoutMs;
        var durationSec = options.DurationSec;
        string? outputDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (args[i])
            {
                case "--vid":
                    vidHex = Next() ?? vidHex;
                    vid = ParseHex16(vidHex) ?? vid;
                    break;
                case "--pid":
                    pidHex = Next() ?? pidHex;
                    pid = ParseHex16(pidHex) ?? pid;
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
            }
        }

        return new HidConsoleProbeOptions
        {
            Vid = vid,
            Pid = pid,
            VidHex = vidHex,
            PidHex = pidHex,
            Index = index,
            TimeoutMs = timeoutMs,
            DurationSec = durationSec,
            OutputDirectory = string.IsNullOrWhiteSpace(outputDir)
                ? Path.Combine(Environment.CurrentDirectory, "tmp", "hid-console-probe")
                : outputDir,
        };
    }

    private static int? ParseHex16(string value)
    {
        var s = value.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

sealed class HidConsoleProbeReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;
    public HidConsoleProbeOptions Options { get; init; } = new();
    public int FilteredDeviceCount { get; set; }
    public HidConsoleProbeDevice? SelectedDevice { get; set; }
    public string SelectedPath { get; set; } = string.Empty;
    public HidConsoleProbeRuntime Runtime { get; set; } = new();
    public List<HidConsoleProbeDevice> Devices { get; set; } = [];
    public List<HidConsoleProbeSample> Samples { get; set; } = [];
    public int ExitCode { get; set; }
    public string Conclusion { get; set; } = string.Empty;
}

sealed class HidConsoleProbeRuntime
{
    public string SelectedPath { get; set; } = string.Empty;
    public int SelectedVid { get; set; }
    public int SelectedPid { get; set; }
    public string OpenedPath { get; set; } = string.Empty;
    public int OpenedVid { get; set; }
    public int OpenedPid { get; set; }
    public string OpenedProductName { get; set; } = string.Empty;
    public string OpenedManufacturer { get; set; } = string.Empty;
    public string OpenedSerial { get; set; } = string.Empty;
    public int OpenedUsagePage { get; set; }
    public int OpenedUsage { get; set; }
    public int OpenedMaxInputReportLength { get; set; }
    public int OpenedMaxOutputReportLength { get; set; }
    public int OpenedMaxFeatureReportLength { get; set; }
    public bool OpenSucceeded { get; set; }
    public bool ReadLoopStarted { get; set; }
    public int ReadAttemptCount { get; set; }
    public int ReadSuccessCount { get; set; }
    public int ReadTimeoutCount { get; set; }
    public DateTimeOffset? LastReadAtUtc { get; set; }
    public string LastException { get; set; } = string.Empty;
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
    {
        return $"path={DevicePath}, productName={ProductName}, manufacturer={Manufacturer}, serial={SerialNumber}, usagePage=0x{UsagePage:X4}, usage=0x{Usage:X4}, maxInputReportLength={MaxInputReportLength}, maxOutputReportLength={MaxOutputReportLength}, maxFeatureReportLength={MaxFeatureReportLength}";
    }
}

sealed class HidConsoleProbeSample
{
    public int Index { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int Length { get; init; }
    public string Hex { get; init; } = string.Empty;
    public string Ascii { get; init; } = string.Empty;
}
