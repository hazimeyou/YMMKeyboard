using System.ComponentModel;
using System.Diagnostics;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin
{
    public class KeyboardAction : ITimelineToolViewModel, INotifyPropertyChanged
    {
        public static Timeline? TimelineInstance { get; private set; }
        private UndoRedoManager? undoRedoManager;

        public Timeline Timeline { get; set; } = null!;

        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            Debug.WriteLine("[KeyboardAction] SetTimelineToolInfo");
            TimelineInstance = info.Timeline;
            undoRedoManager = info.UndoRedoManager;
            Timeline = info.Timeline;
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

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
