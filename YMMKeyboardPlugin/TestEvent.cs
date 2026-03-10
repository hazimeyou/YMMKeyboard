using System.Windows;

namespace YMMKeyboardPlugin
{
    public static class TestEvent
    {
        public static void Execute(string switchName, string? parameter)
        {
            var message = string.IsNullOrWhiteSpace(parameter)
                ? $"{switchName} で TestEvent を実行しました。"
                : $"{switchName} で TestEvent を実行しました。\nパラメータ: {parameter}";

            MessageBox.Show(message, "TestEvent");
        }
    }
}
