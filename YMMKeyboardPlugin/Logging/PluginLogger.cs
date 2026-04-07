using System.Diagnostics;
using System.Text;

namespace YMMKeyboardPlugin.Logging;

public static class PluginLogger
{
    private static readonly object sync = new();
    private static bool resetDone;

    public static string LogDirectoryPath
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "logs");
        }
    }

    public static string CurrentLogFilePath
    {
        get
        {
            var fileName = $"YMMKeyboardPlugin_{DateTime.Now:yyyyMMdd}.log";
            return Path.Combine(LogDirectoryPath, fileName);
        }
    }

    public static void Info(string category, string message) => Write("INFO", category, message, null);

    public static void Warn(string category, string message) => Write("WARN", category, message, null);

    public static void Error(string category, string message, Exception? ex = null) => Write("ERROR", category, message, ex);

    public static void ResetOnStartup()
    {
        lock (sync)
        {
            if (resetDone)
                return;

            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                if (File.Exists(CurrentLogFilePath))
                    File.Delete(CurrentLogFilePath);
            }
            catch
            {
                // best effort
            }
            finally
            {
                resetDone = true;
            }
        }
    }

    private static void Write(string level, string category, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(level);
            sb.Append("] ");
            sb.Append('[');
            sb.Append(category);
            sb.Append("] ");
            sb.Append(message);
            if (ex is not null)
            {
                sb.Append(" | ");
                sb.Append(ex.GetType().Name);
                sb.Append(": ");
                sb.Append(ex.Message);
            }

            var line = sb.ToString();
            Debug.WriteLine(line);

            lock (sync)
            {
                Directory.CreateDirectory(LogDirectoryPath);
                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // logging should never break plugin execution
        }
    }
}
