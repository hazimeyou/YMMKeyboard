using System.Windows;
using System.Windows.Controls;
using YMMKeyboardPlugin.Hid;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Views
{
    public partial class YMMKeyboardSettingsPanel : UserControl
    {
        private readonly YMMKeyboardSettings settings;
        private bool isInitializing;

        public YMMKeyboardSettingsPanel()
            : this(YMMKeyboardSettings.Current)
        {
        }

        public YMMKeyboardSettingsPanel(YMMKeyboardSettings settings)
        {
            this.settings = settings;
            isInitializing = true;
            InitializeComponent();
            try
            {
                LoadConnectionSettings();
            }
            finally
            {
                isInitializing = false;
            }
        }

        private void LoadConnectionSettings()
        {
            HidVendorIdTextBox.Text = settings.HidVendorIdHex;
            HidProductIdTextBox.Text = settings.HidProductIdHex;
            HidProductNameFilterTextBox.Text = settings.HidProductNameFilter;
            HidManufacturerFilterTextBox.Text = settings.HidManufacturerFilter;
            RuntimeLoggingCheckBox.IsChecked = settings.RuntimeLoggingEnabled;

            var rotarySensitivityTag = Math.Clamp(settings.RotarySensitivity, 1, 4).ToString();
            foreach (var item in RotarySensitivityComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), rotarySensitivityTag, StringComparison.OrdinalIgnoreCase))
                {
                    RotarySensitivityComboBox.SelectedItem = item;
                    break;
                }
            }

            PortStatusTextBlock.Text = "USB HID を正式経路として使います。HID フィルタやロータリー感度を調整してください。";
        }

        private void ApplyHidFilter_OnClick(object sender, RoutedEventArgs e)
        {
            settings.UpdateHidFilter(
                HidVendorIdTextBox.Text,
                HidProductIdTextBox.Text,
                HidProductNameFilterTextBox.Text,
                HidManufacturerFilterTextBox.Text);
            HidVendorIdTextBox.Text = settings.HidVendorIdHex;
            HidProductIdTextBox.Text = settings.HidProductIdHex;
            HidProductNameFilterTextBox.Text = settings.HidProductNameFilter;
            HidManufacturerFilterTextBox.Text = settings.HidManufacturerFilter;
            PortStatusTextBlock.Text = $"HIDフィルタを設定しました。VID={settings.HidVendorIdHex}, PID={settings.HidProductIdHex}, 製品名={settings.HidProductNameFilter}, メーカー={settings.HidManufacturerFilter}";
        }

        private void RotarySensitivityComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RotarySensitivityComboBox.SelectedItem is not ComboBoxItem selected)
                return;

            if (!int.TryParse(selected.Tag?.ToString(), out var sensitivity))
                return;

            settings.UpdateRotarySensitivity(sensitivity);
            PortStatusTextBlock.Text = $"ロータリー感度を {selected.Content} に設定しました。";
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

        private void RuntimeLoggingCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            settings.UpdateRuntimeLoggingEnabled(RuntimeLoggingCheckBox.IsChecked == true);
            PortStatusTextBlock.Text = RuntimeLoggingCheckBox.IsChecked == true
                ? "ログを有効化しました。"
                : "ログを無効化しました。";
        }

        private void OpenLogFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (!YMMKeyboardLogger.OpenLogFolder())
                MessageBox.Show("ログフォルダーを開けませんでした。", "ログ");
        }

        private void ClearLog_OnClick(object sender, RoutedEventArgs e)
        {
            var deleted = YMMKeyboardLogger.ClearRuntimeLogs();
            PortStatusTextBlock.Text = deleted > 0
                ? $"ログを {deleted} 件削除しました。"
                : "削除できるログはありませんでした。";
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
