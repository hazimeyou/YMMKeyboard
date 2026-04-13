using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YMMKeyboardPlugin.Actions;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin.Mapping
{
    public class LegacyKeyboardViewModel : ITimelineToolViewModel, INotifyPropertyChanged
    {
        public static LegacyKeyboardViewModel? Instance { get; private set; }

        public static Timeline? TimelineInstance { get; private set; }
#pragma warning disable CS0169
        private UndoRedoManager? undoRedoManager;
#pragma warning restore CS0169
        public Timeline Timeline { get; set; } = null!;

        public LegacyKeyboardViewModel()
        {
            Instance = this;
            Debug.WriteLine("[LegacyKeyboardViewModel] ctor");
        }

        public static void InsertMp3(string path)
        {
            Debug.WriteLine("[LegacyKeyboardViewModel] InsertMp3 START");

            if (!File.Exists(path) || TimelineInstance is null)
            {
                Debug.WriteLine("[LegacyKeyboardViewModel] InsertMp3 skipped");
                return;
            }

            AudioItem item = null!;
            Application.Current.Dispatcher.Invoke(() =>
            {
                item = new AudioItem(path)
                {
                    Frame = TimelineInstance.CurrentFrame,
                    Layer = 0,
                    Length = 90,
                };

                TimelineInstance.TryAddItems(new IItem[] { item }, item.Frame, item.Layer);
            });

            Debug.WriteLine("[LegacyKeyboardViewModel] InsertMp3 END");
        }

        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            Debug.WriteLine("[LegacyKeyboardViewModel] SetTimelineToolInfo");
            TimelineInstance = info.Timeline;
            undoRedoManager = info.UndoRedoManager;
            Timeline = info.Timeline;

            KeyboardAction.SyncTimelineToolInfo(info);
        }

        public static void purasu()
        {
            KeyboardAction.PlusSeekFrame(1);
        }

        public static void mainasu()
        {
            KeyboardAction.MinusSeekFrame(1);
        }

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        /*
        // 旧実装では _timeline を直接触っていた。
        // 現在は KeyboardAction に集約しているため、互換APIだけ残している。
        public static Timeline? _timeline;
        private UndoRedoManager? _undoRedoManager;
        */
    }
}
