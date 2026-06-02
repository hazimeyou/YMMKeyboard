namespace YMMKeyboard.DeviceInspector;

internal sealed class DeviceInspectorOptions
{
    public string LogDirectory { get; init; } = Path.Combine(Environment.CurrentDirectory, "tmp", "device-inspector");
    public bool EmitJson { get; init; }
    public string? OutputPath { get; init; }
    public bool ProbeSerial { get; init; }
    public int SerialProbeDurationMs { get; init; } = 1500;

    public static DeviceInspectorOptions Parse(string[] args)
    {
        string? logDir = null;
        string? outputPath = null;
        var probeSerial = false;
        var emitJson = false;
        var probeMs = 1500;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--logdir":
                    logDir = Next();
                    break;
                case "--json":
                    emitJson = true;
                    break;
                case "--output":
                    outputPath = Next();
                    break;
                case "--probe-serial":
                    probeSerial = true;
                    break;
                case "--serial-ms":
                    if (int.TryParse(Next(), out var parsedMs))
                        probeMs = Math.Max(250, parsedMs);
                    break;
            }
        }

        return new DeviceInspectorOptions
        {
            LogDirectory = string.IsNullOrWhiteSpace(logDir)
                ? Path.Combine(Environment.CurrentDirectory, "tmp", "device-inspector")
                : logDir!,
            EmitJson = emitJson,
            OutputPath = outputPath,
            ProbeSerial = probeSerial,
            SerialProbeDurationMs = probeMs,
        };
    }
}
