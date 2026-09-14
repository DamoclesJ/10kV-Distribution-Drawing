using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class TransactionalDragSliceBTests
{
    [Fact]
    public void TransformerDrag_OldInvalidPointer_IsActuallyNonCollinearAndValid()
    {
        OhlFixture fixture = CreateOhlFixture();
        var controller = new DeviceDragController();
        DocumentPoint oldPointer = new(30, 66);

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.Device,
                fixture.Transformer.Creation.Transformer.Id),
            fixture.Transformer.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));
        Assert.True(controller.UpdatePreview(oldPointer));

        TransformerLayout actualLayout = fixture.Layout.TransformerLayouts[
            fixture.Transformer.Creation.Transformer.Id];
        Assert.Equal(oldPointer, actualLayout.Position);
        DocumentPoint poleAnchor = PoleProfessionalGeometry.GetPoleCenter(fixture.Pole.Layout);
        TransformerProfessionalGeometry transformerGeometry =
            TransformerProfessionalGeometry.Create(
                fixture.Transformer.Creation.Transformer,
                actualLayout,
                DrawingMetrics.Default.Transformer);
        Assert.Equal(new DocumentPoint(40.5, 90.5), poleAnchor);
        Assert.Equal(new DocumentPoint(30, 78), transformerGeometry.HvAnchor);
        Assert.NotEqual(poleAnchor.XMillimeters, transformerGeometry.HvAnchor.XMillimeters);
        Assert.NotEqual(poleAnchor.YMillimeters, transformerGeometry.HvAnchor.YMillimeters);

        fixture.Builder.Build(fixture.Document, fixture.Layout);
    }

    [Fact]
    public void TransformerDrag_RealOhlConstraint_AllowsValidInvalidValidContinuation()
    {
        OhlFixture fixture = CreateOhlFixture();
        DrawingScene beforeScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        var controller = new DeviceDragController();
        SelectionReference target = new(
            SelectionTargetKind.Device,
            fixture.Transformer.Creation.Transformer.Id);

        Assert.True(controller.TryBeginDrag(
            target,
            fixture.Transformer.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));

        DocumentPoint validA = new(185, 120);
        Assert.True(controller.UpdatePreview(validA));
        DrawingScene validAScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        controller.AcceptCurrentPreview();

        RealInvalidTransformerCandidate invalidB =
            ApplyRealRoutingInvalidTransformerCandidate(fixture, controller);
        AssertPointEqualWithinPrecision(invalidB.Pointer, invalidB.LayoutPosition);
        Assert.Equal(
            invalidB.RequiredWaypoint.XMillimeters,
            invalidB.TransformerAnchor.XMillimeters);
        Assert.True(invalidB.Span < invalidB.RequiredStubCapacity);
        Assert.True(controller.RollbackToLastValid());
        Assert.True(controller.IsActive);
        Assert.Equal(validA, TransformerPosition(fixture));
        Assert.Equal(Route(validAScene, fixture.Line.Connection.Id),
            Route(fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Line.Connection.Id));

        DocumentPoint validC = new(210, 135);
        Assert.True(controller.UpdatePreview(validC));
        DrawingScene validCScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        controller.AcceptCurrentPreview();
        ICommand command = Assert.IsType<MoveTransformerCommand>(controller.Commit());
        Assert.Equal(fixture.Transformer.Creation.Layout.Position, TransformerPosition(fixture));

        var stack = new CommandStack();
        stack.ExecuteCommand(command, () => fixture.Builder.Build(fixture.Document, fixture.Layout));
        Assert.Equal(validC, TransformerPosition(fixture));
        Assert.Equal(Route(validCScene, fixture.Line.Connection.Id),
            Route(fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Line.Connection.Id));
        Assert.Single(stack.History);
        GroundingAccessPoint currentAccess = Assert.Single(fixture.Document.GroundingAccessPoints);
        Assert.Equal(fixture.AccessPoint.GroundingAccessPointId,
            currentAccess.GroundingAccessPointId);
        Assert.Equal(fixture.AccessPoint.ConnectionId, currentAccess.ConnectionId);
        Assert.Equal(fixture.AccessPoint.PoleId, currentAccess.PoleId);
        Assert.Equal(fixture.AccessPoint.AdjacentEndpoint, currentAccess.AdjacentEndpoint);
        Assert.Equal(fixture.AccessPoint.LineSide, currentAccess.LineSide);
        Assert.Equal(fixture.AccessPoint.PlacementSide, currentAccess.PlacementSide);
    }

    [Fact]
    public void TransformerDrag_InvalidBeforeAnyAcceptedMove_ReleasesAsNoChange()
    {
        OhlFixture fixture = CreateOhlFixture();
        DrawingScene beforeScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.Device,
                fixture.Transformer.Creation.Transformer.Id),
            fixture.Transformer.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));
        ApplyRealRoutingInvalidTransformerCandidate(fixture, controller);
        controller.RollbackToLastValid();

        Assert.Null(controller.Commit());
        Assert.Equal(fixture.Transformer.Creation.Layout.Position, TransformerPosition(fixture));
        Assert.Equal(Route(beforeScene, fixture.Line.Connection.Id),
            Route(fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Line.Connection.Id));
    }

    [Fact]
    public void TransformerDrag_InvalidRelease_CommitsLastValidAndPreservesSelection()
    {
        OhlFixture fixture = CreateOhlFixture();
        var controller = new DeviceDragController();
        SelectionReference target = new(
            SelectionTargetKind.Device,
            fixture.Transformer.Creation.Transformer.Id);
        var selection = new SelectionManager();
        selection.Select(target);

        Assert.True(controller.TryBeginDrag(
            target,
            fixture.Transformer.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));
        DocumentPoint validA = new(185, 120);
        Assert.True(controller.UpdatePreview(validA));
        DrawingScene validScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        controller.AcceptCurrentPreview();
        ApplyRealRoutingInvalidTransformerCandidate(fixture, controller);
        Assert.True(controller.RollbackToLastValid());
        Assert.Equal(target, selection.Selected);

        ICommand command = Assert.IsType<MoveTransformerCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command, () => fixture.Builder.Build(fixture.Document, fixture.Layout));

        Assert.Equal(validA, TransformerPosition(fixture));
        Assert.Equal(Route(validScene, fixture.Line.Connection.Id),
            Route(fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Line.Connection.Id));
        Assert.Single(stack.History);
        Assert.Equal(target, selection.Selected);
    }

    [Fact]
    public void TransformerDrag_CancelAfterValidAndInvalid_RestoresBefore()
    {
        OhlFixture fixture = CreateOhlFixture();
        DrawingScene beforeScene = fixture.Builder.Build(fixture.Document, fixture.Layout);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.Device,
                fixture.Transformer.Creation.Transformer.Id),
            fixture.Transformer.Creation.Layout.Position,
            fixture.Layout,
            document: fixture.Document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(185, 120)));
        fixture.Builder.Build(fixture.Document, fixture.Layout);
        controller.AcceptCurrentPreview();
        ApplyRealRoutingInvalidTransformerCandidate(fixture, controller);
        controller.RollbackToLastValid();

        Assert.True(controller.Cancel());
        Assert.False(controller.IsActive);
        Assert.Equal(fixture.Transformer.Creation.Layout.Position, TransformerPosition(fixture));
        Assert.Equal(Route(beforeScene, fixture.Line.Connection.Id),
            Route(fixture.Builder.Build(fixture.Document, fixture.Layout), fixture.Line.Connection.Id));
    }

    [Fact]
    public void MixedGroup_RollbackRestoresEveryRootAndGestureContinues()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Group transaction");
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
        SelectionSet selection = SelectionSet.Create([
            poleTarget,
            new SelectionReference(
                SelectionTargetKind.Device,
                transformer.Creation.Transformer.Id)
        ]);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            poleTarget,
            pole.Layout.Position,
            document,
            layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(30, 45)));
        controller.AcceptCurrentPreview();
        DocumentPoint poleLastValid = layout.DrawingLayout.Poles[pole.Pole.Id].Position;
        DocumentPoint transformerLastValid = layout.TransformerLayouts[
            transformer.Creation.Transformer.Id].Position;

        Assert.True(controller.UpdatePreview(new DocumentPoint(55, 70)));
        Assert.True(controller.RollbackToLastValid());
        Assert.True(controller.IsActive);
        Assert.Equal(poleLastValid, layout.DrawingLayout.Poles[pole.Pole.Id].Position);
        Assert.Equal(transformerLastValid, layout.TransformerLayouts[
            transformer.Creation.Transformer.Id].Position);

        Assert.True(controller.UpdatePreview(new DocumentPoint(65, 80)));
        controller.AcceptCurrentPreview();
        GroupMoveCommand command = Assert.IsType<GroupMoveCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.Equal(pole.Layout.Position, layout.DrawingLayout.Poles[pole.Pole.Id].Position);
        Assert.True(stack.Redo());
    }

    [Fact]
    public void GroundingPoint_RollbackUsesLastAcceptedOffsetAndPreservesIdentity()
    {
        PoleCreationResult pole = new PoleCreationFactory().Create("P-1");
        var document = new DrawingDocument(Guid.NewGuid(), "Grounding transaction");
        document.AddDevice(pole.Pole);
        foreach (ElectricalNode node in pole.ElectricalNodes) document.AddElectricalNode(node);
        foreach (Terminal terminal in pole.Terminals) document.AddTerminal(terminal);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            Assert.Single(pole.Pole.OverheadAnchorTerminalIds),
            "Grounding fixture",
            "S01");
        RuntimeLayoutDocument layout = Runtime();
        var hit = new SelectionHitTestEntry(
            new SelectionReference(SelectionTargetKind.GroundingPoint, point.GroundingPointId),
            new DocumentRect(0, 0, 10, 10),
            80);
        var controller = new GroundingPointDragController();

        Assert.True(controller.TryBeginDrag(hit, new DocumentPoint(0, 0), document, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(10, 12)));
        controller.AcceptCurrentPreview();
        Assert.True(controller.UpdatePreview(new DocumentPoint(30, 35)));
        Assert.True(controller.RollbackToLastValid());
        Assert.True(controller.IsActive);
        Assert.Equal(new DocumentPoint(10, 12),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        Assert.Equal(point.Target, document.GetGroundingPoint(point.GroundingPointId).Target);

        Assert.True(controller.UpdatePreview(new DocumentPoint(15, 18)));
        controller.AcceptCurrentPreview();
        ICommand command = Assert.IsType<MoveGroundingPointLayoutCommand>(controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());
        Assert.Equal(new DocumentPoint(15, 18),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
    }

    [Fact]
    public void CableRouteGuide_RollbackReleaseCancelAndNoChangeUseLastValid()
    {
        Guid cableId = Guid.NewGuid();
        SelectionHitTestEntry[] segments = CableSegments(cableId);
        RuntimeLayoutDocument layout = Runtime();
        var controller = new CableRouteDragController();

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 80)));
        controller.AcceptCurrentPreview();
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 95)));
        Assert.True(controller.RollbackToLastValid());
        Assert.True(controller.IsActive);
        Assert.Equal(80, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 110)));
        controller.AcceptCurrentPreview();
        ICommand command = Assert.IsType<SetCableRouteGuideCommand>(controller.Commit());
        Assert.Empty(layout.CableRouteGuides);
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.Equal(110, layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 125)));
        controller.AcceptCurrentPreview();
        Assert.True(controller.UpdatePreview(new DocumentPoint(40, 140)));
        controller.RollbackToLastValid();
        Assert.True(controller.Cancel());
        Assert.Equal(110, layout.CableRouteGuides[cableId].HorizontalYMillimeters);

        Assert.True(controller.TryBeginDrag(segments[1], segments, layout));
        Assert.Null(controller.Commit());
    }

    private static OhlFixture CreateOhlFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Real OHL transaction");
        RuntimeLayoutDocument layout = Runtime();
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(document, layout, new DocumentPoint(30, 80));
        pole.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            document,
            layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(150, 80),
            "Transactional transformer");
        transformer.Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            document,
            layout,
            pole.Terminal.Id,
            transformer.Creation.HvTerminal.Id,
            pole.Layout.Position,
            transformer.Creation.Layout.Position);
        line.Execute();
        GroundingAccessPoint accessPoint = document.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            line.Connection.Id,
            pole.Pole.Id,
            GroundingAdjacentEndpoint.ForTerminal(transformer.Creation.HvTerminal.Id),
            GroundingAccessLineSide.TransformerSide,
            GroundingAccessPlacementSide.AdjacentEndpointSide);
        return new OhlFixture(
            document,
            layout,
            pole,
            transformer,
            line,
            accessPoint,
            new DrawingSceneBuilder());
    }

    private static RealInvalidTransformerCandidate ApplyRealRoutingInvalidTransformerCandidate(
        OhlFixture fixture,
        DeviceDragController controller)
    {
        DrawingMetrics metrics = DrawingMetrics.Default;
        DocumentPoint requiredWaypoint = PoleProfessionalGeometry.GetPoleCenter(
            fixture.Pole.Layout,
            metrics);
        double requiredStubCapacity = metrics.Line.GroundingAccessClearance +
            (metrics.Line.GroundingAccessMarkerDiameter +
             metrics.Line.ConnectionThickness) / 2;
        double invalidSpan = requiredStubCapacity / 2;
        DocumentPoint candidatePosition = new(
            requiredWaypoint.XMillimeters,
            requiredWaypoint.YMillimeters + invalidSpan - metrics.Transformer.MainRadius);

        Assert.True(controller.UpdatePreview(candidatePosition));
        TransformerLayout actualLayout = fixture.Layout.TransformerLayouts[
            fixture.Transformer.Creation.Transformer.Id];
        AssertPointEqualWithinPrecision(candidatePosition, actualLayout.Position);

        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            fixture.Transformer.Creation.Transformer,
            actualLayout,
            metrics.Transformer);
        Assert.Equal(requiredWaypoint.XMillimeters, geometry.HvAnchor.XMillimeters);
        double actualSpan = Math.Abs(
            geometry.HvAnchor.YMillimeters - requiredWaypoint.YMillimeters);
        Assert.Equal(invalidSpan, actualSpan, 10);
        Assert.True(actualSpan < requiredStubCapacity);
        Assert.Throws<RoutingConstraintException>(() =>
            fixture.Builder.Build(fixture.Document, fixture.Layout));

        return new RealInvalidTransformerCandidate(
            candidatePosition,
            actualLayout.Position,
            requiredWaypoint,
            geometry.HvAnchor,
            actualSpan,
            requiredStubCapacity);
    }

    private static RuntimeLayoutDocument Runtime() => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>());

    private static void AssertPointEqualWithinPrecision(
        DocumentPoint expected,
        DocumentPoint actual)
    {
        Assert.Equal(expected.XMillimeters, actual.XMillimeters, 10);
        Assert.Equal(expected.YMillimeters, actual.YMillimeters, 10);
    }

    private static DocumentPoint TransformerPosition(OhlFixture fixture) =>
        fixture.Layout.TransformerLayouts[
            fixture.Transformer.Creation.Transformer.Id].Position;

    private static DocumentPoint[] Route(DrawingScene scene, Guid connectionId) =>
        scene.Routes.Single(item => item.ConnectionId == connectionId).Points.ToArray();

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

    private sealed record OhlFixture(
        DrawingDocument Document,
        RuntimeLayoutDocument Layout,
        AddPoleCommand Pole,
        AddTransformerCommand Transformer,
        AddOverheadLineCommand Line,
        GroundingAccessPoint AccessPoint,
        DrawingSceneBuilder Builder);

    private sealed record RealInvalidTransformerCandidate(
        DocumentPoint Pointer,
        DocumentPoint LayoutPosition,
        DocumentPoint RequiredWaypoint,
        DocumentPoint TransformerAnchor,
        double Span,
        double RequiredStubCapacity);
}
