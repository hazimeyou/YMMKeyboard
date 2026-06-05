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

    public static void RecordDispatchSkipped(KeyEvent input, string dispatchType, string target, string payloadSummary, string result)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "DispatchSkipped",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                DispatchType = dispatchType,
                Target = target,
                PayloadSummary = payloadSummary,
                Result = result,
            });
            FlushLocked();
        }
    }

    public static void RecordDispatchExecuted(KeyEvent input, string dispatchType, string target, string payloadSummary, string result)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "DispatchExecuted",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                DispatchType = dispatchType,
                Target = target,
                PayloadSummary = payloadSummary,
                Succeeded = true,
                Result = result,
            });
            FlushLocked();
        }
    }

    public static void RecordDispatchFailed(KeyEvent input, string dispatchType, string target, string payloadSummary, string result, Exception ex)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "DispatchFailed",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                DispatchType = dispatchType,
                Target = target,
                PayloadSummary = payloadSummary,
                Succeeded = false,
                Result = result,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                ExceptionMessage = ex.Message,
            });
            FlushLocked();
        }
    }

    public static void RecordRotaryAccumulated(KeyEvent input, string switchName, string direction, int count, int threshold)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "RotaryAccumulated",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                FilterName = switchName,
                MappingSource = $"direction={direction}; count={count}; threshold={threshold}",
            });
            FlushLocked();
        }
    }

    public static void RecordRotaryFiltered(KeyEvent input, string switchName, string direction, int count, int threshold, string reason)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "RotaryFiltered",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                FilterName = switchName,
                Accepted = false,
                RejectReason = reason,
                MappingSource = $"direction={direction}; count={count}; threshold={threshold}",
            });
            FlushLocked();
        }
    }

    public static void RecordRotaryDispatched(KeyEvent input, string switchName, string direction, int count, int threshold, string payloadSummary)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "RotaryDispatched",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                FilterName = switchName,
                MappingSource = $"direction={direction}; count={count}; threshold={threshold}",
                PayloadSummary = payloadSummary,
                Result = "dispatched",
            });
            FlushLocked();
        }
    }

    public static void RecordRotaryIgnoredRelease(KeyEvent input, string switchName, string direction)
    {
        lock (sync)
        {
            EnsureSession();
            session!.Events.Add(new InputDiagnosticEvent
            {
                EventType = "RotaryIgnoredRelease",
                Timestamp = DateTimeOffset.Now,
                TransportType = input.TransportType,
                SourceDevice = input.SourceDevice,
                RawInput = input.RawInput,
                InputId = input.InputId,
                FilterName = switchName,
                RejectReason = $"direction={direction}; release ignored",
                Accepted = false,
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
                    IssuesCount = events.Count(e =>
                        e.EventType is "InputFiltered" or "DispatchFailed" or "DispatchSkipped"
                        && (e.EventType != "InputFiltered" || e.Accepted == false)),
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
