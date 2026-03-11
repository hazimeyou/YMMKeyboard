using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin
{
    public partial class YMMKeyboardSettingsPanel : UserControl
    {
        private readonly YMMKeyboardSettings settings;

        public YMMKeyboardSettingsPanel()
            : this(YMMKeyboardSettings.Current)
        {
        }

        public YMMKeyboardSettingsPanel(YMMKeyboardSettings settings)
        {
            this.settings = settings;
            InitializeComponent();
            LoadPorts();
            LoadStartupPorts();
        }

        private void LoadPorts()
        {
            var ports = SerialPort.GetPortNames()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(settings.PortName) && !ports.Contains(settings.PortName, StringComparer.OrdinalIgnoreCase))
                ports.Insert(0, settings.PortName);

            PortComboBox.ItemsSource = ports;
            PortComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.PortName)
                ? null
                : ports.FirstOrDefault(name => string.Equals(name, settings.PortName, StringComparison.OrdinalIgnoreCase));

            PortStatusTextBlock.Text = ports.Count == 0
                ? "利用可能なCOMポートが見つかりません。機器を接続してから再読み込みしてください。"
                : string.IsNullOrWhiteSpace(settings.PortName)
                    ? "接続するCOMポートを選択してください。"
                    : $"現在の設定: {settings.PortName}";
        }

        private void LoadStartupPorts()
        {
            StartupPortsListBox.ItemsSource = null;
            StartupPortsListBox.ItemsSource = settings.GetStartupPortNames().ToList();
        }

        private void ReloadPorts_OnClick(object sender, RoutedEventArgs e)
        {
            LoadPorts();
        }

        private void PortComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PortComboBox.SelectedItem is not string selectedPort)
                return;

            settings.UpdatePortName(selectedPort);
            PortStatusTextBlock.Text = $"現在の設定: {selectedPort}";
        }

        private void Connect_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(settings.PortName))
            {
                PortStatusTextBlock.Text = "先に接続するCOMポートを選択してください。";
                return;
            }

            settings.RequestConnection();
            PortStatusTextBlock.Text = $"{settings.PortName} の監視を開始しました。キー入力を受けるとUIDを取得します。";
        }

        private void Disconnect_OnClick(object sender, RoutedEventArgs e)
        {
            settings.RequestDisconnection();
            PortStatusTextBlock.Text = "シリアル通信の監視を停止しました。";
        }

        private void AddStartupPort_OnClick(object sender, RoutedEventArgs e)
        {
            if (PortComboBox.SelectedItem is not string selectedPort)
            {
                PortStatusTextBlock.Text = "起動時接続に追加するCOMポートを選択してください。";
                return;
            }

            settings.AddStartupPort(selectedPort);
            LoadStartupPorts();
            PortStatusTextBlock.Text = $"{selectedPort} を起動時接続に追加しました。";
        }

        private void RemoveStartupPort_OnClick(object sender, RoutedEventArgs e)
        {
            if (StartupPortsListBox.SelectedItem is not string selectedPort)
            {
                PortStatusTextBlock.Text = "削除する起動時接続ポートを選択してください。";
                return;
            }

            settings.RemoveStartupPort(selectedPort);
            LoadStartupPorts();
            PortStatusTextBlock.Text = $"{selectedPort} を起動時接続から外しました。";
        }

        private void OpenMappingWindow_OnClick(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var window = new KeyboardMappingWindow();

            if (owner is not null)
                window.Owner = owner;

            window.ShowDialog();
        }
    }
}
