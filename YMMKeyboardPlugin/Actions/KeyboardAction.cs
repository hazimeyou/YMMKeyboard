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
        private static readonly object seekCacheLock = new();
        private static object? cachedSeekCommand;
        private static MethodInfo? cachedSeekInvokeMethod;
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
                var seek = ResolveSeekCommandAndMethod();
                if (seek is null)
                    return false;

                var parameterType = seek.Value.InvokeMethod.GetParameters()[0].ParameterType;
                object argument = parameterType == typeof(TimeSpan)
                    ? FrameDeltaToTimeSpan(frameDelta)
                    : frameDelta;

                seek.Value.InvokeMethod.Invoke(seek.Value.Command, new[] { argument });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardAction] Seek command invoke failed: {ex.Message}");
                return false;
            }
        }

        private static (object Command, MethodInfo InvokeMethod)? ResolveSeekCommandAndMethod()
        {
            lock (seekCacheLock)
            {
                if (cachedSeekCommand is not null && cachedSeekInvokeMethod is not null)
                    return (cachedSeekCommand, cachedSeekInvokeMethod);
            }

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
                    if (seekCommand is null)
                        continue;

                    var invokeMethod = seekCommand.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m =>
                        {
                            if (!string.Equals(m.Name, "Invoke", StringComparison.Ordinal))
                                return false;
                            var p = m.GetParameters();
                            return p.Length == 1 && (p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(TimeSpan) || p[0].ParameterType == typeof(object));
                        });
                    if (invokeMethod is null)
                        continue;

                    lock (seekCacheLock)
                    {
                        cachedSeekCommand = seekCommand;
                        cachedSeekInvokeMethod = invokeMethod;
                    }
                    return (seekCommand, invokeMethod);
                }
            }

            return null;
        }

        private static TimeSpan FrameDeltaToTimeSpan(int frameDelta)
        {
            var timeline = TimelineInstance;
            if (timeline is null)
                return TimeSpan.Zero;

            var fps = GetTimelineFps(timeline);
            if (fps <= 0)
                fps = 60.0;

            return TimeSpan.FromSeconds(frameDelta / fps);
        }

        private static double GetTimelineFps(Timeline timeline)
        {
            try
            {
                var videoInfoProperty = timeline.GetType().GetProperty("VideoInfo", BindingFlags.Public | BindingFlags.Instance);
                var videoInfo = videoInfoProperty?.GetValue(timeline);
                if (videoInfo is not null)
                {
                    var fpsValue = GetPropertyDouble(videoInfo, "FPS");
                    if (fpsValue > 0)
                        return fpsValue;
                }

                var directFps = GetPropertyDouble(timeline, "FPS");
                if (directFps > 0)
                    return directFps;

                var frameRate = GetPropertyDouble(timeline, "FrameRate");
                if (frameRate > 0)
                    return frameRate;
            }
            catch
            {
                // fallback below
            }

            return 60.0;
        }

        private static double GetPropertyDouble(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
                return 0;

            var value = property.GetValue(instance);
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => 0,
            };
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
