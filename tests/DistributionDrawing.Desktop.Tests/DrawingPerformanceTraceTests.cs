using System.Diagnostics.Tracing;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Diagnostics;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

[Collection("Drawing performance diagnostics")]
public sealed class DrawingPerformanceTraceTests
{
    [Fact]
    public void TraceCorrelatesOutcomesAttemptsAndCommitWithoutChangingDragBehavior()
    {
        Assert.False(DrawingPerformanceTrace.IsEnabled);
        var disabledDrag = new FakeDrag();
        Assert.Equal(
            DragPreviewOutcome.Accepted,
            DragPreviewTransactionCoordinator.ValidateAndPublish(
                disabledDrag,
                () => { },
                _ => { },
                () => { }));
        Assert.Equal(1, disabledDrag.AcceptCount);

        ConnectionRouteRequest routeRequest = CreateRouteRequest();
        OrthogonalRoute baselineRoute = Assert.Single(
            new OrthogonalRoutePlanner().Plan([routeRequest], []));

        using var listener = new RecordingEventListener();
        Assert.True(DrawingPerformanceTrace.IsEnabled);

        var acceptedDrag = new FakeDrag();
        OrthogonalRoute? tracedRoute = null;
        DrawingPerformanceTrace.BeginGesture("DeviceDrag");
        using (DrawingPerformanceTrace.UpdateOperation update = DrawingPerformanceTrace.BeginUpdate())
        {
            DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ValidateAndPublish(
                acceptedDrag,
                () =>
                {
                    BuildMeasuredScene();
                    tracedRoute = Assert.Single(
                        new OrthogonalRoutePlanner().Plan([routeRequest], []));
                },
                _ => { },
                () => { });
            update.Complete(outcome.ToString());
            DrawingPerformanceTrace.EndGesture("Canceled");
        }

        var rejectedDrag = new FakeDrag();
        int buildCount = 0;
        DrawingPerformanceTrace.BeginGesture("CableRouteGuide");
        using (DrawingPerformanceTrace.UpdateOperation update = DrawingPerformanceTrace.BeginUpdate())
        {
            DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ValidateAndPublish(
                rejectedDrag,
                () =>
                {
                    BuildMeasuredScene();
                    if (buildCount++ == 0)
                    {
                        throw new RoutingConstraintException("expected invalid candidate");
                    }
                },
                _ => { },
                () => { });
            update.Complete(outcome.ToString());
        }
        DrawingPerformanceTrace.EndGesture("Canceled");

        DrawingPerformanceTrace.BeginGesture("GroupDrag");
        using (DrawingPerformanceTrace.UpdateOperation update = DrawingPerformanceTrace.BeginUpdate())
        {
            DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.UpdateValidateAndPublish(
                new FakeDrag(),
                () => false,
                () => throw new InvalidOperationException("unchanged must not rebuild"),
                _ => { },
                () => { });
            update.Complete(outcome.ToString());
        }
        var continuity = new RouteContinuityContext();
        continuity.BeginGesture([]);
        DragPreviewTransactionCoordinator.CommitAndPublishRelease(
            () => new NoOpCommand(),
            command =>
            {
                DrawingPerformanceTrace.RunBuildAttempt("CommitBefore", BuildMeasuredScene);
                command.Execute();
                DrawingPerformanceTrace.RunBuildAttempt("CommitAfter", BuildMeasuredScene);
            },
            BuildMeasuredScene,
            continuity);

        Assert.Equal(1, acceptedDrag.AcceptCount);
        Assert.Equal(0, acceptedDrag.RollbackCount);
        Assert.Equal(0, rejectedDrag.AcceptCount);
        Assert.Equal(1, rejectedDrag.RollbackCount);
        Assert.Equal(baselineRoute.Points, Assert.IsType<OrthogonalRoute>(tracedRoute).Points);

        DiagnosticEvent[] starts = listener.Events("GestureStart").ToArray();
        DiagnosticEvent[] stops = listener.Events("GestureStop").ToArray();
        DiagnosticEvent[] updates = listener.Events("Update").ToArray();
        DiagnosticEvent[] phases = listener.Events("Phase").ToArray();
        Assert.Equal(3, starts.Length);
        Assert.Equal(3, stops.Length);
        Assert.Equal(["Accepted", "Rejected", "Unchanged"],
            updates.Select(item => item.String("outcome")).ToArray());

        string acceptedGesture = starts.Single(item => item.String("gestureKind") == "DeviceDrag")
            .String("gestureId");
        DiagnosticEvent acceptedUpdate = updates.Single(item =>
            item.String("gestureId") == acceptedGesture);
        DiagnosticEvent acceptedScene = phases.Single(item =>
            item.String("gestureId") == acceptedGesture &&
            item.String("phaseName") == "SceneBuild");
        Assert.Equal(acceptedUpdate.Int64("updateId"), acceptedScene.Int64("updateId"));
        Assert.True(acceptedScene.Int64("buildAttemptId") > 0);
        Assert.True(acceptedScene.Int64("sceneBuildId") > 0);
        Assert.Equal("Candidate", acceptedScene.String("attemptKind"));
        DiagnosticEvent[] acceptedRoutes = phases.Where(item =>
            item.String("gestureId") == acceptedGesture &&
            item.String("phaseName") == "ConnectionRoute").ToArray();
        Assert.NotEmpty(acceptedRoutes);
        Assert.All(acceptedRoutes, item =>
            Assert.NotEqual(string.Empty, item.String("connectionId")));

        string rejectedGesture = starts.Single(item =>
            item.String("gestureKind") == "CableRouteGuide").String("gestureId");
        Assert.Contains(phases, item =>
            item.String("gestureId") == rejectedGesture &&
            item.String("attemptKind") == "Candidate");
        Assert.Contains(phases, item =>
            item.String("gestureId") == rejectedGesture &&
            item.String("attemptKind") == "Rollback");
        Assert.Contains(phases, item =>
            item.String("gestureId") == rejectedGesture &&
            item.String("phaseName") == "BuildAttempt" &&
            item.String("attemptKind") == "Candidate" &&
            item.String("outcome") == "Failed");

        string groupGesture = starts.Single(item => item.String("gestureKind") == "GroupDrag")
            .String("gestureId");
        DiagnosticEvent groupStop = stops.Single(item => item.String("gestureId") == groupGesture);
        Assert.Equal("Committed", groupStop.String("outcome"));
        Assert.Equal(1, groupStop.Int32("unchangedCount"));
        Assert.Contains(phases, item =>
            item.String("gestureId") == groupGesture &&
            item.String("phaseName") == "MouseUpCommit");
        Assert.Contains(phases, item =>
            item.String("gestureId") == groupGesture &&
            item.String("attemptKind") == "CommitBefore");
        Assert.Contains(phases, item =>
            item.String("gestureId") == groupGesture &&
            item.String("attemptKind") == "CommitAfter");

        int eventCountAfterCancel = listener.Count;
        using (DrawingPerformanceTrace.Measure("MustNotSurviveCancel"))
        {
        }
        Assert.Equal(eventCountAfterCancel, listener.Count);
    }

    private static void BuildMeasuredScene()
    {
        using DrawingPerformanceTrace.PhaseOperation scene =
            DrawingPerformanceTrace.BeginSceneBuild();
        using (DrawingPerformanceTrace.PhaseOperation obstacle =
               DrawingPerformanceTrace.Measure("ObstacleBuild"))
        {
            obstacle.SetCounts(4, 2);
        }
        using (DrawingPerformanceTrace.PhaseOperation route =
               DrawingPerformanceTrace.Measure("ConnectionRoute", Guid.NewGuid()))
        {
            route.SetCounts(3, 4);
        }
        scene.SetCounts(10, 5);
    }

    private static ConnectionRouteRequest CreateRouteRequest()
    {
        Guid connectionId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        Guid startId = Guid.Parse("71000000-0000-0000-0000-000000000002");
        Guid endId = Guid.Parse("71000000-0000-0000-0000-000000000003");
        return new ConnectionRouteRequest(
            connectionId,
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(startId, new DocumentPoint(0, 0), TerminalAnchorDirection.Right),
            new TerminalAnchor(endId, new DocumentPoint(100, 40), TerminalAnchorDirection.Left));
    }

    private sealed class FakeDrag : ITransactionalDragPreview
    {
        public int AcceptCount { get; private set; }
        public int RollbackCount { get; private set; }
        public void AcceptCurrentPreview() => AcceptCount++;
        public bool RollbackToLastValid()
        {
            RollbackCount++;
            return true;
        }
    }

    private sealed class NoOpCommand : ICommand
    {
        public void Execute()
        {
        }

        public void Undo()
        {
        }

        public void Redo()
        {
        }
    }

    private sealed class RecordingEventListener : EventListener
    {
        private readonly List<DiagnosticEvent> _events = [];

        public int Count
        {
            get
            {
                lock (_events)
                {
                    return _events.Count;
                }
            }
        }

        public IEnumerable<DiagnosticEvent> Events(string name)
        {
            lock (_events)
            {
                return _events.Where(item => item.Name == name).ToArray();
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "DistributionDrawing-Performance")
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name != "DistributionDrawing-Performance")
            {
                return;
            }

            var payload = new Dictionary<string, object?>();
            if (eventData.PayloadNames is not null && eventData.Payload is not null)
            {
                for (int index = 0; index < eventData.PayloadNames.Count; index++)
                {
                    payload[eventData.PayloadNames[index]] = eventData.Payload[index];
                }
            }

            lock (_events)
            {
                _events.Add(new DiagnosticEvent(eventData.EventName ?? string.Empty, payload));
            }
        }
    }

    private sealed record DiagnosticEvent(string Name, IReadOnlyDictionary<string, object?> Payload)
    {
        public string String(string name) => Assert.IsType<string>(Payload[name]);
        public long Int64(string name) => Assert.IsType<long>(Payload[name]);
        public int Int32(string name) => Assert.IsType<int>(Payload[name]);
    }
}

[CollectionDefinition("Drawing performance diagnostics", DisableParallelization = true)]
public sealed class DrawingPerformanceDiagnosticsCollection;
