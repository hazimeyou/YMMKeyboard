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
                var seekCommand = ResolveSeekCommand();
                if (seekCommand is null)
                    return false;

                var invokeMethod = seekCommand.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                    {
                        if (!string.Equals(m.Name, "Invoke", StringComparison.Ordinal))
                            return false;
                        var p = m.GetParameters();
                        return p.Length == 1 && (p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(TimeSpan) || p[0].ParameterType == typeof(object));
                    });
                if (invokeMethod is null)
                    return false;

                var parameterType = invokeMethod.GetParameters()[0].ParameterType;
                object argument = parameterType == typeof(TimeSpan)
                    ? TimeSpan.FromMilliseconds(frameDelta)
                    : frameDelta;

                invokeMethod.Invoke(seekCommand, new[] { argument });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardAction] Seek command invoke failed: {ex.Message}");
                return false;
            }
        }

        private static object? ResolveSeekCommand()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (!type.Name.Contains("CommandSettings", StringComparison.Ordinal))
                        continue;

                    var defaultProperty = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                    if (defaultProperty is null)
                        continue;

                    var settingsInstance = defaultProperty.GetValue(null);
                    if (settingsInstance is null)
                        continue;

                    var indexer = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, null, new[] { typeof(string) }, null);
                    if (indexer is null)
                        continue;

                    var seekCommand = indexer.GetValue(settingsInstance, new object[] { "Seek" });
                    if (seekCommand is not null)
                        return seekCommand;
                }
            }

            return null;
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
