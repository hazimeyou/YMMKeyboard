using System.ComponentModel;
using System.Diagnostics;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin.Actions
{
    public class KeyboardAction : ITimelineToolViewModel, INotifyPropertyChanged
    {
        public static Timeline? TimelineInstance { get; private set; }
#pragma warning disable CS0169
        private UndoRedoManager? undoRedoManager;
#pragma warning restore CS0169

        public Timeline Timeline { get; set; } = null!;

        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            Debug.WriteLine("[KeyboardAction] SetTimelineToolInfo");
            SyncTimelineToolInfo(info);
        }

        public static void SyncTimelineToolInfo(TimelineToolInfo info)
        {
            TimelineInstance = info.Timeline;
        }

        public static void PlusSeekFrame(int frames)
        {
            if (TimelineInstance is null)
                return;

            TimelineInstance.CurrentFrame += frames;
        }

        public static void MinusSeekFrame(int frames)
        {
            if (TimelineInstance is null)
                return;

            TimelineInstance.CurrentFrame -= frames;
        }

        public static void PlusFrame(int frames)
        {
            PlusSeekFrame(frames);
        }

        public static void MinusFrame(int frames)
        {
            MinusSeekFrame(frames);
        }

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        /*
        // 旧実装: UndoRedoManager を直接保持していたが、現状の操作では未使用。
        // 将来アンドゥ対応を戻す場合はここから再利用できるようフィールドは残している。
        */
    }
}
