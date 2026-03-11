using System.Diagnostics;
using System.Windows.Controls;
using YMMKeyboardPlugin.Mapping;

namespace YMMKeyboardPlugin.Views
{
    public partial class KeyboardView : UserControl
    {
        public KeyboardView()
        {
            InitializeComponent();
            Debug.WriteLine("[KeyboardView] ctor");
        }

        private void SwitchButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var switchName = button.Tag as string ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(switchName))
                return;

            MappingConverter.ExecuteUiSwitch(switchName);
        }

        /*
        // 旧実装: 各ボタンごとに個別ハンドラを持っていたが、
        // 現在は SwitchButton_OnClick に統合している。
        public void SW01(object sender, System.Windows.RoutedEventArgs e) => MappingConverter.SW01();
        private void SW02(object sender, System.Windows.RoutedEventArgs e) => MappingConverter.SW02();
        private void SW03(object sender, System.Windows.RoutedEventArgs e) => MappingConverter.SW03();
        */
    }
}
