namespace YMMKeyboardPlugin.Key;

public class KeyEvent
{
    public string Uid { get; set; } = "";
    public string TransportType { get; set; } = "";
    public string SourceDevice { get; set; } = "";
    public string RawInput { get; set; } = "";
    public string InputId { get; set; } = "";
    public bool IsPressed { get; set; }
    public int SwitchId { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}

