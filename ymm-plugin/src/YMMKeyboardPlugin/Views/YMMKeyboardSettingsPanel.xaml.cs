using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YMMKeyboardPlugin.Hid;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Views
{
    public partial class YMMKeyboardSettingsPanel : UserControl
    {
        private readonly YMMKeyboardSettings settings;
        private bool serialPortSupported = true;

        public YMMKeyboardSettingsPanel()
            : this(YMMKeyboardSettings.Current)
        {
        }

        public YMMKeyboardSettingsPanel(YMMKeyboardSettings settings)
        {
            this.settings = settings;
            InitializeComponent();
            LoadConnectionSettings();
            LoadPorts();
            LoadStartupPorts();
        }

        private void LoadConnectionSettings()
        {
            var targetTag = settings.ConnectionMode.ToString();
            foreach (var item in ConnectionModeComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    ConnectionModeComboBox.SelectedItem = item;
                    break;
                }
            }

            HidVendorIdTextBox.Text = settings.HidVendorIdHex;
            HidProductIdTextBox.Text = settings.HidProductIdHex;
        }

        private void LoadPorts()
        {
            var ports = GetPortNamesSafe()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(settings.PortName) && !ports.Contains(settings.PortName, StringComparer.OrdinalIgnoreCase))
                ports.Insert(0, settings.PortName);

            PortComboBox.ItemsSource = ports;
            PortComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.PortName)
                ? null
                : ports.FirstOrDefault(name => string.Equals(name, settings.PortName, StringComparison.OrdinalIgnoreCase));

            PortStatusTextBlock.Text = (!serialPortSupported && ports.Count == 0)
                ? "この環境ではシリアルポート列挙APIが利用できません。"
                : ports.Count == 0
                    ? "利用可能なCOMポートが見つかりません。接続後に再読み込みしてください。"
                    : string.IsNullOrWhiteSpace(settings.PortName)
                        ? "接続するCOMポートを選択してください。"
                        : $"現在の設定: {settings.PortName}";
        }

        private IEnumerable<string> GetPortNamesSafe()
        {
            try
            {
                serialPortSupported = true;
                var ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                    return ports;

                // 一部環境では System.IO.Ports が空を返すことがあるため、
                // Windows のシリアルポート情報をレジストリから補完する。
                return GetPortNamesFromRegistry();
            }
            catch (PlatformNotSupportedException)
            {
                serialPortSupported = false;
                return GetPortNamesFromRegistry();
            }
            catch
            {
                return GetPortNamesFromRegistry();
            }
        }

        private static IEnumerable<string> GetPortNamesFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key is null)
                    return Array.Empty<string>();

                var ports = key.GetValueNames()
                    .Select(name => key.GetValue(name)?.ToString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return ports;
            }
            catch
            {
                return Array.Empty<string>();
            }
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

        private void ConnectionModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionModeComboBox.SelectedItem is not ComboBoxItem selected)
                return;

            if (!Enum.TryParse<ConnectionMode>(selected.Tag?.ToString(), ignoreCase: true, out var mode))
                return;

            settings.UpdateConnectionMode(mode);
            PortStatusTextBlock.Text = $"接続モードを {selected.Content} に設定しました。";
        }

        private void ApplyHidFilter_OnClick(object sender, RoutedEventArgs e)
        {
            settings.UpdateHidFilter(HidVendorIdTextBox.Text, HidProductIdTextBox.Text);
            HidVendorIdTextBox.Text = settings.HidVendorIdHex;
            HidProductIdTextBox.Text = settings.HidProductIdHex;
            PortStatusTextBlock.Text = $"HIDフィルタを設定しました。VID={settings.HidVendorIdHex}, PID={settings.HidProductIdHex}";
        }

        private void Connect_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(settings.PortName))
            {
                PortStatusTextBlock.Text = "先に接続するCOMポートを選択してください。";
                return;
            }

            settings.RequestConnection();
            PortStatusTextBlock.Text = $"{settings.PortName} への接続を開始しました。キー入力でUIDを自動登録します。";
        }

        private void Disconnect_OnClick(object sender, RoutedEventArgs e)
        {
            settings.RequestDisconnection();
            PortStatusTextBlock.Text = "シリアル接続の切断を要求しました。";
        }

        private void AddStartupPort_OnClick(object sender, RoutedEventArgs e)
        {
            if (PortComboBox.SelectedItem is not string selectedPort)
            {
                PortStatusTextBlock.Text = "追加する起動時接続ポートを選択してください。";
                return;
            }

            settings.AddStartupPort(selectedPort);
            LoadStartupPorts();
            PortStatusTextBlock.Text = $"{selectedPort} を起動時接続ポートに追加しました。";
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
            PortStatusTextBlock.Text = $"{selectedPort} を起動時接続ポートから削除しました。";
        }

        private void OpenMappingWindow_OnClick(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var window = new KeyboardMappingWindow();

            if (owner is not null)
                window.Owner = owner;

            window.ShowDialog();
        }

        private void HidProbe_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var devices = HidDeviceProbe.EnumerateAll();
                var report = HidDeviceProbe.BuildReportText(devices);
                var reportPath = Path.Combine(PluginLogger.LogDirectoryPath, $"hid_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                Directory.CreateDirectory(PluginLogger.LogDirectoryPath);
                File.WriteAllText(reportPath, report);

                PluginLogger.Info("YMMKeyboardSettingsPanel", $"HID report generated. count={devices.Count}, path={reportPath}");
                PortStatusTextBlock.Text = $"HID診断を出力しました: {reportPath}";
                MessageBox.Show($"HID診断を出力しました。\n{reportPath}", "HID診断");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("YMMKeyboardSettingsPanel", "Failed to generate HID report.", ex);
                MessageBox.Show($"HID診断の出力に失敗しました。\n{ex.Message}", "HID診断");
            }
        }
    }
}
