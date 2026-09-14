using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class DragPreviewTransactionCoordinatorTests
{
    [Fact]
    public void ExpectedRoutingInvalidity_RollsBackKeepsGestureAndUsesNonModalFeedback()
    {
        var drag = new FakeDrag();
        int buildCalls = 0;
        int publishedScenes = 0;
        var feedback = new List<string>();
        int clears = 0;

        DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ValidateAndPublish(
            drag,
            () =>
            {
                buildCalls++;
                if (buildCalls is 1 or 3)
                {
                    throw new RoutingConstraintException("expected candidate invalidity");
                }
                publishedScenes++;
            },
            feedback.Add,
            () => clears++);

        Assert.Equal(DragPreviewOutcome.Rejected, outcome);
        Assert.Equal(DragPreviewOutcome.Rejected,
            DragPreviewTransactionCoordinator.ValidateAndPublish(
                drag,
                () =>
                {
                    buildCalls++;
                    if (buildCalls is 1 or 3)
                    {
                        throw new RoutingConstraintException("expected candidate invalidity");
                    }
                    publishedScenes++;
                },
                feedback.Add,
                () => clears++));

        Assert.Equal(2, drag.RollbackCount);
        Assert.Equal(0, drag.AcceptCount);
        Assert.True(drag.IsActive);
        Assert.Equal(4, buildCalls);
        Assert.Equal(2, publishedScenes);
        Assert.Equal(
            [
                DragPreviewTransactionCoordinator.InvalidCandidateFeedback,
                DragPreviewTransactionCoordinator.InvalidCandidateFeedback
            ],
            feedback);
        Assert.Equal(0, clears);
    }

    [Fact]
    public void ValidCandidate_PublishesAcceptsAndClearsFeedback()
    {
        var drag = new FakeDrag();
        int publishedScenes = 0;
        int feedbackCalls = 0;
        int clears = 0;

        DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ValidateAndPublish(
            drag,
            () => publishedScenes++,
            _ => feedbackCalls++,
            () => clears++);

        Assert.Equal(DragPreviewOutcome.Accepted, outcome);
        Assert.Equal(1, publishedScenes);
        Assert.Equal(1, drag.AcceptCount);
        Assert.Equal(0, drag.RollbackCount);
        Assert.Equal(0, feedbackCalls);
        Assert.Equal(1, clears);
    }

    [Fact]
    public void UnexpectedInvariantFailure_IsNotConvertedToExpectedInvalidFeedback()
    {
        var drag = new FakeDrag();
        int feedbackCalls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            DragPreviewTransactionCoordinator.ValidateAndPublish(
                drag,
                () => throw new InvalidOperationException("structural invariant"),
                _ => feedbackCalls++,
                () => { }));

        Assert.Equal(0, drag.AcceptCount);
        Assert.Equal(0, drag.RollbackCount);
        Assert.Equal(0, feedbackCalls);
        Assert.True(drag.IsActive);
    }

    [Fact]
    public void GroundingPointTypedSceneFailure_RollsBackAndAllowsSubsequentValidPreview()
    {
        PoleCreationResult pole = new PoleCreationFactory().Create("P-1");
        var document = new DrawingDocument(Guid.NewGuid(), "Grounding preview transaction");
        document.AddDevice(pole.Pole);
        foreach (ElectricalNode node in pole.ElectricalNodes) document.AddElectricalNode(node);
        foreach (Terminal terminal in pole.Terminals) document.AddTerminal(terminal);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            Assert.Single(pole.Pole.OverheadAnchorTerminalIds),
            "Grounding fixture",
            "S01");
        var layout = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        var hit = new SelectionHitTestEntry(
            new SelectionReference(SelectionTargetKind.GroundingPoint, point.GroundingPointId),
            new DocumentRect(0, 0, 10, 10),
            80);
        var controller = new GroundingPointDragController();
        var feedback = new List<string>();
        int clears = 0;

        Assert.True(controller.TryBeginDrag(hit, new DocumentPoint(0, 0), document, layout));
        Assert.Equal(DragPreviewOutcome.Accepted,
            DragPreviewTransactionCoordinator.UpdateValidateAndPublish(
                controller,
                () => controller.UpdatePreview(new DocumentPoint(10, 12)),
                () => { },
                feedback.Add,
                () => clears++));

        int invalidBuildCalls = 0;
        Assert.Equal(DragPreviewOutcome.Rejected,
            DragPreviewTransactionCoordinator.UpdateValidateAndPublish(
                controller,
                () => controller.UpdatePreview(new DocumentPoint(30, 35)),
                () =>
                {
                    if (invalidBuildCalls++ == 0)
                    {
                        throw new RoutingConstraintException("injected typed scene failure");
                    }
                },
                feedback.Add,
                () => clears++));
        Assert.True(controller.IsActive);
        Assert.Equal(new DocumentPoint(10, 12),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        Assert.Equal(point.Target, document.GetGroundingPoint(point.GroundingPointId).Target);

        Assert.Equal(DragPreviewOutcome.Accepted,
            DragPreviewTransactionCoordinator.UpdateValidateAndPublish(
                controller,
                () => controller.UpdatePreview(new DocumentPoint(15, 18)),
                () => { },
                feedback.Add,
                () => clears++));
        ICommand command = Assert.IsType<MoveGroundingPointLayoutCommand>(controller.Commit());
        command.Execute();
        Assert.Equal(new DocumentPoint(15, 18),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        Assert.Equal([DragPreviewTransactionCoordinator.InvalidCandidateFeedback], feedback);
        Assert.Equal(2, clears);
    }

    [Theory]
    [MemberData(nameof(UnexpectedFailures))]
    public void UnexpectedFailureTypes_UseModalBoundaryAndNeverExpectedFeedback(
        Func<Exception> exceptionFactory)
    {
        var drag = new FakeDrag();
        int feedbackCalls = 0;
        int clearCalls = 0;
        int cancelCalls = 0;
        var modalErrors = new List<Exception>();

        DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ProcessPointerUpdate(
            drag,
            () => throw exceptionFactory(),
            () => { },
            _ => feedbackCalls++,
            () => clearCalls++,
            () => cancelCalls++,
            modalErrors.Add);

        Assert.Equal(DragPreviewOutcome.UnexpectedFailure, outcome);
        Assert.Equal(0, drag.RollbackCount);
        Assert.Equal(0, drag.AcceptCount);
        Assert.Equal(0, feedbackCalls);
        Assert.Equal(0, clearCalls);
        Assert.Equal(1, cancelCalls);
        Assert.IsType(exceptionFactory().GetType(), Assert.Single(modalErrors));
    }

    public static TheoryData<Func<Exception>> UnexpectedFailures => new()
    {
        () => new InvalidOperationException("invariant"),
        () => new ArgumentOutOfRangeException("value"),
        () => new KeyNotFoundException("mapping")
    };

    [Fact]
    public void GroundingPointNonFiniteCandidate_RollsBackWithoutModalAndThenResumes()
    {
        GroundingFixture fixture = CreateGroundingFixture();
        var controller = new GroundingPointDragController();
        var feedback = new List<string>();
        int clears = 0;
        int modalCalls = 0;
        int cancelCalls = 0;
        DocumentPoint sceneOffset = new(0, 0);

        Assert.True(controller.TryBeginDrag(
            fixture.Hit,
            new DocumentPoint(0, 0),
            fixture.Document,
            fixture.Layout));
        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(10, 12)),
            () => sceneOffset = GroundingOffset(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));

        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(double.NaN, 35)),
            () => sceneOffset = GroundingOffset(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.True(controller.IsActive);
        Assert.Equal(new DocumentPoint(10, 12), GroundingOffset(fixture));
        Assert.Equal(new DocumentPoint(10, 12), sceneOffset);
        Assert.Equal(fixture.Point.Target,
            fixture.Document.GetGroundingPoint(fixture.Point.GroundingPointId).Target);
        Assert.Single(feedback);
        Assert.Equal(0, modalCalls);
        Assert.Equal(0, cancelCalls);

        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(15, 18)),
            () => sceneOffset = GroundingOffset(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.Equal(new DocumentPoint(15, 18), sceneOffset);
        Assert.Equal(2, clears);
    }

    [Fact]
    public void CableGuideNonFiniteCandidate_RollsBackContinuesAndSupportsReleaseCancelHistory()
    {
        Guid cableId = Guid.NewGuid();
        SelectionHitTestEntry[] segments = CableSegments(cableId);
        RuntimeLayoutDocument layout = Runtime();
        var controller = new CableRouteDragController();
        var feedback = new List<string>();
        int clears = 0;
        int modalCalls = 0;
        int cancelCalls = 0;
        double sceneY = 50;

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, 80)),
            () => sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters,
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        bool rejectInjectedScene = true;
        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, 95)),
            () =>
            {
                sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters;
                if (rejectInjectedScene)
                {
                    rejectInjectedScene = false;
                    throw new RoutingConstraintException("injected cable scene failure");
                }
            },
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.True(controller.IsActive);
        Assert.Equal(80, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.Equal(80, sceneY);
        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, double.PositiveInfinity)),
            () => sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters,
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.True(controller.IsActive);
        Assert.Equal(80, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.Equal(80, sceneY);
        Assert.Equal(2, feedback.Count);
        Assert.Equal(0, modalCalls);

        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, 110)),
            () => sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters,
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        ICommand command = Assert.IsType<SetCableRouteGuideCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.Empty(layout.CableRouteGuides);
        Assert.True(stack.Redo());
        Assert.Equal(110, layout.CableRouteGuides[cableId].HorizontalYMillimeters);

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, 125)),
            () => sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters,
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(40, double.NegativeInfinity)),
            () => sceneY = layout.CableRouteGuides[cableId].HorizontalYMillimeters,
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.True(controller.Cancel());
        clears++;
        Assert.Equal(110, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.Null(controller.Commit());
        Assert.True(clears >= 3);
        Assert.Equal(0, modalCalls);
    }

    [Fact]
    public void TransformerNonFiniteCandidate_PreservesSelectionAndThenResumes()
    {
        TransformerFixture fixture = CreateTransformerFixture();
        var controller = new DeviceDragController();
        SelectionReference target = new(
            SelectionTargetKind.Device,
            fixture.Command.Creation.Transformer.Id);
        var selection = new SelectionManager();
        selection.Select(target);
        var feedback = new List<string>();
        int clears = 0;
        int modalCalls = 0;
        int cancelCalls = 0;
        DocumentPoint scenePosition = fixture.Command.Creation.Layout.Position;

        Assert.True(controller.TryBeginDrag(
            target,
            fixture.Command.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));
        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(130, 140)),
            () => scenePosition = TransformerPosition(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(double.NaN, 160)),
            () => scenePosition = TransformerPosition(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));

        Assert.True(controller.IsActive);
        Assert.Equal(new DocumentPoint(130, 140), TransformerPosition(fixture));
        Assert.Equal(new DocumentPoint(130, 140), scenePosition);
        Assert.Equal(SelectionTargetKind.Device, selection.Selected?.Kind);
        Assert.Equal(target.ObjectId, selection.Selected?.ObjectId);
        Assert.Single(feedback);
        Assert.Equal(0, modalCalls);

        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(150, 175)),
            () => scenePosition = TransformerPosition(fixture),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.Equal(new DocumentPoint(150, 175), scenePosition);
        Assert.Equal(2, clears);
    }

    [Fact]
    public void MixedGroupInjectedSceneFailure_AutomaticallyRollsBackEveryRootAndContinues()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Mixed group transaction");
        RuntimeLayoutDocument layout = Runtime();
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(document, layout, new DocumentPoint(10, 20));
        pole.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            document,
            layout,
            TransformerKind.PublicIndoor,
            new DocumentPoint(100, 120),
            "Group transformer");
        transformer.Execute();
        SelectionReference poleTarget = new(SelectionTargetKind.Device, pole.Pole.Id);
        SelectionReference transformerTarget = new(
            SelectionTargetKind.Device,
            transformer.Creation.Transformer.Id);
        SelectionSet selected = SelectionSet.Create([poleTarget, transformerTarget]);
        var controller = new DeviceDragController();
        var feedback = new List<string>();
        int clears = 0;
        int modalCalls = 0;
        int cancelCalls = 0;
        GroupScene scene = CurrentGroupScene(layout, pole.Pole.Id,
            transformer.Creation.Transformer.Id);

        Assert.True(controller.TryBeginGroupDrag(
            selected,
            poleTarget,
            pole.Layout.Position,
            document,
            layout));
        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(30, 45)),
            () => scene = CurrentGroupScene(layout, pole.Pole.Id,
                transformer.Creation.Transformer.Id),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        GroupScene validA = scene;

        bool rejectCandidate = true;
        Assert.Equal(DragPreviewOutcome.Rejected, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(55, 70)),
            () =>
            {
                scene = CurrentGroupScene(layout, pole.Pole.Id,
                    transformer.Creation.Transformer.Id);
                if (rejectCandidate)
                {
                    rejectCandidate = false;
                    throw new RoutingConstraintException("injected mixed-group scene failure");
                }
            },
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        Assert.True(controller.IsActive);
        Assert.Equal(validA, CurrentGroupScene(layout, pole.Pole.Id,
            transformer.Creation.Transformer.Id));
        Assert.Equal(validA, scene);
        Assert.Single(feedback);
        Assert.Equal(0, modalCalls);

        Assert.Equal(DragPreviewOutcome.Accepted, Process(
            controller,
            () => controller.UpdatePreview(new DocumentPoint(65, 80)),
            () => scene = CurrentGroupScene(layout, pole.Pole.Id,
                transformer.Creation.Transformer.Id),
            feedback,
            ref clears,
            ref cancelCalls,
            ref modalCalls));
        GroupScene validC = scene;
        GroupMoveCommand command = Assert.IsType<GroupMoveCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.Equal(validC, CurrentGroupScene(layout, pole.Pole.Id,
            transformer.Creation.Transformer.Id));
        Assert.True(stack.Undo());
        Assert.Equal(new GroupScene(pole.Layout.Position, transformer.Creation.Layout.Position),
            CurrentGroupScene(layout, pole.Pole.Id, transformer.Creation.Transformer.Id));
        Assert.True(stack.Redo());
        Assert.Equal(validC, CurrentGroupScene(layout, pole.Pole.Id,
            transformer.Creation.Transformer.Id));
    }

    [Fact]
    public void RealTransformerDragCommitFailure_RestoresLayoutSceneSelectionAndHistory()
    {
        TransformerFixture fixture = CreateTransformerFixture();
        var builder = new DrawingSceneBuilder();
        DrawingScene currentScene = builder.Build(fixture.Document, fixture.Layout);
        var controller = new DeviceDragController();
        SelectionReference target = new(
            SelectionTargetKind.Device,
            fixture.Command.Creation.Transformer.Id);
        var selection = new SelectionManager();
        selection.Select(target);
        DocumentPoint start = fixture.Command.Creation.Layout.Position;
        DocumentPoint valid = new(145, 165);

        Assert.True(controller.TryBeginDrag(
            target,
            start,
            fixture.Layout,
            document: fixture.Document));
        Assert.Equal(DragPreviewOutcome.Accepted,
            DragPreviewTransactionCoordinator.UpdateValidateAndPublish(
                controller,
                () => controller.UpdatePreview(valid),
                () => currentScene = builder.Build(fixture.Document, fixture.Layout),
                _ => { },
                () => { }));
        ICommand dragCommand = Assert.IsType<MoveTransformerCommand>(controller.Commit());
        TransformerLayout preTransactionLayout = fixture.Layout.TransformerLayouts[target.ObjectId];
        currentScene = builder.Build(fixture.Document, fixture.Layout);
        DrawingScene preTransactionScene = currentScene;
        var stack = new CommandStack();
        stack.MarkSaved();
        CommandTransactionFailureStage? observedFailureStage = null;

        Assert.Throws<RoutingConstraintException>(() => stack.ExecuteCommand(
            dragCommand,
            () =>
            {
                currentScene = builder.Build(fixture.Document, fixture.Layout);
                throw new RoutingConstraintException("injected final scene validation failure");
            },
            failureStage =>
            {
                observedFailureStage = failureStage;
                if (failureStage == CommandTransactionFailureStage.Validation)
                {
                    dragCommand.Undo();
                }
                currentScene = preTransactionScene;
            }));

        Assert.Equal(start, TransformerPosition(fixture));
        Assert.Equal(start, preTransactionLayout.Position);
        Assert.Same(preTransactionScene, currentScene);
        Assert.Empty(stack.History);
        Assert.Equal(0, stack.CurrentIndex);
        Assert.Equal(0, stack.CurrentStateId);
        Assert.Equal(0, stack.SavedStateId);
        Assert.False(stack.IsDirty);
        Assert.Equal(CommandTransactionFailureStage.Validation, observedFailureStage);
        Assert.Equal(SelectionTargetKind.Device, selection.Selected?.Kind);
        Assert.Equal(target.ObjectId, selection.Selected?.ObjectId);

        DocumentPoint subsequent = new(175, 190);
        var subsequentCommand = new MoveTransformerCommand(
            fixture.Layout,
            target.ObjectId,
            TransformerKind.PublicIndoor,
            start,
            subsequent);
        stack.ExecuteCommand(
            subsequentCommand,
            () => currentScene = builder.Build(fixture.Document, fixture.Layout),
            failureStage =>
            {
                if (failureStage == CommandTransactionFailureStage.Validation)
                {
                    subsequentCommand.Undo();
                }
                currentScene = preTransactionScene;
            });
        Assert.Equal(subsequent, TransformerPosition(fixture));
        Assert.True(stack.Undo(
            () => currentScene = builder.Build(fixture.Document, fixture.Layout),
            () => currentScene = builder.Build(fixture.Document, fixture.Layout)));
        Assert.Equal(start, TransformerPosition(fixture));
        Assert.True(stack.Redo(
            () => currentScene = builder.Build(fixture.Document, fixture.Layout),
            () => currentScene = builder.Build(fixture.Document, fixture.Layout)));
        Assert.Equal(subsequent, TransformerPosition(fixture));
        Assert.Equal(target, selection.Selected);
    }

    private static GroupScene CurrentGroupScene(
        RuntimeLayoutDocument layout,
        Guid poleId,
        Guid transformerId) => new(
        layout.DrawingLayout.Poles[poleId].Position,
        layout.TransformerLayouts[transformerId].Position);

    private static DragPreviewOutcome Process(
        ITransactionalDragPreview controller,
        Func<bool> updatePreview,
        Action rebuildAndPublish,
        List<string> feedback,
        ref int clears,
        ref int cancelCalls,
        ref int modalCalls)
    {
        int currentClears = clears;
        int currentCancelCalls = cancelCalls;
        int currentModalCalls = modalCalls;
        DragPreviewOutcome outcome = DragPreviewTransactionCoordinator.ProcessPointerUpdate(
            controller,
            updatePreview,
            rebuildAndPublish,
            feedback.Add,
            () => currentClears++,
            () => currentCancelCalls++,
            _ => currentModalCalls++);
        clears = currentClears;
        cancelCalls = currentCancelCalls;
        modalCalls = currentModalCalls;
        return outcome;
    }

    private static GroundingFixture CreateGroundingFixture()
    {
        PoleCreationResult pole = new PoleCreationFactory().Create("P-1");
        var document = new DrawingDocument(Guid.NewGuid(), "Grounding candidate fixture");
        document.AddDevice(pole.Pole);
        foreach (ElectricalNode node in pole.ElectricalNodes) document.AddElectricalNode(node);
        foreach (Terminal terminal in pole.Terminals) document.AddTerminal(terminal);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            Assert.Single(pole.Pole.OverheadAnchorTerminalIds),
            "Grounding fixture",
            "S01");
        var hit = new SelectionHitTestEntry(
            new SelectionReference(SelectionTargetKind.GroundingPoint, point.GroundingPointId),
            new DocumentRect(0, 0, 10, 10),
            80);
        return new GroundingFixture(document, Runtime(), point, hit);
    }

    private static DocumentPoint GroundingOffset(GroundingFixture fixture) =>
        fixture.Layout.GroundingPointLayouts[fixture.Point.GroundingPointId].SymbolOffset;

    private static TransformerFixture CreateTransformerFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Transformer candidate fixture");
        RuntimeLayoutDocument layout = Runtime();
        AddTransformerCommand command = new DeviceCommandFactory().CreateAddTransformer(
            document,
            layout,
            TransformerKind.PublicIndoor,
            new DocumentPoint(100, 120),
            "Transactional transformer");
        command.Execute();
        return new TransformerFixture(document, layout, command);
    }

    private static DocumentPoint TransformerPosition(TransformerFixture fixture) =>
        fixture.Layout.TransformerLayouts[
            fixture.Command.Creation.Transformer.Id].Position;

    private static RuntimeLayoutDocument Runtime() => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>());

    private static SelectionHitTestEntry[] CableSegments(Guid cableId)
    {
        SelectionReference target = new(SelectionTargetKind.CableSegment, cableId);
        return
        [
            Segment(target, new DocumentPoint(0, 0), new DocumentPoint(0, 50)),
            Segment(target, new DocumentPoint(0, 50), new DocumentPoint(100, 50)),
            Segment(target, new DocumentPoint(100, 50), new DocumentPoint(100, 0))
        ];
    }

    private static SelectionHitTestEntry Segment(
        SelectionReference target,
        DocumentPoint start,
        DocumentPoint end) => new(
        target,
        new DocumentRect(
            Math.Min(start.XMillimeters, end.XMillimeters) - 1,
            Math.Min(start.YMillimeters, end.YMillimeters) - 1,
            Math.Abs(end.XMillimeters - start.XMillimeters) + 2,
            Math.Abs(end.YMillimeters - start.YMillimeters) + 2),
        30,
        start,
        end);

    private sealed record GroundingFixture(
        DrawingDocument Document,
        RuntimeLayoutDocument Layout,
        GroundingPoint Point,
        SelectionHitTestEntry Hit);

    private sealed record TransformerFixture(
        DrawingDocument Document,
        RuntimeLayoutDocument Layout,
        AddTransformerCommand Command);

    private sealed record GroupScene(
        DocumentPoint PolePosition,
        DocumentPoint TransformerPosition);

    private sealed class FakeDrag : ITransactionalDragPreview
    {
        public int AcceptCount { get; private set; }

        public int RollbackCount { get; private set; }

        public bool IsActive { get; private set; } = true;

        public void AcceptCurrentPreview() => AcceptCount++;

        public bool RollbackToLastValid()
        {
            RollbackCount++;
            return true;
        }
    }
}
