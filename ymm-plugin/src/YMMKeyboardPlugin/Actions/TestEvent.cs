using System.Text;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YMMKeyboardPlugin.Logging;

namespace YMMKeyboardPlugin.Actions
{
    public static class TestEvent
    {
        public static void Execute(string switchName, string? parameter)
        {
            var started = DateTime.Now;
            var report = new StringBuilder();
            var foundCandidates = 0;
            var invokeSuccess = 0;
            var invokeFail = 0;

            report.AppendLine($"=== TestEvent Probe Start {started:yyyy-MM-dd HH:mm:ss.fff} ===");
            report.AppendLine($"switch={switchName}");
            report.AppendLine($"parameter={parameter ?? "(null)"}");
            report.AppendLine($"AppContext.BaseDirectory={AppContext.BaseDirectory}");
            report.AppendLine();

            try
            {
                EnsureTargetAssembliesLoaded(report);
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                report.AppendLine("[Loaded assemblies]");
                foreach (var name in assemblies.Select(a => a.GetName().Name).Where(n => !string.IsNullOrWhiteSpace(n)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    report.AppendLine($"- {name}");
                report.AppendLine();

                var commandSettingsTypes = ResolveCommandSettingsTypes(assemblies, report);
                if (commandSettingsTypes.Count == 0)
                {
                    report.AppendLine("CommandSettings type was not found.");
                }
                else
                {
                    foreach (var type in commandSettingsTypes)
                    {
                        report.AppendLine($"[CommandSettings] {type.Assembly.GetName().Name} :: {type.FullName}");
                        TryProbeCommandSettings(type, report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
                        report.AppendLine();
                    }
                }

                ProbeBroadCommandCandidates(assemblies, report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
                ProbeICommandCandidates(assemblies, report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
                ProbeWindowCommandBindings(report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
            }
            catch (Exception ex)
            {
                report.AppendLine($"Probe exception: {ex}");
            }

            var finished = DateTime.Now;
            report.AppendLine($"=== TestEvent Probe End {finished:yyyy-MM-dd HH:mm:ss.fff} ===");
            report.AppendLine($"Candidates={foundCandidates}, InvokeSuccess={invokeSuccess}, InvokeFail={invokeFail}");

            var reportPath = WriteProbeReport(report.ToString());
            PluginLogger.Info("TestEvent", $"Probe finished. candidates={foundCandidates}, success={invokeSuccess}, fail={invokeFail}, report={reportPath}");

            MessageBox.Show(
                $"テストイベント診断を実行しました。\n候補数: {foundCandidates}\n成功: {invokeSuccess}\n失敗: {invokeFail}\n\nレポート:\n{reportPath}",
                "テストイベント");
        }

        private static List<Type> ResolveCommandSettingsTypes(Assembly[] loadedAssemblies, StringBuilder report)
        {
            var list = new List<Type>();

            var directTypeNames = new[]
            {
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker.Plugin",
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker",
                "YukkuriMovieMaker.Settings.CommandSettings, YukkuriMovieMaker.Settings",
            };

            report.AppendLine("[Direct type resolve]");
            foreach (var typeName in directTypeNames)
            {
                var type = Type.GetType(typeName, throwOnError: false);
                report.AppendLine($"- {typeName} => {(type is null ? "NG" : "OK")}");
                if (type is not null)
                    list.Add(type);
            }
            report.AppendLine();

            report.AppendLine("[Assembly scan type resolve]");
            foreach (var asm in loadedAssemblies)
            {
                Type? type = null;
                try
                {
                    type = asm.GetType("YukkuriMovieMaker.Settings.CommandSettings", throwOnError: false);
                }
                catch
                {
                    // ignore
                }

                if (type is null)
                {
                    foreach (var t in GetLoadableTypes(asm))
                    {
                        if (string.Equals(t.Name, "CommandSettings", StringComparison.Ordinal) ||
                            string.Equals(t.FullName, "YukkuriMovieMaker.Settings.CommandSettings", StringComparison.Ordinal))
                        {
                            type = t;
                            break;
                        }
                    }
                }

                if (type is not null)
                {
                    report.AppendLine($"- hit {asm.GetName().Name} => {type.FullName}");
                    list.Add(type);
                }
            }
            report.AppendLine();

            return list
                .GroupBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();
        }

        private static void TryProbeCommandSettings(
            Type commandSettingsType,
            StringBuilder report,
            ref int foundCandidates,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            var instances = new List<(string Source, object Instance)>();

            var staticProps = commandSettingsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            report.AppendLine($"  static properties={staticProps.Length}");
            foreach (var prop in staticProps)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                    continue;
                try
                {
                    var value = prop.GetValue(null);
                    report.AppendLine($"    static prop {prop.Name} => {(value is null ? "null" : value.GetType().FullName)}");
                    if (value is not null)
                        instances.Add(($"static-prop:{prop.Name}", value));
                }
                catch (Exception ex)
                {
                    report.AppendLine($"    static prop {prop.Name} => exception: {ex.Message}");
                }
            }

            var staticFields = commandSettingsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            report.AppendLine($"  static fields={staticFields.Length}");
            foreach (var field in staticFields)
            {
                try
                {
                    var value = field.GetValue(null);
                    report.AppendLine($"    static field {field.Name} => {(value is null ? "null" : value.GetType().FullName)}");
                    if (value is not null)
                        instances.Add(($"static-field:{field.Name}", value));
                }
                catch (Exception ex)
                {
                    report.AppendLine($"    static field {field.Name} => exception: {ex.Message}");
                }
            }

            if (instances.Count == 0)
            {
                report.AppendLine("  no static instance candidate found.");
                return;
            }

            foreach (var (source, settingsInstance) in instances
                .GroupBy(x => RuntimeHelpers.GetHashCode(x.Instance))
                .Select(g => g.First()))
            {
                report.AppendLine($"  [settings-instance] source={source}, type={settingsInstance.GetType().FullName}");
                ProbeSettingsInstance(settingsInstance, source, report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
            }
        }

        private static void ProbeSettingsInstance(
            object settingsInstance,
            string source,
            StringBuilder report,
            ref int foundCandidates,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            var settingsType = settingsInstance.GetType();

            var stringIndexer = settingsType.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { typeof(string) }, null);
            report.AppendLine($"    string indexer => {(stringIndexer is null ? "NG" : "OK")}");
            if (stringIndexer is not null)
            {
                var keys = new[]
                {
                    "Seek", "[Seek]", "SeekWithoutSnap", "SeekToNextFrame", "SeekToPreviousFrame", "SeekToNextSecond", "SeekToPreviousSecond", "ScrollToFrame"
                };

                foreach (var key in keys)
                {
                    object? cmd = null;
                    try
                    {
                        cmd = stringIndexer.GetValue(settingsInstance, new object[] { key });
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine($"      key='{key}' => exception: {ex.Message}");
                        continue;
                    }

                    report.AppendLine($"      key='{key}' => {(cmd is null ? "null" : cmd.GetType().FullName)}");
                    if (cmd is null)
                        continue;

                    foundCandidates++;
                    TryInvokeCandidate(cmd, $"{source}/string:{key}", report, ref invokeSuccess, ref invokeFail);
                }
            }

            var enumTypes = GetLoadableTypes(settingsType.Assembly)
                .Where(t => t.IsEnum && (string.Equals(t.Name, "CommandType", StringComparison.Ordinal) || t.Name.Contains("CommandType", StringComparison.Ordinal)))
                .ToArray();
            report.AppendLine($"    command-like enums={enumTypes.Length}");
            foreach (var enumType in enumTypes)
            {
                var enumIndexer = settingsType.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null, new[] { enumType }, null);
                if (enumIndexer is null)
                    continue;

                var enumNames = Enum.GetNames(enumType)
                    .Where(n => n.Contains("Seek", StringComparison.OrdinalIgnoreCase) || n.Contains("Scroll", StringComparison.OrdinalIgnoreCase) || n.Contains("Frame", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                report.AppendLine($"      enum={enumType.FullName}, seek-like names={enumNames.Length}");
                foreach (var name in enumNames)
                {
                    object? cmd = null;
                    try
                    {
                        var key = Enum.Parse(enumType, name);
                        cmd = enumIndexer.GetValue(settingsInstance, new[] { key });
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine($"        enum='{name}' => exception: {ex.Message}");
                        continue;
                    }

                    report.AppendLine($"        enum='{name}' => {(cmd is null ? "null" : cmd.GetType().FullName)}");
                    if (cmd is null)
                        continue;

                    foundCandidates++;
                    TryInvokeCandidate(cmd, $"{source}/enum:{name}", report, ref invokeSuccess, ref invokeFail);
                }
            }

            var props = settingsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 &&
                            (p.Name.Contains("Seek", StringComparison.OrdinalIgnoreCase) ||
                             p.Name.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
                             p.Name.Contains("Frame", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            report.AppendLine($"    seek-like readable properties={props.Length}");
            foreach (var prop in props)
            {
                object? cmd = null;
                try
                {
                    cmd = prop.GetValue(settingsInstance);
                }
                catch (Exception ex)
                {
                    report.AppendLine($"      prop='{prop.Name}' => exception: {ex.Message}");
                    continue;
                }

                report.AppendLine($"      prop='{prop.Name}' => {(cmd is null ? "null" : cmd.GetType().FullName)}");
                if (cmd is null)
                    continue;

                foundCandidates++;
                TryInvokeCandidate(cmd, $"{source}/prop:{prop.Name}", report, ref invokeSuccess, ref invokeFail);
            }
        }

        private static void TryInvokeCandidate(object cmd, string label, StringBuilder report, ref int invokeSuccess, ref int invokeFail)
        {
            var methods = cmd.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => string.Equals(m.Name, "Invoke", StringComparison.Ordinal) || string.Equals(m.Name, "Execute", StringComparison.Ordinal))
                .ToArray();

            report.AppendLine($"      [{label}] methods={methods.Length}");

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var sig = string.Join(", ", parameters.Select(p => p.ParameterType.Name));
                report.AppendLine($"        {method.Name}({sig})");

                try
                {
                    object?[]? args = BuildArgs(parameters);
                    if (args is null)
                    {
                        report.AppendLine("          -> skip (unsupported signature)");
                        continue;
                    }

                    if (Application.Current?.Dispatcher is { } dispatcher)
                        dispatcher.Invoke(() => method.Invoke(cmd, args));
                    else
                        method.Invoke(cmd, args);

                    invokeSuccess++;
                    report.AppendLine("          -> success");
                }
                catch (Exception ex)
                {
                    invokeFail++;
                    var msg = ex.InnerException?.Message ?? ex.Message;
                    report.AppendLine($"          -> fail: {msg}");
                }
            }
        }

        private static object?[]? BuildArgs(ParameterInfo[] parameters)
        {
            if (parameters.Length == 0)
                return Array.Empty<object?>();

            if (parameters.Length == 1)
            {
                var t = parameters[0].ParameterType;
                if (t == typeof(int))
                    return new object?[] { 1 };
                if (t == typeof(TimeSpan))
                    return new object?[] { TimeSpan.FromSeconds(1.0 / 60.0) };
                if (t == typeof(object))
                    return new object?[] { 1 };
                return null;
            }

            if (parameters.Length == 2)
            {
                var first = parameters[0].ParameterType;
                var second = parameters[1].ParameterType;

                if (first == typeof(object) && typeof(System.Windows.IInputElement).IsAssignableFrom(second))
                    return new object?[] { 1, Application.Current?.MainWindow };
            }

            return null;
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

        private static void EnsureTargetAssembliesLoaded(StringBuilder report)
        {
            var targetFiles = new[]
            {
                "YukkuriMovieMaker.dll",
                "YukkuriMovieMaker.Plugin.dll",
                "YukkuriMovieMaker.Settings.dll",
            };

            report.AppendLine("[Ensure target assemblies loaded]");
            foreach (var file in targetFiles)
            {
                var path = Path.Combine(AppContext.BaseDirectory, file);
                if (!File.Exists(path))
                {
                    report.AppendLine($"- {file} => file not found");
                    continue;
                }

                try
                {
                    var loaded = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => string.Equals(a.GetName().Name, Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase));
                    if (loaded is not null)
                    {
                        report.AppendLine($"- {file} => already loaded");
                        continue;
                    }

                    _ = Assembly.LoadFrom(path);
                    report.AppendLine($"- {file} => loaded");
                }
                catch (Exception ex)
                {
                    report.AppendLine($"- {file} => load fail: {ex.Message}");
                }
            }
            report.AppendLine();
        }

        private static void ProbeBroadCommandCandidates(
            Assembly[] assemblies,
            StringBuilder report,
            ref int foundCandidates,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            report.AppendLine("[Broad scan in YukkuriMovieMaker*.dll]");

            var targetAssemblies = assemblies
                .Where(a =>
                {
                    var n = a.GetName().Name ?? string.Empty;
                    return n.StartsWith("YukkuriMovieMaker", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();

            report.AppendLine($"- target assemblies={targetAssemblies.Length}");

            foreach (var asm in targetAssemblies)
            {
                var asmName = asm.GetName().Name ?? "(unknown)";
                var types = GetLoadableTypes(asm).ToArray();
                report.AppendLine($"  [{asmName}] type count={types.Length}");

                var candidateTypes = types.Where(t =>
                        (t.Name.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
                         t.Name.Contains("KeyConfig", StringComparison.OrdinalIgnoreCase) ||
                         t.Name.Contains("Timeline", StringComparison.OrdinalIgnoreCase) ||
                         t.Name.Contains("Seek", StringComparison.OrdinalIgnoreCase)) &&
                        !t.IsAbstract)
                    .Take(200)
                    .ToArray();
                report.AppendLine($"    command-like types={candidateTypes.Length}");

                foreach (var t in candidateTypes)
                {
                    try
                    {
                        var staticProps = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                            .ToArray();
                        var staticFields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                        foreach (var p in staticProps)
                        {
                            object? instance = null;
                            try { instance = p.GetValue(null); } catch { }
                            if (instance is null) continue;
                            report.AppendLine($"    - {t.FullName} static-prop:{p.Name} => {instance.GetType().FullName}");
                            ProbeSettingsInstance(instance, $"broad:{asmName}:{t.Name}:{p.Name}", report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
                        }

                        foreach (var f in staticFields)
                        {
                            object? instance = null;
                            try { instance = f.GetValue(null); } catch { }
                            if (instance is null) continue;
                            report.AppendLine($"    - {t.FullName} static-field:{f.Name} => {instance.GetType().FullName}");
                            ProbeSettingsInstance(instance, $"broad:{asmName}:{t.Name}:{f.Name}", report, ref foundCandidates, ref invokeSuccess, ref invokeFail);
                        }
                    }
                    catch
                    {
                        // continue probing others
                    }
                }
            }

            report.AppendLine();
        }

        private static void ProbeICommandCandidates(
            Assembly[] assemblies,
            StringBuilder report,
            ref int foundCandidates,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            report.AppendLine("[ICommand direct scan]");

            var targetAssemblies = assemblies
                .Where(a => (a.GetName().Name ?? string.Empty).StartsWith("YukkuriMovieMaker", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var asm in targetAssemblies)
            {
                var asmName = asm.GetName().Name ?? "(unknown)";
                var types = GetLoadableTypes(asm).ToArray();

                foreach (var t in types)
                {
                    FieldInfo[] fields;
                    PropertyInfo[] props;
                    try
                    {
                        fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                            .ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var f in fields)
                    {
                        if (!IsSeekLikeName(f.Name))
                            continue;

                        object? value = null;
                        try { value = f.GetValue(null); } catch { }
                        if (value is not ICommand cmd)
                            continue;

                        report.AppendLine($"- {asmName}:{t.FullName}.{f.Name} => ICommand({value.GetType().FullName})");
                        foundCandidates++;
                        TryExecuteICommand(cmd, $"{asmName}:{t.Name}.{f.Name}", report, ref invokeSuccess, ref invokeFail);
                    }

                    foreach (var p in props)
                    {
                        if (!IsSeekLikeName(p.Name))
                            continue;

                        object? value = null;
                        try { value = p.GetValue(null); } catch { }
                        if (value is not ICommand cmd)
                            continue;

                        report.AppendLine($"- {asmName}:{t.FullName}.{p.Name} => ICommand({value.GetType().FullName})");
                        foundCandidates++;
                        TryExecuteICommand(cmd, $"{asmName}:{t.Name}.{p.Name}", report, ref invokeSuccess, ref invokeFail);
                    }
                }
            }

            report.AppendLine();
        }

        private static bool IsSeekLikeName(string name)
        {
            return name.Contains("Seek", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Frame", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryExecuteICommand(
            ICommand cmd,
            string label,
            StringBuilder report,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            object?[] candidates =
            {
                1,
                -1,
                TimeSpan.FromSeconds(1.0 / 60.0),
                null
            };

            foreach (var arg in candidates)
            {
                try
                {
                    var canExecute = cmd.CanExecute(arg);
                    report.AppendLine($"  [{label}] CanExecute({ArgLabel(arg)})={canExecute}");
                    if (!canExecute)
                        continue;

                    if (Application.Current?.Dispatcher is { } dispatcher)
                        dispatcher.Invoke(() => cmd.Execute(arg));
                    else
                        cmd.Execute(arg);

                    invokeSuccess++;
                    report.AppendLine($"  [{label}] Execute({ArgLabel(arg)}) => success");
                }
                catch (Exception ex)
                {
                    invokeFail++;
                    var msg = ex.InnerException?.Message ?? ex.Message;
                    report.AppendLine($"  [{label}] Execute({ArgLabel(arg)}) => fail: {msg}");
                }
            }
        }

        private static string ArgLabel(object? arg)
        {
            if (arg is null) return "null";
            return arg is TimeSpan ts ? $"TimeSpan({ts})" : arg.ToString() ?? "(unknown)";
        }

        private static void ProbeWindowCommandBindings(
            StringBuilder report,
            ref int foundCandidates,
            ref int invokeSuccess,
            ref int invokeFail)
        {
            report.AppendLine("[Window CommandBindings scan]");

            var window = Application.Current?.MainWindow;
            if (window is null)
            {
                report.AppendLine("- MainWindow is null");
                report.AppendLine();
                return;
            }

            try
            {
                report.AppendLine($"- MainWindow={window.GetType().FullName}");
                report.AppendLine($"- CommandBindings={window.CommandBindings.Count}");

                foreach (CommandBinding binding in window.CommandBindings)
                {
                    var cmd = binding.Command;
                    var name = GetCommandName(cmd);
                    report.AppendLine($"  - command={name}");
                    if (!IsSeekLikeName(name))
                        continue;

                    foundCandidates++;
                    TryExecuteICommand(cmd, $"window:{name}", report, ref invokeSuccess, ref invokeFail);
                }

                report.AppendLine($"- InputBindings={window.InputBindings.Count}");
                foreach (InputBinding inputBinding in window.InputBindings)
                {
                    var cmd = inputBinding.Command;
                    var name = GetCommandName(cmd);
                    report.AppendLine($"  - input={name}");
                    if (!IsSeekLikeName(name))
                        continue;

                    foundCandidates++;
                    TryExecuteICommand(cmd, $"window-input:{name}", report, ref invokeSuccess, ref invokeFail);
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"- scan fail: {ex.Message}");
            }

            report.AppendLine();
        }

        private static string GetCommandName(ICommand command)
        {
            if (command is RoutedUICommand rui && !string.IsNullOrWhiteSpace(rui.Name))
                return rui.Name;
            if (command is RoutedCommand rc && !string.IsNullOrWhiteSpace(rc.Name))
                return rc.Name;
            return command.GetType().FullName ?? command.GetType().Name;
        }

        private static string WriteProbeReport(string content)
        {
            var logDir = Path.GetDirectoryName(PluginLogger.CurrentLogFilePath) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(logDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(logDir, $"TestEventProbe_{timestamp}.log");
            var latest = Path.Combine(logDir, "TestEventProbe_latest.log");

            File.WriteAllText(path, content, Encoding.UTF8);
            File.WriteAllText(latest, content, Encoding.UTF8);

            return path;
        }
    }
}







