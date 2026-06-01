using System.Text;
using System.Text.RegularExpressions;
using HidSharp;

var options = ProbeOptions.Parse(args);
var logDir = options.LogDirectory;
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, $"hidprobe_{DateTime.Now:yyyyMMdd_HHmmss}.log");
using var log = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };

void Write(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
    Console.WriteLine(line);
    log.WriteLine(line);
}

Write("HidProbeConsole START");
Write($"Options: vid={options.VidHex ?? "(none)"}, pid={options.PidHex ?? "(none)"}, durationSec={options.DurationSec}, includeAll={options.IncludeAll}, dumpAll={options.DumpAll}, dumpAsciiOnly={options.DumpAsciiOnly}");

var devices = DeviceList.Local.GetHidDevices().ToArray();
Write($"Enumerated devices: {devices.Length}");

var candidates = devices.Where(d => options.Match(d)).ToArray();
Write($"Filtered candidates: {candidates.Length}");
foreach (var d in candidates)
{
    Write($"Candidate: {Describe(d)}");
}

if (candidates.Length == 0)
{
    Write("No candidate devices. EXIT");
    return;
}

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSec));
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var tasks = candidates.Select(d => Task.Run(() => ReadLoop(d, options, Write, cts.Token), cts.Token)).ToArray();
try
{
    await Task.WhenAll(tasks);
}
catch (OperationCanceledException)
{
}

Write("HidProbeConsole END");
Write($"Log file: {logPath}");

static string Describe(HidDevice d)
{
    return $"VID:PID={d.VendorID:X4}:{d.ProductID:X4}, in={SafeInt(() => d.GetMaxInputReportLength())}, out={SafeInt(() => d.GetMaxOutputReportLength())}, product={SafeStr(() => d.GetProductName())}, maker={SafeStr(() => d.GetManufacturer())}, path={d.DevicePath}";
}

static int SafeInt(Func<int> f)
{
    try { return f(); } catch { return -1; }
}

static string SafeStr(Func<string?> f)
{
    try { return f() ?? string.Empty; } catch { return string.Empty; }
}

static async Task ReadLoop(HidDevice d, ProbeOptions options, Action<string> write, CancellationToken token)
{
    var eventPattern = new Regex(@"(?:YMMK:)?(?<uid>[0-9a-fA-F]+):(?<state>[PR]):SW_(?<switch>\d+)", RegexOptions.Compiled);

    if (!d.TryOpen(out var stream))
    {
        write($"OPEN_FAIL: {Describe(d)}");
        return;
    }

    using (stream)
    {
        write($"OPEN_OK: {Describe(d)}");
        var reportSize = Math.Max(64, SafeInt(() => d.GetMaxInputReportLength()));
        var buffer = new byte[reportSize];
        var sampleIndex = 0;

        while (!token.IsCancellationRequested)
        {
            int len;
            try
            {
                len = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception ex)
            {
                write($"READ_FAIL: {d.DevicePath} | {ex.GetType().Name}: {ex.Message}");
                break;
            }

            if (len <= 0)
                break;

            sampleIndex++;
            var head = options.DumpAll ? len : Math.Min(len, 32);
            var hex = BitConverter.ToString(buffer, 0, head);
            var asciiRaw = Encoding.ASCII.GetString(buffer, 0, len).Trim('\0', '\r', '\n', ' ');
            if (options.DumpAsciiOnly)
                write($"RAW[{sampleIndex}] len={len} ascii={asciiRaw}");
            else
                write($"RAW[{sampleIndex}] len={len} hex={hex} ascii={asciiRaw}");

            var m = eventPattern.Match(asciiRaw);
            if (m.Success)
            {
                write($"PARSED uid={m.Groups["uid"].Value.ToLowerInvariant()} state={m.Groups["state"].Value} switch={m.Groups["switch"].Value}");
            }
        }
    }
}

sealed class ProbeOptions
{
    public int? Vid { get; private init; }
    public int? Pid { get; private init; }
    public string? VidHex { get; private init; }
    public string? PidHex { get; private init; }
    public bool IncludeAll { get; private init; }
    public bool DumpAll { get; private init; }
    public bool DumpAsciiOnly { get; private init; }
    public int DurationSec { get; private init; } = 60;
    public string LogDirectory { get; private init; } = Path.Combine(Environment.CurrentDirectory, "tmp", "hidprobe");

    public bool Match(HidDevice d)
    {
        if (IncludeAll)
            return true;
        if (Vid.HasValue && d.VendorID != Vid.Value)
            return false;
        if (Pid.HasValue && d.ProductID != Pid.Value)
            return false;
        return true;
    }

    public static ProbeOptions Parse(string[] args)
    {
        int? vid = null;
        int? pid = null;
        string? vidHex = null;
        string? pidHex = null;
        var includeAll = false;
        var dumpAll = false;
        var dumpAsciiOnly = false;
        var duration = 60;
        string? logDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? next() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "--vid":
                    vidHex = next();
                    vid = ParseHex16(vidHex);
                    break;
                case "--pid":
                    pidHex = next();
                    pid = ParseHex16(pidHex);
                    break;
                case "--all":
                    includeAll = true;
                    break;
                case "--duration":
                    duration = int.TryParse(next(), out var d) ? Math.Max(5, d) : 60;
                    break;
                case "--logdir":
                    logDir = next();
                    break;
                case "--dump-all":
                    dumpAll = true;
                    break;
                case "--ascii-only":
                    dumpAsciiOnly = true;
                    break;
            }
        }

        return new ProbeOptions
        {
            Vid = vid,
            Pid = pid,
            VidHex = vidHex,
            PidHex = pidHex,
            IncludeAll = includeAll,
            DumpAll = dumpAll,
            DumpAsciiOnly = dumpAsciiOnly,
            DurationSec = duration,
            LogDirectory = string.IsNullOrWhiteSpace(logDir)
                ? Path.Combine(Environment.CurrentDirectory, "tmp", "hidprobe")
                : logDir!,
        };
    }

    private static int? ParseHex16(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : null;
    }
}
