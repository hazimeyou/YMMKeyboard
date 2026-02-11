using System.Windows.Controls;
using System.Diagnostics;
using YMMKeyboardPlugin.TimelineTool;
namespace YMMKeyboardPlugin
{
    public partial class YmmKeyboardSettingsView : UserControl
    {
        public YmmKeyboardSettingsView()
        {
            InitializeComponent();
        }

        private void InsertMp3_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TimelineImport.Instance?.InsertMp3();
        }
        public void migi(object sender, System.Windows.RoutedEventArgs e)
        {
            TimelineSeek.Instance?.migi();
        }
        public void hidari(object sender, System.Windows.RoutedEventArgs e)
        {
            TimelineSeek.Instance?.hidari();
        }
    }
}
