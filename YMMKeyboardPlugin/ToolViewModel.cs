using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows;
namespace YMMKeyboardPlugin
{
    public class ToolViewModel: BaseViewModel
    {
        private string selectedDevice;
        public string SelectedDevice { get => selectedDevice; set { selectedDevice = value; OnPropertyChanged(nameof(SelectedDevice)); } }
        public ICommand a { get; }
        public ICommand b { get; }
        public ICommand c { get; }
        public ICommand d { get; }
        public ICommand e { get; }
        public ToolViewModel()
        {
            a = new RelayCommand(a1);
            b = new RelayCommand(b2);
            c = new RelayCommand(c3);
            d = new RelayCommand(d4);
            e = new RelayCommand(e5);

        }






        public void a1() => MessageBox.Show("a");
        public void b2() => MessageBox.Show("b");
        public void c3() => MessageBox.Show("c");
        public void d4() => MessageBox.Show("d");
        public void e5() => MessageBox.Show("e");
    }
}
