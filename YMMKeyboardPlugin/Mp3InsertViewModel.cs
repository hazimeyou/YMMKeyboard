using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin
{
    public class Mp3InsertViewModel : ITimelineToolViewModel, INotifyPropertyChanged
    {
        public static Mp3InsertViewModel? Instance { get; private set; }

        public static Timeline? _timeline;
        private UndoRedoManager? _undoRedoManager;
        public Timeline Timeline { get; set; }
        public Mp3InsertViewModel()
        {
            Instance = this;
            Debug.WriteLine("[Mp3Insert] Constructor");
        }

        public async void InsertMp3()
        {
            Debug.WriteLine("[Mp3Insert] Insert START");

            if (_timeline == null || _undoRedoManager == null)
            {
                Debug.WriteLine("[Mp3Insert] Timeline or UndoRedo is NULL");
                return;
            }

            var path = @"C:\Users\yu-za-hazimeyou\Desktop\S06test.wav";
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
