using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Diagnostics;

public static class InputDiagnosticWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ResolveDiagnosticsDirectoryPath()
    {
        var envPath = Environment.GetEnvironmentVariable("YMMK_INPUT_DIAGNOSTICS_DIR");
        if (!string.IsNullOrWhiteSpace(envPath))
            return envPath;

        return Path.Combine(AppContext.BaseDirectory, "tmp", "input-diagnostics");
    }

    public static string GetDefaultOutputPath()
    {
        return Path.Combine(ResolveDiagnosticsDirectoryPath(), $"input-diagnostics-{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    public static void Write(InputDiagnosticReport report, string? outputPath = null)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(outputPath) ? GetDefaultOutputPath() : outputPath!;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(report, JsonOptions);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            PluginLogger.Info("InputDiagnostics", $"JSON report written: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            PluginLogger.Error("InputDiagnostics", "Failed to write JSON report.", ex);
        }
    }
}
