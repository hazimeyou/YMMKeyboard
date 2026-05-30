using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
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
        private static ICommand? cachedWindowSeekCommand;
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

            void Action()
            {
                if (TryInvokeSeekCommand(frames))
                    return;

                var timeline = TimelineInstance;
                if (timeline is not null)
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
                if (TryInvokeSeekByWindowCommand(frameDelta))
                    return true;

                var seek = ResolveSeekCommandAndMethod();
                if (seek is null)
                    return false;

                var args = CreateSeekArguments(seek.Value.InvokeMethod, frameDelta);
                if (args is null)
                    return false;

                seek.Value.InvokeMethod.Invoke(seek.Value.Command, args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeyboardAction] Seek command invoke failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryInvokeSeekByWindowCommand(int frameDelta)
        {
            var cmd = ResolveWindowSeekCommand();
            if (cmd is null)
                return false;

            var args = new object?[]
            {
                frameDelta,
                FrameDeltaToTimeSpan(frameDelta),
                null
            };

            foreach (var arg in args)
            {
                try
                {
                    if (!cmd.CanExecute(arg))
                        continue;
                    cmd.Execute(arg);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[KeyboardAction] Window seek command failed for arg={arg ?? "null"}: {ex.Message}");
                }
            }

            return false;
        }

        private static ICommand? ResolveWindowSeekCommand()
        {
            lock (seekCacheLock)
            {
                if (cachedWindowSeekCommand is not null)
                    return cachedWindowSeekCommand;
            }

            var window = Application.Current?.MainWindow;
            if (window is null)
                return null;

            ICommand? found = null;
            foreach (CommandBinding binding in window.CommandBindings)
            {
                var cmd = binding.Command;
                var name = GetCommandName(cmd);
                if (string.Equals(name, "ScrollToFrame", StringComparison.Ordinal) ||
                    string.Equals(name, "シーク", StringComparison.Ordinal) ||
                    name.Contains("ScrollToFrame", StringComparison.OrdinalIgnoreCase))
                {
                    found = cmd;
                    break;
                }
            }

            if (found is null)
                return null;

            lock (seekCacheLock)
            {
                cachedWindowSeekCommand = found;
            }
            return found;
        }

        private static string GetCommandName(ICommand command)
        {
            if (command is RoutedUICommand rui && !string.IsNullOrWhiteSpace(rui.Name))
                return rui.Name;
            if (command is RoutedCommand rc && !string.IsNullOrWhiteSpace(rc.Name))
                return rc.Name;
            return command.GetType().FullName ?? command.GetType().Name;
        }

        private static (object Command, MethodInfo InvokeMethod)? ResolveSeekCommandAndMethod()
        {
            lock (seekCacheLock)
            {
                if (cachedSeekCommand is not null && cachedSeekInvokeMethod is not null)
                    return (cachedSeekCommand, cachedSeekInvokeMethod);
            }

            foreach (var commandSettingsType in ResolveCommandSettingsTypes())
            {
                if (!TryResolveSeekFromCommandSettings(commandSettingsType, out var seekCommand, out var invokeMethod))
                    continue;

                lock (seekCacheLock)
                {
                    cachedSeekCommand = seekCommand;
                    cachedSeekInvokeMethod = invokeMethod;
                }
                return (cachedSeekCommand!, cachedSeekInvokeMethod!);
            }

            return null;
        }

        private static IEnumerable<Type> ResolveCommandSettingsTypes()
        {
            var directTypeNames = new[]
            {
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker.Settings",
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker.Plugin",
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker",
            };

            foreach (var typeName in directTypeNames)
            {
                var type = Type.GetType(typeName, throwOnError: false);
                if (type is not null)
                    yield return type;
            }

            foreach (var assembly in GetPreferredAssemblies())
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (!string.Equals(type.Name, "CommandSettings", StringComparison.Ordinal))
                        continue;

                    if (!type.FullName?.Contains("YukkuriMovieMaker.Settings", StringComparison.Ordinal) ?? true)
                        continue;

                    yield return type;
                }
            }
        }

        private static IEnumerable<Assembly> GetPreferredAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            var pluginAsm = loaded.FirstOrDefault(a => string.Equals(a.GetName().Name, "YukkuriMovieMaker.Plugin", StringComparison.Ordinal));
            if (pluginAsm is not null)
                yield return pluginAsm;
            else
            {
                var loadedFromFile = TryLoadYmmPluginAssemblyFromFile();
                if (loadedFromFile is not null)
                    yield return loadedFromFile;
            }

            foreach (var assembly in loaded)
            {
                if (ReferenceEquals(assembly, pluginAsm))
                    continue;
                yield return assembly;
            }
        }

        private static Assembly? TryLoadYmmPluginAssemblyFromFile()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "YukkuriMovieMaker.Plugin.dll");
                if (!File.Exists(path))
                    return null;

                return Assembly.LoadFrom(path);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!;
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static bool TryResolveSeekFromCommandSettings(Type commandSettingsType, out object? seekCommand, out MethodInfo? invokeMethod)
        {
            seekCommand = null;
            invokeMethod = null;

            var defaultProperty = commandSettingsType.GetProperty("Default", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var settingsInstance = defaultProperty?.GetValue(null);
            if (settingsInstance is null)
                return false;

            var instanceType = settingsInstance.GetType();
            var indexer = instanceType.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { typeof(string) }, null);
            if (indexer is null)
                return false;

            seekCommand = indexer.GetValue(settingsInstance, new object[] { "Seek" })
                ?? indexer.GetValue(settingsInstance, new object[] { "[Seek]" });
            if (seekCommand is null)
                return false;

            invokeMethod = seekCommand.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "Invoke", StringComparison.Ordinal) &&
                        !string.Equals(m.Name, "Execute", StringComparison.Ordinal))
                        return false;
                    var p = m.GetParameters();
                    if (p.Length == 1)
                        return p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(TimeSpan) || p[0].ParameterType == typeof(object);
                    if (p.Length == 2)
                        return p[0].ParameterType == typeof(object) && typeof(IInputElement).IsAssignableFrom(p[1].ParameterType);
                    return false;
                });

            return invokeMethod is not null;
        }

        private static object?[]? CreateSeekArguments(MethodInfo invokeMethod, int frameDelta)
        {
            var parameters = invokeMethod.GetParameters();
            if (parameters.Length == 1)
            {
                var parameterType = parameters[0].ParameterType;
                object value = parameterType == typeof(TimeSpan)
                    ? FrameDeltaToTimeSpan(frameDelta)
                    : frameDelta;
                return new object?[] { value };
            }

            if (parameters.Length == 2)
                return new object?[] { frameDelta, Application.Current?.MainWindow };

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
