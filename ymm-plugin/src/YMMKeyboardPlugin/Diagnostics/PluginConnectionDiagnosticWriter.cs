using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Diagnostics;

public static class PluginConnectionDiagnosticWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ResolveDiagnosticsDirectoryPath()
    {
        var envPath = Environment.GetEnvironmentVariable("YMMK_PLUGIN_DIAGNOSTICS_DIR");
        if (!string.IsNullOrWhiteSpace(envPath))
            return envPath;

        return Path.Combine(AppContext.BaseDirectory, "tmp", "plugin-diagnostics");
    }

    public static string GetDefaultOutputPath()
    {
        return Path.Combine(ResolveDiagnosticsDirectoryPath(), $"plugin-connection_{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    public static void Write(PluginConnectionDiagnosticReport report, string? outputPath = null)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(outputPath) ? GetDefaultOutputPath() : outputPath!;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(report, JsonOptions);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            PluginLogger.Info("PluginConnectionDiagnostics", $"JSON report written: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            PluginLogger.Error("PluginConnectionDiagnostics", "Failed to write JSON report.", ex);
        }
    }
}
