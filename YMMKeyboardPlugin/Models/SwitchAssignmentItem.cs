using System.Collections.ObjectModel;
using System.ComponentModel;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Models
{
    public class SwitchAssignmentItem : INotifyPropertyChanged
    {
        private string selectedActionName = MappingConverter.NoneActionName;
        private string parameter = string.Empty;

        public string SwitchName { get; init; } = string.Empty;
        public IReadOnlyList<string> AvailableActions => MappingConverter.AvailableActions;

        public string SelectedActionName
        {
            get => selectedActionName;
            set
            {
                if (selectedActionName == value)
                    return;

                selectedActionName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedActionName)));
            }
        }

        public string Parameter
        {
            get => parameter;
            set
            {
                if (parameter == value)
                    return;

                parameter = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parameter)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
