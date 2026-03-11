using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Views
{
    public partial class KeyboardMappingWindow : Window
    {
        private const string UiScopeLabel = "UIキーボード";

        private readonly YMMKeyboardSettings settings;
        private readonly ObservableCollection<SwitchAssignmentItem> items = new();
        private readonly ObservableCollection<string> scopeOptions = new();
        private string currentScope = UiScopeLabel;
        private SwitchAssignmentItem? selectedAssignment;
        private bool isUpdatingSelection;

        public KeyboardMappingWindow()
        {
            settings = YMMKeyboardSettings.Current;
            InitializeComponent();
            ScopeComboBox.ItemsSource = scopeOptions;
            ActionComboBox.ItemsSource = MappingConverter.AvailableActions;
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

            SelectAssignment(items.FirstOrDefault()?.SwitchName ?? string.Empty);
            RefreshKeyboardButtons();
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

        private void SelectAssignment(string switchName)
        {
            selectedAssignment = items.FirstOrDefault(item => item.SwitchName == switchName);
            isUpdatingSelection = true;

            if (selectedAssignment is null)
            {
                SelectedSwitchTextBlock.Text = "未選択";
                ActionComboBox.SelectedItem = null;
                ParameterTextBox.Text = string.Empty;
                SelectionHintTextBlock.Text = "キーを選ぶとここに設定内容が表示されます。";
            }
            else
            {
                SelectedSwitchTextBlock.Text = selectedAssignment.SwitchName;
                ActionComboBox.SelectedItem = selectedAssignment.SelectedActionName;
                ParameterTextBox.Text = selectedAssignment.Parameter;
                UpdateSelectionHint();
            }

            isUpdatingSelection = false;
            RefreshKeyboardButtons();
        }

        private void UpdateSelectionHint()
        {
            if (selectedAssignment is null)
            {
                SelectionHintTextBlock.Text = "キーを選ぶとここに設定内容が表示されます。";
                return;
            }

            var parameterText = string.IsNullOrWhiteSpace(selectedAssignment.Parameter)
                ? "なし"
                : selectedAssignment.Parameter;

            SelectionHintTextBlock.Text =
                $"選択中: {selectedAssignment.SwitchName}\n動作: {selectedAssignment.SelectedActionName}\nパラメータ: {parameterText}";
        }

        private void RefreshKeyboardButtons()
        {
            foreach (var button in FindKeyboardButtons(KeyboardSurfaceGrid))
            {
                var switchName = button.Tag as string ?? string.Empty;
                var assignment = items.FirstOrDefault(item => item.SwitchName == switchName);
                if (assignment is null)
                    continue;

                var isSelected = selectedAssignment?.SwitchName == switchName;
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#D9E9FF" : "#F5F3ED"));
                button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#4A84D8" : "#C7B9A5"));
                button.Content = CreateButtonContent(switchName, assignment);
            }
        }

        private static object CreateButtonContent(string switchName, SwitchAssignmentItem assignment)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = switchName,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = GetActionDisplayName(assignment.SelectedActionName),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });
            return panel;
        }

        private static string GetActionDisplayName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName) || actionName == MappingConverter.NoneActionName)
                return "未設定";

            return actionName;
        }

        private static IEnumerable<Button> FindKeyboardButtons(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Button button && button.Tag is string)
                    yield return button;

                foreach (var nested in FindKeyboardButtons(child))
                    yield return nested;
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

        private void SwitchButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string switchName)
                return;

            SelectAssignment(switchName);
        }

        private void ActionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingSelection || selectedAssignment is null)
                return;

            selectedAssignment.SelectedActionName = ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName;
            UpdateSelectionHint();
            SaveAssignments();
            RefreshKeyboardButtons();
        }

        private void ParameterTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingSelection || selectedAssignment is null)
                return;

            selectedAssignment.Parameter = ParameterTextBox.Text;
            UpdateSelectionHint();
            SaveAssignments();
        }

        private void TestSelectedButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (selectedAssignment is null)
                return;

            SaveAssignments();
            MappingConverter.ExecuteAction(
                selectedAssignment.SelectedActionName,
                selectedAssignment.Parameter,
                selectedAssignment.SwitchName,
                currentScope);
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
