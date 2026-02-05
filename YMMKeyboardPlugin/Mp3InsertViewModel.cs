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

        private Timeline? _timeline;
        private UndoRedoManager? _undoRedoManager;

        public Mp3InsertViewModel()
        {
            Instance = this;
            Debug.WriteLine("[Mp3Insert] Constructor");
        }

        public void InsertMp3()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Debug.WriteLine("[Mp3Insert] InsertMp3 START");

                if (_timeline == null || _undoRedoManager == null)
                {
                    Debug.WriteLine("[Mp3Insert] Timeline or UndoRedoManager is NULL");
                    return;
                }

                var path = @"C:\Users\yu-za-hazimeyou\Desktop\S06test.wav";
                if (!File.Exists(path))
                {
                    Debug.WriteLine("[Mp3Insert] File not found");
                    return;
                }

                var item = new AudioItem(path)
                {
                    Frame = _timeline.CurrentFrame,
                    Layer = 0,
                };

                Debug.WriteLine("[Mp3Insert] TryAddItems");

                var ok = _timeline.TryAddItems(
                    new IItem[] { item },
                    item.Frame,
                    item.Layer);

                Debug.WriteLine($"[Mp3Insert] TryAddItems Result = {ok}");
                Debug.WriteLine($"[Mp3Insert] Length(before) = {item.Length}");

                // ★★★ ここが本命 ★★★
                ForceTimelineRefresh(item);

                Debug.WriteLine($"[Mp3Insert] Length(after) = {item.Length}");

                _undoRedoManager.Record();
                Debug.WriteLine("[Mp3Insert] UndoRedo Record DONE");
            });
        }

        /// <summary>
        /// タイムラインを強制再構築させる
        /// </summary>
        private void ForceTimelineRefresh(AudioItem item)
        {
            Debug.WriteLine("[Mp3Insert] ForceTimelineRefresh");

            // 一旦削除
            _timeline!.Items.Remove(item);

            // すぐ戻す
            _timeline.Items.Add(item);
        }

        // ===== YMMから自動で呼ばれる =====
        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            Debug.WriteLine("[Mp3Insert] SetTimelineToolInfo");
            _timeline = info.Timeline;
            _undoRedoManager = info.UndoRedoManager;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
