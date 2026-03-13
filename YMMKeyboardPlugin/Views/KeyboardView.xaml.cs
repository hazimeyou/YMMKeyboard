using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;

namespace YMMKeyboardPlugin.Views
{
    public partial class KeyboardView : UserControl
    {
        private readonly HashSet<string> selectedCombination = new(StringComparer.OrdinalIgnoreCase);

        public KeyboardView()
        {
            InitializeComponent();
            Debug.WriteLine("[KeyboardView] ctor");
            UpdateComboUiState();
        }

        private bool IsComboMode => ComboModeCheckBox.IsChecked == true;

        private void SwitchButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var switchName = button.Tag as string ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(switchName))
                return;

            if (IsComboMode)
            {
                if (!selectedCombination.Add(switchName))
                    selectedCombination.Remove(switchName);

                UpdateComboUiState();
                return;
            }

            MappingConverter.ExecuteUiSwitch(switchName);
        }

        private void ComboModeCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (!IsComboMode)
                selectedCombination.Clear();

            UpdateComboUiState();
        }

        private void ExecuteCombinationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (selectedCombination.Count < 2)
            {
                MessageBox.Show("複数キーを実行するには、2つ以上のキーを選択してください。", "YMMキーボード");
                return;
            }

            MappingConverter.ExecuteUiCombination(selectedCombination);
        }

        private void ClearSelectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            selectedCombination.Clear();
            UpdateComboUiState();
        }

        private void UpdateComboUiState()
        {
            SelectedKeysTextBlock.Text = selectedCombination.Count == 0
                ? "なし"
                : SwitchLayout.FormatCombination(selectedCombination);

            foreach (var button in FindKeyboardButtons(KeyboardSurfaceGrid))
            {
                var switchName = button.Tag as string ?? string.Empty;
                var isSelected = IsComboMode && selectedCombination.Contains(switchName);
                if (isSelected)
                {
                    button.Background = SystemColors.HighlightBrush;
                    button.BorderBrush = SystemColors.HighlightBrush;
                    button.Foreground = SystemColors.HighlightTextBrush;
                }
                else
                {
                    button.ClearValue(Control.BackgroundProperty);
                    button.ClearValue(Control.BorderBrushProperty);
                    button.ClearValue(Control.ForegroundProperty);
                }
            }
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
    }
}
