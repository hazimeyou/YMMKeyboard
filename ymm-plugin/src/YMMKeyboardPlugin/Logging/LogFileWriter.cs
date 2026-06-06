using System.Text;

namespace YMMKeyboardPlugin.Logging;

internal static class LogFileWriter
{
    public static void AppendLine(string filePath, string line)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
    }
}
