using System.Text;

namespace YMMKeyboardPlugin.Logging;

internal sealed record YMMKeyboardLogEvent(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Message,
    string? ExceptionType = null,
    string? ExceptionMessage = null)
{
    public string ToLine()
    {
        var sb = new StringBuilder();
        sb.Append(Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append(" [");
        sb.Append(Level);
        sb.Append("] ");
        sb.Append('[');
        sb.Append(EventName);
        sb.Append("] ");
        sb.Append(Message);

        if (!string.IsNullOrWhiteSpace(ExceptionType) || !string.IsNullOrWhiteSpace(ExceptionMessage))
        {
            sb.Append(" | ");
            sb.Append(ExceptionType ?? "Exception");
            if (!string.IsNullOrWhiteSpace(ExceptionMessage))
            {
                sb.Append(": ");
                sb.Append(ExceptionMessage);
            }
        }

        return sb.ToString();
    }
}
