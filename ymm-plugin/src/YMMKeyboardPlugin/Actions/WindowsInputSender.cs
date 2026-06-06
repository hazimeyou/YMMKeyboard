using System.Runtime.InteropServices;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Actions;

internal static class WindowsInputSender
{
    private const ushort VkSpace = 0x20;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    public static string? LastFailureDetail { get; private set; }

    public static bool SendKeyTap(ushort virtualKey)
    {
        LastFailureDetail = null;
        var inputs = new[]
        {
            CreateKeyInput(virtualKey, keyUp: false),
            CreateKeyInput(virtualKey, keyUp: true),
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var lastError = Marshal.GetLastWin32Error();
            LastFailureDetail = $"SendInput failed. vk=0x{virtualKey:X2}, sent={sent}/{inputs.Length}, lastError={lastError}";
            PluginLogger.Warn("WindowsInputSender", LastFailureDetail);
            return false;
        }

        return true;
    }

    public static bool SendSpaceTap() => SendKeyTap(VkSpace);

    private static INPUT CreateKeyInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? KeyeventfKeyup : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
