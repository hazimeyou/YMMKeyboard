using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin
{
    public partial class KeyboardMappingWindow : Window
    {
        private readonly YMMKeyboardSettings settings;
        private readonly ObservableCollection<SwitchAssignmentItem> items = new();
        private readonly ObservableCollection<string> knownUids = new();
        private string currentUid = string.Empty;

        public KeyboardMappingWindow()
        {
            settings = YMMKeyboardSettings.Current;
            InitializeComponent();
            AssignmentsGrid.ItemsSource = items;
            UidComboBox.ItemsSource = knownUids;
            LoadKnownUids();
        }

        private void LoadKnownUids()
        {
            knownUids.Clear();
            foreach (var uid in settings.GetKnownDeviceUids())
                knownUids.Add(uid);

            var preferredUid = settings.GetManualTargetUid();
            var initialUid = !string.IsNullOrWhiteSpace(preferredUid)
                ? preferredUid
                : knownUids.FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(initialUid))
            {
                currentUid = initialUid;
                UidComboBox.Text = currentUid;
                settings.SetManualTargetUid(currentUid);
                LoadAssignments(currentUid);
                StatusTextBlock.Text = $"UID {currentUid} の割り当てを編集中です。";
            }
            else
            {
                items.Clear();
                currentUid = string.Empty;
                StatusTextBlock.Text = "まだUIDが登録されていません。機器を接続するか、UIDを入力して追加してください。";
            }
        }

        private void LoadAssignments(string uid)
        {
            items.Clear();
            foreach (var item in SwitchLayout.All)
            {
                var config = settings.GetButtonConfig(uid, item.SwitchName);
                items.Add(new SwitchAssignmentItem
                {
                    SwitchName = item.SwitchName,
                    SelectedActionName = config.ActionName,
                    Parameter = config.Parameter,
                });
            }
        }

        private void SaveAssignments()
        {
            if (string.IsNullOrWhiteSpace(currentUid))
                return;

            foreach (var item in items)
            {
                settings.SetButtonConfig(currentUid, item.SwitchName, new ButtonConfig
                {
                    ActionName = item.SelectedActionName,
                    Parameter = item.Parameter,
                });
            }
        }

        private void AddOrSelectUid_OnClick(object sender, RoutedEventArgs e)
        {
            var uid = UidComboBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(uid))
            {
                StatusTextBlock.Text = "UIDを入力してください。";
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentUid))
                SaveAssignments();

            settings.RegisterKnownDeviceUid(uid);
            settings.SetManualTargetUid(uid);
            if (!knownUids.Contains(uid))
                knownUids.Add(uid);

            currentUid = uid;
            UidComboBox.Text = uid;
            LoadAssignments(uid);
            StatusTextBlock.Text = $"UID {uid} の割り当てを編集中です。";
        }

        private void TestButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: SwitchAssignmentItem item })
                return;

            SaveAssignments();
            MappingConverter.ExecuteAction(item.SelectedActionName, item.Parameter, item.SwitchName, currentUid);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveAssignments();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveAssignments();
            base.OnClosing(e);
        }
    }
}
