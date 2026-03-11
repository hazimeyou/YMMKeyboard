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
        private const string UiScopeLabel = "UIキーボード";

        private readonly YMMKeyboardSettings settings;
        private readonly ObservableCollection<SwitchAssignmentItem> items = new();
        private readonly ObservableCollection<string> scopeOptions = new();
        private string currentScope = UiScopeLabel;

        public KeyboardMappingWindow()
        {
            settings = YMMKeyboardSettings.Current;
            InitializeComponent();
            AssignmentsGrid.ItemsSource = items;
            ScopeComboBox.ItemsSource = scopeOptions;
            LoadScopes();
        }

        private static bool IsUiScope(string scope)
        {
            return string.Equals(scope, UiScopeLabel, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadScopes()
        {
            scopeOptions.Clear();
            scopeOptions.Add(UiScopeLabel);

            foreach (var uid in settings.GetKnownDeviceUids())
                scopeOptions.Add(uid);

            currentScope = UiScopeLabel;
            ScopeComboBox.Text = currentScope;
            LoadAssignments(currentScope);
            StatusTextBlock.Text = "UIキーボードの割り当てを編集中です。";
        }

        private void LoadAssignments(string scope)
        {
            items.Clear();
            foreach (var item in SwitchLayout.All)
            {
                var config = IsUiScope(scope)
                    ? settings.GetUiButtonConfig(item.SwitchName)
                    : settings.GetDeviceButtonConfig(scope, item.SwitchName);

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
            if (string.IsNullOrWhiteSpace(currentScope))
                return;

            foreach (var item in items)
            {
                var config = new ButtonConfig
                {
                    ActionName = item.SelectedActionName,
                    Parameter = item.Parameter,
                };

                if (IsUiScope(currentScope))
                    settings.SetUiButtonConfig(item.SwitchName, config);
                else
                    settings.SetDeviceButtonConfig(currentScope, item.SwitchName, config);
            }
        }

        private void OpenScope_OnClick(object sender, RoutedEventArgs e)
        {
            var scope = ScopeComboBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scope))
            {
                StatusTextBlock.Text = "対象を入力してください。";
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentScope))
                SaveAssignments();

            if (!IsUiScope(scope))
            {
                settings.RegisterKnownDeviceUid(scope);
                if (!scopeOptions.Contains(scope))
                    scopeOptions.Add(scope);
            }
            else
            {
                scope = UiScopeLabel;
            }

            currentScope = scope;
            ScopeComboBox.Text = scope;
            LoadAssignments(scope);
            StatusTextBlock.Text = IsUiScope(scope)
                ? "UIキーボードの割り当てを編集中です。"
                : $"実機 UID {scope} の割り当てを編集中です。";
        }

        private void TestButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: SwitchAssignmentItem item })
                return;

            SaveAssignments();
            MappingConverter.ExecuteAction(item.SelectedActionName, item.Parameter, item.SwitchName, currentScope);
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
