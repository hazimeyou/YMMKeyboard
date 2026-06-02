using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YMMKeyboard.DeviceInspector;

internal static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefaultOutputPath(string logDirectory)
    {
        return Path.Combine(logDirectory, $"device-inspector_{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    public static void Write(string path, DeviceInspectionReport report)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(report, Options);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
