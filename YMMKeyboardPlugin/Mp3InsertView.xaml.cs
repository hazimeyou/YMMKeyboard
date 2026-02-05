using System.Windows.Controls;
using System.Diagnostics;

namespace YMMKeyboardPlugin
{
    public partial class Mp3InsertView : UserControl
    {
        public Mp3InsertView()
        {
            InitializeComponent();
            Debug.WriteLine("[Mp3InsertView] ctor");
        }

        private void InsertMp3_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Debug.WriteLine("[Mp3InsertView] Button Click");

            Mp3InsertViewModel.Instance?.InsertMp3();
        }
    }
}
