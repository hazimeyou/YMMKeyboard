using System.Diagnostics;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Logging;

public static class YMMKeyboardLogger
{
    private static readonly object sync = new();

    public static bool IsEnabled => YMMKeyboardSettings.IsRuntimeLoggingEnabled;

    public static string LogDirectoryPath => PluginLogger.LogDirectoryPath;

    public static string CurrentLogFilePath => Path.Combine(LogDirectoryPath, $"ymmkeyboard-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string eventName, string message) => Write("INFO", eventName, message);

    public static void Warn(string eventName, string message) => Write("WARN", eventName, message);

    public static void Error(string eventName, string message) => Write("ERROR", eventName, message);

    public static void Error(string eventName, Exception exception) => Write("ERROR", eventName, exception.Message, exception);

    public static void Error(string eventName, string message, Exception exception) => Write("ERROR", eventName, message, exception);

    public static void Write(string eventName, string message) => Write("INFO", eventName, message);

    public static bool OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectoryPath,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int ClearRuntimeLogs()
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            var files = Directory.GetFiles(LogDirectoryPath, "ymmkeyboard-*.log");
            var deleted = 0;

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // best effort
                }
            }

            return deleted;
        }
        catch
        {
            return 0;
        }
    }

    public static void PluginLoaded() => Info("PluginLoaded", "Plugin loaded.");

    public static void DeviceSearchStarted(string message) => Info("DeviceSearchStarted", message);

    public static void DeviceConnected(string message) => Info("DeviceConnected", message);

    public static void DeviceDisconnected(string message) => Info("DeviceDisconnected", message);

    public static void Exception(string message, Exception exception) => Error("Exception", message, exception);

    private static void Write(string level, string eventName, string message, Exception? exception = null)
    {
        if (!IsEnabled)
            return;

        try
        {
            var entry = new YMMKeyboardLogEvent(
                DateTime.Now,
                level,
                eventName,
                message,
                exception?.GetType().Name,
                exception?.Message);

            var line = entry.ToLine();
            Debug.WriteLine(line);

            lock (sync)
            {
                LogFileWriter.AppendLine(CurrentLogFilePath, line);
            }
        }
        catch
        {
            // logging must never break plugin execution
        }
    }
}
