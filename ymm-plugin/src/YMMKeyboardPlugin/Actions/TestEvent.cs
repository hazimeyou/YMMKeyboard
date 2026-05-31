using System.Windows;

namespace YMMKeyboardPlugin.Actions
{
    public static class TestEvent
    {
        public static void Execute(string switchName, string? parameter)
        {
            MessageBox.Show(
                $"テストイベント\nSwitch: {switchName}\nParameter: {parameter ?? "(null)"}",
                "テストイベント");
        }
    }
}
