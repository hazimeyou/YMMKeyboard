using System.Diagnostics;
using System.IO;
using System.Windows;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.UndoRedo;
using System.ComponentModel;
namespace YMMKeyboardPlugin.TimelineTool
{
    public class TimelineSeek: INotifyPropertyChanged
    {
        public static TimelineSeek? Instance { get; private set; }

        public static Timeline? _timeline;
        private UndoRedoManager? _undoRedoManager;
        public Timeline Timeline { get; set; }
        public async void migi()
        {
            Debug.WriteLine("[Mp3Insert] Dispose");
            purasu();
        }
        public async void hidari()
        {
            Debug.WriteLine("[Mp3Insert] Dispose");
            mainasu();
        }
        public static void purasu()
        {
            _timeline.CurrentFrame = _timeline.CurrentFrame + 1;
        }
        public static void mainasu()
        {
            _timeline.CurrentFrame = _timeline.CurrentFrame - 1;
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
