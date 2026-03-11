using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private readonly HashSet<string> selectedCombination = new(StringComparer.OrdinalIgnoreCase);
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

        private bool IsComboMode => ComboModeCheckBox.IsChecked == true;

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

            selectedCombination.Clear();
            selectedAssignment = items.FirstOrDefault();
            LoadCurrentSelection();
            RefreshKeyboardButtons();
        }

        private void SaveCurrentSelection()
        {
            if (isUpdatingSelection)
                return;

            if (IsComboMode)
            {
                if (selectedCombination.Count < 2)
                    return;

                var combinationKey = SwitchLayout.NormalizeCombination(selectedCombination);
                var config = CreateConfigFromEditor();
                if (IsUiScope(currentScope))
                    settings.SetUiComboButtonConfig(combinationKey, config);
                else
                    settings.SetDeviceComboButtonConfig(currentScope, combinationKey, config);
                return;
            }

            if (selectedAssignment is null)
                return;

            selectedAssignment.SelectedActionName = ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName;
            selectedAssignment.Parameter = ParameterTextBox.Text;

            var singleConfig = CreateConfigFromEditor();
            if (IsUiScope(currentScope))
                settings.SetUiButtonConfig(selectedAssignment.SwitchName, singleConfig);
            else
                settings.SetDeviceButtonConfig(currentScope, selectedAssignment.SwitchName, singleConfig);
        }

        private ButtonConfig CreateConfigFromEditor()
        {
            return new ButtonConfig
            {
                ActionName = ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName,
                Parameter = ParameterTextBox.Text,
            };
        }

        private void LoadCurrentSelection()
        {
            isUpdatingSelection = true;

            if (IsComboMode)
                LoadCombinationSelection();
            else
                LoadSingleSelection();

            isUpdatingSelection = false;
            RefreshKeyboardButtons();
        }

        private void LoadSingleSelection()
        {
            if (selectedAssignment is null)
            {
                SelectedSwitchTextBlock.Text = "未選択";
                ActionComboBox.SelectedItem = null;
                ParameterTextBox.Text = string.Empty;
                SelectionHintTextBlock.Text = "キーを選ぶとここに設定内容が表示されます。";
                return;
            }

            SelectedSwitchTextBlock.Text = selectedAssignment.SwitchName;
            ActionComboBox.SelectedItem = selectedAssignment.SelectedActionName;
            ParameterTextBox.Text = selectedAssignment.Parameter;
            UpdateSelectionHint();
        }

        private void LoadCombinationSelection()
        {
            if (selectedCombination.Count == 0)
            {
                SelectedSwitchTextBlock.Text = "未選択";
                ActionComboBox.SelectedItem = MappingConverter.NoneActionName;
                ParameterTextBox.Text = string.Empty;
                SelectionHintTextBlock.Text = "複数キー編集モードでは、2つ以上のキーを選んでください。";
                return;
            }

            SelectedSwitchTextBlock.Text = SwitchLayout.FormatCombination(selectedCombination);
            if (selectedCombination.Count < 2)
            {
                ActionComboBox.SelectedItem = MappingConverter.NoneActionName;
                ParameterTextBox.Text = string.Empty;
                SelectionHintTextBlock.Text = "あと1つ以上キーを選ぶと、組み合わせの設定を編集できます。";
                return;
            }

            var combinationKey = SwitchLayout.NormalizeCombination(selectedCombination);
            var config = IsUiScope(currentScope)
                ? settings.GetUiComboButtonConfig(combinationKey)
                : settings.GetDeviceComboButtonConfig(currentScope, combinationKey);

            ActionComboBox.SelectedItem = config.ActionName;
            ParameterTextBox.Text = config.Parameter;
            UpdateSelectionHint();
        }

        private void UpdateSelectionHint()
        {
            if (IsComboMode)
            {
                var selectedText = selectedCombination.Count == 0
                    ? "なし"
                    : SwitchLayout.FormatCombination(selectedCombination);
                var parameterText = string.IsNullOrWhiteSpace(ParameterTextBox.Text) ? "なし" : ParameterTextBox.Text;
                SelectionHintTextBlock.Text =
                    $"組み合わせ: {selectedText}\n動作: {ActionComboBox.SelectedItem ?? MappingConverter.NoneActionName}\nパラメータ: {parameterText}";
                return;
            }

            if (selectedAssignment is null)
            {
                SelectionHintTextBlock.Text = "キーを選ぶとここに設定内容が表示されます。";
                return;
            }

            var singleParameter = string.IsNullOrWhiteSpace(selectedAssignment.Parameter)
                ? "なし"
                : selectedAssignment.Parameter;

            SelectionHintTextBlock.Text =
                $"選択中: {selectedAssignment.SwitchName}\n動作: {selectedAssignment.SelectedActionName}\nパラメータ: {singleParameter}";
        }

        private void RefreshKeyboardButtons()
        {
            foreach (var button in FindKeyboardButtons(KeyboardSurfaceGrid))
            {
                var switchName = button.Tag as string ?? string.Empty;
                var assignment = items.FirstOrDefault(item => item.SwitchName == switchName);
                if (assignment is null)
                    continue;

                var isSelected = IsComboMode
                    ? selectedCombination.Contains(switchName)
                    : selectedAssignment?.SwitchName == switchName;

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
            SaveCurrentSelection();

            var scope = ScopeComboBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scope))
            {
                StatusTextBlock.Text = "対象を入力してください。";
                return;
            }

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

        private void ComboModeCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelection();

            if (IsComboMode)
            {
                selectedCombination.Clear();
                if (selectedAssignment is not null)
                    selectedCombination.Add(selectedAssignment.SwitchName);
            }
            else
            {
                var fallbackSwitch = selectedCombination.FirstOrDefault() ?? items.FirstOrDefault()?.SwitchName;
                selectedCombination.Clear();
                selectedAssignment = items.FirstOrDefault(item => item.SwitchName == fallbackSwitch)
                    ?? items.FirstOrDefault();
            }

            LoadCurrentSelection();
        }

        private void SwitchButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string switchName)
                return;

            if (IsComboMode)
            {
                if (!selectedCombination.Add(switchName))
                    selectedCombination.Remove(switchName);
            }
            else
            {
                selectedAssignment = items.FirstOrDefault(item => item.SwitchName == switchName);
            }

            LoadCurrentSelection();
        }

        private void ActionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingSelection)
                return;

            SaveCurrentSelection();

            if (!IsComboMode && selectedAssignment is not null)
                selectedAssignment.SelectedActionName = ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName;

            UpdateSelectionHint();
            RefreshKeyboardButtons();
        }

        private void ParameterTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingSelection)
                return;

            SaveCurrentSelection();

            if (!IsComboMode && selectedAssignment is not null)
                selectedAssignment.Parameter = ParameterTextBox.Text;

            UpdateSelectionHint();
        }

        private void TestSelectedButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelection();

            if (IsComboMode)
            {
                if (selectedCombination.Count < 2)
                    return;

                MappingConverter.ExecuteAction(
                    ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName,
                    ParameterTextBox.Text,
                    SwitchLayout.NormalizeCombination(selectedCombination),
                    currentScope);
                return;
            }

            if (selectedAssignment is null)
                return;

            MappingConverter.ExecuteAction(
                ActionComboBox.SelectedItem as string ?? MappingConverter.NoneActionName,
                ParameterTextBox.Text,
                selectedAssignment.SwitchName,
                currentScope);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelection();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveCurrentSelection();
            base.OnClosing(e);
        }
    }
}
