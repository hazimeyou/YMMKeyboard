using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Windows;
namespace YMMKeyboardPlugin
{
    public class ToolViewModel: BaseViewModel
    {
        private string selectedDevice;
        public string SelectedDevice { get => selectedDevice; set { selectedDevice = value; OnPropertyChanged(nameof(SelectedDevice)); } }
        public ICommand kese { get; }
        public ToolViewModel()
        {
            kese = new RelayCommand(test);
        }
        public void test() => MessageBox.Show("test");
    }
}
