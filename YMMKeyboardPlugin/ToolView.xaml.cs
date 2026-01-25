namespace YMMKeyboardPlugin
{
    public partial class ToolView : UserControl
    {
        public ToolView()
        {
            InitializeComponent();
            this.DataContext = new ToolViewModel();
        }
    }
}
