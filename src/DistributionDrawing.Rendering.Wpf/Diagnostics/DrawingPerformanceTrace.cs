using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace DistributionDrawing.Rendering.Wpf.Diagnostics;

public static class DrawingPerformanceTrace
{
    private static readonly AsyncLocal<TraceContext?> Current = new();

    public static bool IsEnabled => DrawingPerformanceEventSource.Log.IsEnabled();

    public static void BeginGesture(string gestureKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gestureKind);
        if (!IsEnabled)
        {
            return;
        }

        var gesture = new GestureState(Guid.NewGuid(), gestureKind, Stopwatch.GetTimestamp());
        Current.Value = new TraceContext(gesture, 0, 0, string.Empty, 0);
        DrawingPerformanceEventSource.Log.GestureStart(
            gesture.Id.ToString("N"),
            gestureKind,
            gesture.StartTimestamp,
            Stopwatch.Frequency);
    }

    public static void EndGesture(string outcome)
    {
        TraceContext? context = Current.Value;
        if (context is null)
        {
            return;
        }

        GestureState gesture = context.Gesture;
        if (context.UpdateId != 0)
        {
            gesture.PendingEndOutcome ??= outcome;
            return;
        }

        EmitGestureStop(gesture, outcome);
        Current.Value = null;
    }

    private static void EmitGestureStop(GestureState gesture, string outcome)
    {
        long now = Stopwatch.GetTimestamp();
        DrawingPerformanceEventSource.Log.GestureStop(
            gesture.Id.ToString("N"),
            gesture.Kind,
            outcome,
            now,
            now - gesture.StartTimestamp,
            gesture.AcceptedCount,
            gesture.RejectedCount,
            gesture.UnchangedCount);
    }

    public static UpdateOperation BeginUpdate()
    {
        TraceContext? before = Current.Value;
        if (before is null || !IsEnabled)
        {
            return UpdateOperation.Disabled;
        }

        long updateId = ++before.Gesture.NextUpdateId;
        long startTimestamp = Stopwatch.GetTimestamp();
        Current.Value = before with
        {
            UpdateId = updateId,
            BuildAttemptId = 0,
            AttemptKind = string.Empty,
            SceneBuildId = 0
        };
        return new UpdateOperation(before, before.Gesture, updateId, startTimestamp);
    }

    private static IDisposable BeginBuildAttempt(string attemptKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptKind);
        TraceContext? before = Current.Value;
        if (before is null || !IsEnabled)
        {
            return DisabledScope.Instance;
        }

        long attemptId = ++before.Gesture.NextBuildAttemptId;
        Current.Value = before with
        {
            BuildAttemptId = attemptId,
            AttemptKind = attemptKind,
            SceneBuildId = 0
        };
        return new ContextScope(before);
    }

    public static void RunBuildAttempt(string attemptKind, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using IDisposable context = BeginBuildAttempt(attemptKind);
        using PhaseOperation attempt = Measure("BuildAttempt");
        try
        {
            action();
        }
        catch
        {
            attempt.SetOutcome("Failed");
            throw;
        }
    }

    public static PhaseOperation BeginSceneBuild()
    {
        TraceContext? before = Current.Value;
        if (before is null || !IsEnabled)
        {
            return PhaseOperation.Disabled;
        }

        long sceneBuildId = ++before.Gesture.NextSceneBuildId;
        Current.Value = before with { SceneBuildId = sceneBuildId };
        return new PhaseOperation("SceneBuild", null, before);
    }

    public static PhaseOperation Measure(string phaseName, Guid? connectionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        return Current.Value is null || !IsEnabled
            ? PhaseOperation.Disabled
            : new PhaseOperation(phaseName, connectionId, null);
    }

    public sealed class UpdateOperation : IDisposable
    {
        internal static readonly UpdateOperation Disabled = new();
        private readonly TraceContext? _before;
        private readonly GestureState? _gesture;
        private readonly long _updateId;
        private readonly long _startTimestamp;
        private bool _completed;

        private UpdateOperation()
        {
        }

        internal UpdateOperation(
            TraceContext before,
            GestureState gesture,
            long updateId,
            long startTimestamp)
        {
            _before = before;
            _gesture = gesture;
            _updateId = updateId;
            _startTimestamp = startTimestamp;
        }

        public void Complete(string outcome)
        {
            if (_gesture is null || _completed)
            {
                return;
            }

            _completed = true;
            switch (outcome)
            {
                case "Accepted":
                    _gesture.AcceptedCount++;
                    break;
                case "Rejected":
                    _gesture.RejectedCount++;
                    break;
                case "Unchanged":
                    _gesture.UnchangedCount++;
                    break;
            }

            long now = Stopwatch.GetTimestamp();
            DrawingPerformanceEventSource.Log.Update(
                _gesture.Id.ToString("N"),
                _updateId,
                outcome,
                _startTimestamp,
                now - _startTimestamp);
        }

        public void Dispose()
        {
            if (_gesture is null)
            {
                return;
            }

            if (!_completed)
            {
                Complete("Incomplete");
            }
            if (_gesture.PendingEndOutcome is { } pendingEndOutcome)
            {
                EmitGestureStop(_gesture, pendingEndOutcome);
                Current.Value = null;
                return;
            }
            if (Current.Value is not null)
            {
                Current.Value = _before;
            }
        }
    }

    public sealed class PhaseOperation : IDisposable
    {
        internal static readonly PhaseOperation Disabled = new();
        private readonly string? _phaseName;
        private readonly Guid? _connectionId;
        private readonly TraceContext? _restoreContext;
        private readonly TraceContext? _context;
        private readonly long _startTimestamp;
        private int _itemCount;
        private int _secondaryCount;
        private int _tertiaryCount;
        private int _quaternaryCount;
        private string _outcome = "Completed";
        private bool _disposed;

        private PhaseOperation()
        {
        }

        internal PhaseOperation(
            string phaseName,
            Guid? connectionId,
            TraceContext? restoreContext)
        {
            _phaseName = phaseName;
            _connectionId = connectionId;
            _restoreContext = restoreContext;
            _context = Current.Value;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void SetCounts(
            int itemCount,
            int secondaryCount = 0,
            int tertiaryCount = 0,
            int quaternaryCount = 0)
        {
            _itemCount = itemCount;
            _secondaryCount = secondaryCount;
            _tertiaryCount = tertiaryCount;
            _quaternaryCount = quaternaryCount;
        }

        public void SetOutcome(string outcome)
        {
            _outcome = outcome;
        }

        public void Dispose()
        {
            if (_phaseName is null || _context is null || _disposed)
            {
                return;
            }

            _disposed = true;
            long now = Stopwatch.GetTimestamp();
            DrawingPerformanceEventSource.Log.Phase(
                _phaseName,
                _context.Gesture.Id.ToString("N"),
                _context.UpdateId,
                _context.BuildAttemptId,
                _context.SceneBuildId,
                _connectionId?.ToString("N") ?? string.Empty,
                _context.AttemptKind,
                _startTimestamp,
                now - _startTimestamp,
                _itemCount,
                _secondaryCount,
                _tertiaryCount,
                _quaternaryCount,
                _outcome);
            if (_restoreContext is not null)
            {
                Current.Value = _restoreContext;
            }
        }
    }

    private sealed class ContextScope(TraceContext before) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Current.Value = before;
        }
    }

    private sealed class DisabledScope : IDisposable
    {
        public static readonly DisabledScope Instance = new();

        public void Dispose()
        {
        }
    }

    internal sealed class GestureState(Guid id, string kind, long startTimestamp)
    {
        public Guid Id { get; } = id;
        public string Kind { get; } = kind;
        public long StartTimestamp { get; } = startTimestamp;
        public long NextUpdateId { get; set; }
        public long NextBuildAttemptId { get; set; }
        public long NextSceneBuildId { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int UnchangedCount { get; set; }
        public string? PendingEndOutcome { get; set; }
    }

    internal sealed record TraceContext(
        GestureState Gesture,
        long UpdateId,
        long BuildAttemptId,
        string AttemptKind,
        long SceneBuildId);
}

[EventSource(Name = "DistributionDrawing-Performance")]
internal sealed class DrawingPerformanceEventSource : EventSource
{
    public static readonly DrawingPerformanceEventSource Log = new();

    [Event(1, Level = EventLevel.Informational)]
    public void GestureStart(
        string gestureId,
        string gestureKind,
        long timestamp,
        long stopwatchFrequency) =>
        WriteEvent(1, gestureId, gestureKind, timestamp, stopwatchFrequency);

    [Event(2, Level = EventLevel.Informational)]
    public void GestureStop(
        string gestureId,
        string gestureKind,
        string outcome,
        long timestamp,
        long durationTicks,
        int acceptedCount,
        int rejectedCount,
        int unchangedCount) =>
        WriteEvent(2, gestureId, gestureKind, outcome, timestamp, durationTicks,
            acceptedCount, rejectedCount, unchangedCount);

    [Event(3, Level = EventLevel.Informational)]
    public void Update(
        string gestureId,
        long updateId,
        string outcome,
        long timestamp,
        long durationTicks) =>
        WriteEvent(3, gestureId, updateId, outcome, timestamp, durationTicks);

    [Event(4, Level = EventLevel.Informational)]
    public void Phase(
        string phaseName,
        string gestureId,
        long updateId,
        long buildAttemptId,
        long sceneBuildId,
        string connectionId,
        string attemptKind,
        long timestamp,
        long durationTicks,
        int itemCount,
        int secondaryCount,
        int tertiaryCount,
        int quaternaryCount,
        string outcome) =>
        WriteEvent(4, phaseName, gestureId, updateId, buildAttemptId, sceneBuildId,
            connectionId, attemptKind, timestamp, durationTicks, itemCount,
            secondaryCount, tertiaryCount, quaternaryCount, outcome);
}
