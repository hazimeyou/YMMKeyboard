using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
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
            SeekByFrames(frames);
        }

        public static void MinusSeekFrame(int frames)
        {
            SeekByFrames(-frames);
        }

        public static void PlusFrame(int frames)
        {
            PlusSeekFrame(frames);
        }

        public static void MinusFrame(int frames)
        {
            MinusSeekFrame(frames);
        }

        private static void SeekByFrames(int frames)
        {
            if (frames == 0)
                return;

            var timeline = TimelineInstance;
            if (timeline is null)
                return;

            void Action()
            {
                if (!TryInvokeSeekCommand(frames))
                    timeline.CurrentFrame += frames;
            }

            if (Application.Current?.Dispatcher is { } dispatcher)
                dispatcher.Invoke(Action);
            else
                Action();
        }

        private static bool TryInvokeSeekCommand(int frameDelta)
        {
            try
            {
                // YMM API: CommandSettings.Default["Seek"].Invoke(frameDelta or TimeSpan)
                var commandSettingsType = Type.GetType("YukkuriMovieMaker.Commons.CommandSettings, YukkuriMovieMaker");
                if (commandSettingsType is null)
                    return false;

                var defaultProperty = commandSettingsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                var settingsInstance = defaultProperty?.GetValue(null);
                if (settingsInstance is null)
                    return false;

                var indexer = commandSettingsType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, null, new[] { typeof(string) }, null);
                var seekCommand = indexer?.GetValue(settingsInstance, new object[] { "Seek" });
                if (seekCommand is null)
                    return false;

                var invokeMethod = seekCommand.GetType().GetMethod("Invoke", new[] { typeof(object) })
                    ?? seekCommand.GetType().GetMethod("Invoke", new[] { typeof(int) })
                    ?? seekCommand.GetType().GetMethod("Invoke", new[] { typeof(TimeSpan) });
                if (invokeMethod is null)
                    return false;

                var parameterType = invokeMethod.GetParameters().FirstOrDefault()?.ParameterType;
                if (parameterType == typeof(TimeSpan))
                {
                    invokeMethod.Invoke(seekCommand, new object[] { TimeSpan.FromMilliseconds(frameDelta) });
                }
                else if (parameterType == typeof(int))
                {
                    invokeMethod.Invoke(seekCommand, new object[] { frameDelta });
                }
                else
                {
                    invokeMethod.Invoke(seekCommand, new object[] { frameDelta });
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardAction] Seek command invoke failed: {ex.Message}");
                return false;
            }
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
