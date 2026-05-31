using YMMKeyboardPlugin.Key;

namespace YMMKeyboardPlugin;

public interface IKeyboardLink : IDisposable
{
    event Action<SerialKeyboardDevice>? DeviceDetected;
    event Action<SerialKeyboardDevice, KeyEvent>? KeyEventReceived;
    IReadOnlyList<string> KnownUids { get; }
    void Start();
}
