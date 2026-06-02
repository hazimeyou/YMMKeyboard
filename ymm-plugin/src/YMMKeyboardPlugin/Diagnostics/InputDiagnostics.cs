using System.Reflection;
using YMMKeyboardPlugin.Key;

namespace YMMKeyboardPlugin.Diagnostics;

public static class InputDiagnostics
{
    private static readonly object sync = new();
    private static InputDiagnosticSession? session;

    public static void Initialize(string source = "runtime")
    {
        lock (sync)
        {
            if (session is not null)
                return;

            session = new InputDiagnosticSession(source);
            InputDiagnosticWriter.Write(session.ToReport());
        }
    }

    public static void RecordInputReceived(KeyEvent input)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "InputReceived",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
            });
            FlushLocked();
        }
    }

    public static void RecordInputFiltered(KeyEvent input, string filterName, bool accepted, string rejectReason)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "InputFiltered",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                FilterName = filterName,
                Accepted = accepted,
                RejectReason = rejectReason,
            });
            FlushLocked();
        }
    }

    public static void RecordInputMapped(KeyEvent input, string mappedAction, string mappingSource)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "InputMapped",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                MappedAction = mappedAction,
                MappingSource = mappingSource,
            });
            FlushLocked();
        }
    }

    public static void RecordMacroResolved(KeyEvent input, string macroName, int stepCount, string resolutionResult)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "MacroResolved",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                MacroName = macroName,
                StepCount = stepCount,
                ResolutionResult = resolutionResult,
            });
            FlushLocked();
        }
    }

    public static void RecordDispatchPrepared(KeyEvent input, string dispatchType, string target, string payloadSummary)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "DispatchPrepared",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                DispatchType = dispatchType,
                Target = target,
                PayloadSummary = payloadSummary,
            });
            FlushLocked();
        }
    }

    public static void Flush()
    {
        lock (sync)
        {
            EnsureSession();
            FlushLocked();
        }
    }

    private static void EnsureSession()
    {
        session ??= new InputDiagnosticSession("runtime");
    }

    private static void FlushLocked()
    {
        if (session is null)
            return;

        InputDiagnosticWriter.Write(session.ToReport());
    }

    private sealed class InputDiagnosticSession
    {
        private readonly string source;
        public List<InputDiagnosticEvent> Events { get; } = [];

        public InputDiagnosticSession(string source)
        {
            this.source = source;
        }

        public InputDiagnosticReport ToReport()
        {
            var appVersion = GetAppVersion();
            var pluginVersion = GetPluginVersion();
            var events = Events.ToList();

            return new InputDiagnosticReport
            {
                GeneratedAt = DateTimeOffset.Now,
                AppVersion = appVersion,
                PluginVersion = pluginVersion,
                MachineName = Environment.MachineName,
                OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Source = source,
                Events = events,
                Summary = new InputDiagnosticSummary
                {
                    EventCount = events.Count,
                    MacroCount = events.Count(e => e.EventType == "MacroResolved"),
                    MappedActionCount = events.Count(e => e.EventType == "InputMapped"),
                    RejectedCount = events.Count(e => e.EventType == "InputFiltered" && e.Accepted == false),
                    IssuesCount = 0,
                },
            };
        }

        private static string GetAppVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }

        private static string GetPluginVersion()
        {
            var assembly = typeof(YMMKeyboardPlugin.Plugin.MyToolPlugin).Assembly;
            return assembly.GetName().Version?.ToString()
                ?? "unknown";
        }
    }
}
