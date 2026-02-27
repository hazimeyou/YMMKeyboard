using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Shapes;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin.Mapping
{
    public class keyboardViewModel : ITimelineToolViewModel, INotifyPropertyChanged
    {
        public static keyboardViewModel? Instance { get; private set; }

        public static Timeline? _timeline;
        private UndoRedoManager? _undoRedoManager;
        public Timeline Timeline { get; set; }
        public keyboardViewModel()
        {
            Instance = this;
            Debug.WriteLine("[Mp3Insert] Constructor");
        }

        public static void InsertMp3(string path)
        {
            Debug.WriteLine("[Mp3Insert] Insert START");

            if (!File.Exists(path))
            {
                Debug.WriteLine("[Mp3Insert] File not found");
                return;
            }

            // ★ UIスレッドで追加
            AudioItem item = null!;

            Application.Current.Dispatcher.Invoke(() =>
            {
                item = new AudioItem(path)
                {
                    Frame = _timeline.CurrentFrame,
                    Layer = 0,
                    Length = 90,
                };

                Debug.WriteLine("[Mp3Insert] TryAddItems");
                _timeline.TryAddItems(new IItem[] { item }, item.Frame, item.Layer);
            });
            Debug.WriteLine("[Mp3Insert] Insert END");
        }

        // ===== YMMから自動で呼ばれる =====
        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            Debug.WriteLine("[Mp3Insert] SetTimelineToolInfo");
            _timeline = info.Timeline;
            _undoRedoManager = info.UndoRedoManager;
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
