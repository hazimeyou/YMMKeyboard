using System.Reflection;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;

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
            LogRuntime("InputReceived", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; raw={input.RawInput}");
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
            LogRuntime("InputFiltered", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; filter={filterName}; accepted={accepted}; reason={rejectReason}");
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
            LogRuntime("InputMapped", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; action={mappedAction}; source={mappingSource}");
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
            LogRuntime("MacroResolved", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; macro={macroName}; steps={stepCount}; result={resolutionResult}");
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
            LogRuntime("DispatchPrepared", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; dispatch={dispatchType}; target={target}; payload={payloadSummary}");
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
            LogRuntime("DispatchSkipped", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; dispatch={dispatchType}; target={target}; payload={payloadSummary}; result={result}");
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
            LogRuntime("DispatchExecuted", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; dispatch={dispatchType}; target={target}; payload={payloadSummary}; result={result}");
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
            YMMKeyboardLogger.Error("DispatchFailed", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; dispatch={dispatchType}; target={target}; payload={payloadSummary}; result={result}", ex);
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
            LogRuntime("RotaryAccumulated", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; switch={switchName}; direction={direction}; count={count}; threshold={threshold}");
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
            LogRuntime("RotaryFiltered", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; switch={switchName}; direction={direction}; count={count}; threshold={threshold}; reason={reason}");
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
            LogRuntime("RotaryDispatched", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; switch={switchName}; direction={direction}; count={count}; threshold={threshold}; payload={payloadSummary}");
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
            LogRuntime("RotaryIgnoredRelease", $"transport={input.TransportType}; source={input.SourceDevice}; inputId={input.InputId}; switch={switchName}; direction={direction}");
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

    private static void LogRuntime(string eventName, string message)
    {
        if (!YMMKeyboardLogger.IsEnabled)
            return;

        YMMKeyboardLogger.Info(eventName, message);
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
