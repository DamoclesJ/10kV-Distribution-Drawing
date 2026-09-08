using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class WpEm04WindowsValidationTests
{
    [Fact]
    public void ShortButValidSpan_BuildsCompleteSceneAndKeepsMarkerInsideCapacity()
    {
        var (document, runtime, start, end, gap) = BuildLineScene(new DocumentPoint(0, 0), new DocumentPoint(120, 0), true);
        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        OrthogonalRoute route = Assert.Single(scene.Routes, item => item.ConnectionId == gap.ConnectionId);
        Assert.Contains(PoleProfessionalGeometry.GetPoleCenter(runtime.DrawingLayout.Poles[start.Pole.Id]), route.Points);
        Assert.All(route.Segments, segment => Assert.True(segment.IsHorizontal || segment.IsVertical));
        AssertNoDuplicatePoints(route);
        AssertClearance(scene, gap, ActualRenderedEnvelope(scene, start.Pole.Id, runtime.DrawingLayout), false);
        AssertMarkerWithinAdjacentCapacity(scene, gap, runtime.DrawingLayout);
    }

    [Theory]
    [InlineData(100d)]
    [InlineData(-100d)]
    public void VerticalRenderedScene_KeepsMarkerVisibleEdgeBeforeAdjacentPole(double endY)
    {
        var (document, runtime, start, end, gap) = BuildLineScene(new DocumentPoint(0, 0), new DocumentPoint(0, endY), true);
        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        OrthogonalRoute route = Assert.Single(scene.Routes, item => item.ConnectionId == gap.ConnectionId);
        SceneEllipse marker = Marker(scene, gap);
        DocumentPoint pole = PoleProfessionalGeometry.GetPoleCenter(runtime.DrawingLayout.Poles[start.Pole.Id]);
        DocumentPoint adjacent = PoleProfessionalGeometry.GetPoleCenter(runtime.DrawingLayout.Poles[end.Pole.Id]);
        DocumentPoint center = Center(marker);
        Assert.Contains(route.Segments, segment => segment.IsVertical &&
            center.XMillimeters == segment.Start.XMillimeters &&
            center.YMillimeters >= Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) &&
            center.YMillimeters <= Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters));
        AssertMarkerWithinAdjacentCapacity(scene, gap, runtime.DrawingLayout);
        AssertVerticalClearance(scene, gap, ActualRenderedEnvelope(scene, start.Pole.Id, runtime.DrawingLayout), pole, adjacent);
    }

    private static (DrawingDocument Document, RuntimeLayoutDocument Runtime, AddPoleCommand Start,
        AddPoleCommand End, GroundingAccessPoint Gap) BuildLineScene(DocumentPoint startPoint, DocumentPoint endPoint, bool addSwitch)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "test");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var factory = new DeviceCommandFactory();
        AddPoleCommand start = factory.CreateAddPole(document, runtime, startPoint);
        AddPoleCommand end = factory.CreateAddPole(document, runtime, endPoint);
        start.Execute(); end.Execute();
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine, start.Terminal.Id, end.Terminal.Id, "line", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", [start.Pole.Id, end.Pole.Id]));
        runtime.DrawingLayout.Add(new OverheadLineLayout(connection.Id, start.Layout.Position, end.Layout.Position));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(Guid.NewGuid(), connection.Id, start.Pole.Id, end.Pole.Id,
            GroundingAccessLineSide.LargerNumberSide);
        if (addSwitch)
        {
            factory.CreateAddPoleSwitchAttachment(document, runtime, start.Pole.Id, SwitchKind.IsolationSwitch,
                PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.IsolationSwitch)).Execute();
        }
        return (document, runtime, start, end, gap);
    }

    private static void AssertNoDuplicatePoints(OrthogonalRoute route)
    {
        Assert.DoesNotContain(route.Points.Zip(route.Points.Skip(1)), pair => pair.First == pair.Second);
    }

    [Theory]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void EveryLegalPoleSwitchKind_CoexistsWithGap(SwitchKind kind)
    {
        var document = new DrawingDocument(Guid.NewGuid(), kind.ToString());
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var factory = new DeviceCommandFactory();
        AddPoleCommand start = factory.CreateAddPole(document, runtime, new DocumentPoint(0, 0));
        AddPoleCommand end = factory.CreateAddPole(document, runtime, new DocumentPoint(160, 0));
        start.Execute(); end.Execute();
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            start.Terminal.Id, end.Terminal.Id, "line", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", [start.Pole.Id, end.Pole.Id]));
        runtime.DrawingLayout.Add(new OverheadLineLayout(connection.Id, start.Layout.Position, end.Layout.Position));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(Guid.NewGuid(), connection.Id,
            start.Pole.Id, end.Pole.Id, GroundingAccessLineSide.LargerNumberSide);
        var add = factory.CreateAddPoleSwitchAttachment(document, runtime, start.Pole.Id, kind,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(kind));
        add.Execute();
        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        Assert.Empty(scene.Diagnostics);
        Assert.NotNull(scene.Elements.Single(element => element.TargetId == gap.GroundingAccessPointId));
        AssertClearance(scene, gap, ActualRenderedEnvelope(scene, start.Pole.Id, runtime.DrawingLayout), false);
        AssertMarkerWithinAdjacentCapacity(scene, gap, runtime.DrawingLayout);
    }

    [Fact]
    public void GroundSwitchStandalonePoleAttachment_IsRejectedByCreationPolicy()
    {
        Assert.Throws<ArgumentException>(() => new PoleSwitchAttachmentCreationFactory().Create(
            Guid.NewGuid(), SwitchKind.GroundSwitch, new DocumentPoint(0, 0)));
    }

    [Fact]
    public void RotatedMountedSwitch_UsesRotatedRenderedBoundsAndPreservesGapIdentity()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "rotated");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var factory = new DeviceCommandFactory();
        AddPoleCommand start = factory.CreateAddPole(document, runtime, new DocumentPoint(0, 0));
        AddPoleCommand end = factory.CreateAddPole(document, runtime, new DocumentPoint(160, 0));
        start.Execute(); end.Execute();
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine, start.Terminal.Id, end.Terminal.Id, "line", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", [start.Pole.Id, end.Pole.Id]));
        runtime.DrawingLayout.Add(new OverheadLineLayout(connection.Id, start.Layout.Position, end.Layout.Position));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(Guid.NewGuid(), connection.Id, start.Pole.Id, end.Pole.Id,
            GroundingAccessLineSide.LargerNumberSide);
        var add = factory.CreateAddPoleSwitchAttachment(document, runtime, start.Pole.Id, SwitchKind.IsolationSwitch,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.IsolationSwitch));
        add.Execute();
        AttachmentLayout original = runtime.DrawingLayout.Attachments[add.Creation.Attachment.AttachmentId];
        DrawingScene baseline = new DrawingSceneBuilder().Build(document, runtime);
        DocumentRect baselineEnvelope = ActualRenderedEnvelope(baseline, start.Pole.Id, runtime.DrawingLayout);
        runtime.DrawingLayout.Replace(original.RotateBy(1));
        DrawingScene rotated = new DrawingSceneBuilder().Build(document, runtime);
        Assert.NotEqual(baselineEnvelope,
            ActualRenderedEnvelope(rotated, start.Pole.Id, runtime.DrawingLayout));
        Assert.Same(gap, document.GetGroundingAccessPoint(gap.GroundingAccessPointId));
        AssertClearance(rotated, gap, ActualRenderedEnvelope(rotated, start.Pole.Id, runtime.DrawingLayout), false);
        AssertMarkerWithinAdjacentCapacity(rotated, gap, runtime.DrawingLayout);
    }

    [Theory]
    [InlineData(0, 0, 100, 0, 20, 60)]
    [InlineData(100, 0, 0, 0, 20, 60)]
    [InlineData(0, 0, 0, 100, 20, 60)]
    [InlineData(0, 100, 0, 0, 20, 60)]
    public void RequiredWaypoint_UsesIndependentDirectionalStubRequirements(
        double startX, double startY, double endX, double endY,
        double predecessorStub, double successorStub)
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        Guid poleId = Guid.NewGuid();
        DocumentPoint waypoint = startX == endX
            ? new DocumentPoint(startX, startY + (endY - startY) * 0.3)
            : new DocumentPoint(startX + (endX - startX) * 0.3, startY);
        var request = new ConnectionRouteRequest(Guid.NewGuid(), ConnectionType.OverheadLine,
            startId, endId,
            new TerminalAnchor(startId, new DocumentPoint(startX, startY), TerminalAnchorDirection.Auto),
            new TerminalAnchor(endId, new DocumentPoint(endX, endY), TerminalAnchorDirection.Auto),
            RequiredWaypoints: [new RequiredRouteWaypoint(poleId, waypoint,
                PredecessorMinimumStubLength: predecessorStub,
                SuccessorMinimumStubLength: successorStub)]);
        OrthogonalRoute route = new OrthogonalRoutePlanner().Plan([request], []).Single();
        Assert.Contains(route.Points, point => point == waypoint);
        Assert.All(route.Segments, segment => Assert.True(segment.IsHorizontal || segment.IsVertical));
    }

    [Fact]
    public void ShortCollinearSpan_ExplicitlyFailsInsteadOfReversingFallback()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        var request = new ConnectionRouteRequest(Guid.NewGuid(), ConnectionType.OverheadLine,
            startId, endId,
            new TerminalAnchor(startId, new DocumentPoint(0, 0), TerminalAnchorDirection.Right),
            new TerminalAnchor(endId, new DocumentPoint(30, 0), TerminalAnchorDirection.Left),
            RequiredWaypoints: [new RequiredRouteWaypoint(Guid.NewGuid(), new DocumentPoint(15, 0),
                PredecessorMinimumStubLength: 20, SuccessorMinimumStubLength: 20)]);
        Assert.Throws<InvalidOperationException>(() => new OrthogonalRoutePlanner().Plan([request], []));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SwitchAndBothGaps_CoexistInEitherOrder_AndUndoRedoPreservesFacts(
        bool switchFirst, bool splitConnections)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "GAP coexistence");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var devices = new DeviceCommandFactory();
        AddPoleCommand[] poles = Enumerable.Range(0, 3).Select(index =>
        {
            AddPoleCommand command = devices.CreateAddPole(document, runtime, new DocumentPoint(index * 160, 0));
            command.Execute();
            return command;
        }).ToArray();
        Connection AddLine(AddPoleCommand start, AddPoleCommand end, Guid[] support)
        {
            var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
                start.Terminal.Id, end.Terminal.Id, "line", "10kV");
            document.AddConnection(connection);
            document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", support));
            runtime.DrawingLayout.Add(new OverheadLineLayout(connection.Id, start.Layout.Position, end.Layout.Position));
            return connection;
        }
        Connection left = AddLine(poles[0], splitConnections ? poles[1] : poles[2],
            splitConnections ? [poles[0].Pole.Id, poles[1].Pole.Id] : poles.Select(pole => pole.Pole.Id).ToArray());
        Connection right = splitConnections
            ? AddLine(poles[1], poles[2], [poles[1].Pole.Id, poles[2].Pole.Id]) : left;
        var builder = new DrawingSceneBuilder();
        var stack = new CommandStack();
        AddPoleSwitchAttachmentCommand add = devices.CreateAddPoleSwitchAttachment(document, runtime,
            poles[1].Pole.Id, SwitchKind.IsolationSwitch,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.IsolationSwitch));
        if (switchFirst) stack.ExecuteCommand(add, () => builder.Build(document, runtime));
        GroundingAccessPoint[] gaps =
        [
            document.CreateGroundingAccessPoint(Guid.NewGuid(), left.Id, poles[1].Pole.Id, poles[0].Pole.Id,
                GroundingAccessLineSide.SmallerNumberSide),
            document.CreateGroundingAccessPoint(Guid.NewGuid(), right.Id, poles[1].Pole.Id, poles[2].Pole.Id,
                GroundingAccessLineSide.LargerNumberSide)
        ];
        document.CreateGroundingPoint(Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(gaps[1].GroundingAccessPointId),
            "大号侧", "L01");
        DrawingScene initial = builder.Build(document, runtime);
        if (!switchFirst) stack.ExecuteCommand(add, () => builder.Build(document, runtime));
        DrawingScene withSwitch = builder.Build(document, runtime);
        Assert.Empty(withSwitch.Diagnostics);
        Assert.All(gaps, gap => Assert.Same(gap, document.GetGroundingAccessPoint(gap.GroundingAccessPointId)));
        DocumentRect envelope = ActualRenderedEnvelope(withSwitch, poles[1].Pole.Id, runtime.DrawingLayout);
        AssertClearance(withSwitch, gaps[0], envelope, leftSide: true);
        AssertClearance(withSwitch, gaps[1], envelope, leftSide: false);
        if (!switchFirst) Assert.True(Center(Marker(withSwitch, gaps[1])).XMillimeters > Center(Marker(initial, gaps[1])).XMillimeters);

        Assert.True(stack.Undo());
        DrawingScene withoutSwitch = builder.Build(document, runtime);
        Assert.True(Center(Marker(withoutSwitch, gaps[1])).XMillimeters < Center(Marker(withSwitch, gaps[1])).XMillimeters);
        Assert.True(stack.Redo());
        Assert.Equal(Marker(withSwitch, gaps[1]), Marker(builder.Build(document, runtime), gaps[1]));
        ICommand remove = devices.CreateRemovePoleSwitchAndBypass(document, runtime, add.Creation.Attachment.AttachmentId);
        stack.ExecuteCommand(remove, () => builder.Build(document, runtime));
        Assert.Equal(Marker(withoutSwitch, gaps[1]), Marker(builder.Build(document, runtime), gaps[1]));
        Assert.True(stack.Undo());
        Assert.Equal(Marker(withSwitch, gaps[1]), Marker(builder.Build(document, runtime), gaps[1]));
        Assert.True(stack.Redo());
        Assert.All(gaps, gap => Assert.Same(gap, document.GetGroundingAccessPoint(gap.GroundingAccessPointId)));
    }

    [Fact]
    public void RouteNormalization_PreservesReversalAtRequiredSupportPole()
    {
        var route = new OrthogonalRoute(Guid.NewGuid(), ConnectionType.OverheadLine,
            Guid.NewGuid(), Guid.NewGuid(),
            [new(48, 10.5), new(10.5, 10.5), new(200, 10.5)]);
        Assert.Contains(new DocumentPoint(10.5, 10.5), route.Points);
        Assert.Equal(2, route.Segments.Count);
    }

    [Fact]
    public void GroundingSymbol_HasStableIdentityThreeBarsNumberAndIndependentHitRegion()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Grounding symbol");
        var runtime = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>());
        var factory = new DeviceCommandFactory();
        AddPoleCommand start = factory.CreateAddPole(document, runtime, new DocumentPoint(0, 0));
        AddPoleCommand end = factory.CreateAddPole(document, runtime, new DocumentPoint(200, 0));
        start.Execute();
        end.Execute();
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            start.Terminal.Id, end.Terminal.Id, "line", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", [start.Pole.Id, end.Pole.Id]));
        runtime.DrawingLayout.Add(new OverheadLineLayout(connection.Id, start.Layout.Position, end.Layout.Position));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(Guid.NewGuid(), connection.Id,
            start.Pole.Id, end.Pole.Id, GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint gp = document.CreateGroundingPoint(Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId), "大号侧", "L01");
        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        SceneElement[] elements = scene.Elements.Where(element => element.TargetId == gp.GroundingPointId).ToArray();
        Assert.DoesNotContain(elements, element => element is SceneRectangle);
        SceneLine stem = Assert.Single(elements.OfType<SceneLine>(), line => line.Start.XMillimeters == line.End.XMillimeters);
        SceneLine[] bars = elements.OfType<SceneLine>().Where(line => line.Start.YMillimeters >= stem.End.YMillimeters)
            .OrderBy(line => line.Start.YMillimeters).ToArray();
        Assert.Equal(3, bars.Length);
        Assert.All(bars, bar => Assert.Equal(stem.End.XMillimeters, (bar.Start.XMillimeters + bar.End.XMillimeters) / 2));
        Assert.True(Width(bars[0]) > Width(bars[1]) && Width(bars[1]) > Width(bars[2]));
        Assert.Equal(DrawingMetrics.Default.Grounding.StemLength, stem.End.YMillimeters - stem.Start.YMillimeters);
        SceneText number = Assert.Single(elements.OfType<SceneText>());
        Assert.Equal("L01", number.Text);
        Assert.Equal(DrawingMetrics.Default.Typography.GroundingPointNumberFontSize, number.FontSizeMillimeters);
        Assert.Equal(new SelectionReference(SelectionTargetKind.GroundingPoint, gp.GroundingPointId),
            scene.HitTestIndex.HitTest(stem.End));
        Assert.Equal(new SelectionReference(SelectionTargetKind.GroundingAccessPoint, gap.GroundingAccessPointId),
            scene.HitTestIndex.HitTest(Center(Marker(scene, gap))));
        SelectionHitTestEntry hit = scene.HitTestIndex.Find(new SelectionReference(SelectionTargetKind.GroundingPoint, gp.GroundingPointId))!;
        var overlapping = new SelectionHitTestIndex([new(new(SelectionTargetKind.GroundingAccessPoint, gap.GroundingAccessPointId), hit.Bounds, 70), hit]);
        Assert.Equal(gp.GroundingPointId, overlapping.HitTest(stem.End)!.ObjectId);
        AssertClearance(scene, gap, ActualRenderedEnvelope(scene, start.Pole.Id, runtime.DrawingLayout), false);
    }

    private static double Width(SceneLine line) => line.End.XMillimeters - line.Start.XMillimeters;
    private static DocumentRect ActualRenderedEnvelope(DrawingScene scene, Guid poleId, DrawingLayout layout)
    {
        DocumentPoint center = PoleProfessionalGeometry.GetPoleCenter(layout.Poles[poleId]);
        DocumentRect pole = PoleProfessionalGeometry.GetPoleBounds(layout.Poles[poleId]);
        DocumentRect[] rendered = scene.Elements.OfType<SceneLogicalBounds>()
            .Select(element => element.Bounds)
            .Where(bounds => bounds != pole &&
                bounds.XMillimeters <= center.XMillimeters + 40 &&
                bounds.XMillimeters + bounds.WidthMillimeters >= center.XMillimeters - 40 &&
                bounds.YMillimeters <= center.YMillimeters + 40 &&
                bounds.YMillimeters + bounds.HeightMillimeters >= center.YMillimeters - 40)
            .ToArray();
        DocumentRect union = rendered.Aggregate(pole, Union);
        double halfStroke = DrawingMetrics.Default.Line.ConnectionThickness / 2;
        return new DocumentRect(union.XMillimeters - halfStroke, union.YMillimeters - halfStroke,
            union.WidthMillimeters + halfStroke * 2, union.HeightMillimeters + halfStroke * 2);
    }
    private static DocumentRect Union(DocumentRect first, DocumentRect second) => new(
        Math.Min(first.XMillimeters, second.XMillimeters),
        Math.Min(first.YMillimeters, second.YMillimeters),
        Math.Max(first.XMillimeters + first.WidthMillimeters, second.XMillimeters + second.WidthMillimeters) - Math.Min(first.XMillimeters, second.XMillimeters),
        Math.Max(first.YMillimeters + first.HeightMillimeters, second.YMillimeters + second.HeightMillimeters) - Math.Min(first.YMillimeters, second.YMillimeters));
    private static SceneEllipse Marker(DrawingScene scene, GroundingAccessPoint gap) =>
        Assert.IsType<SceneEllipse>(Assert.Single(scene.Elements, element => element.TargetId == gap.GroundingAccessPointId));
    private static DocumentPoint Center(SceneEllipse marker) => new(
        marker.Bounds.XMillimeters + marker.Bounds.WidthMillimeters / 2,
        marker.Bounds.YMillimeters + marker.Bounds.HeightMillimeters / 2);
    private static void AssertMarkerWithinAdjacentCapacity(
        DrawingScene scene,
        GroundingAccessPoint gap,
        DrawingLayout layout)
    {
        DocumentPoint pole = PoleProfessionalGeometry.GetPoleCenter(layout.Poles[gap.PoleId]);
        DocumentPoint adjacent = PoleProfessionalGeometry.GetPoleCenter(layout.Poles[gap.AdjacentPoleId]);
        DocumentPoint marker = Center(Marker(scene, gap));
        double radius = DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter / 2 +
            DrawingMetrics.Default.Line.ConnectionThickness / 2;
        if (adjacent.XMillimeters > pole.XMillimeters)
            Assert.True(marker.XMillimeters + radius < adjacent.XMillimeters);
        else if (adjacent.XMillimeters < pole.XMillimeters)
            Assert.True(marker.XMillimeters - radius > adjacent.XMillimeters);
        else if (adjacent.YMillimeters > pole.YMillimeters)
            Assert.True(marker.YMillimeters + radius < adjacent.YMillimeters);
        else
            Assert.True(marker.YMillimeters - radius > adjacent.YMillimeters);
    }
    private static void AssertVerticalClearance(
        DrawingScene scene,
        GroundingAccessPoint gap,
        DocumentRect envelope,
        DocumentPoint pole,
        DocumentPoint adjacent)
    {
        SceneEllipse marker = Marker(scene, gap);
        DocumentPoint center = Center(marker);
        double halfVisible = marker.Bounds.HeightMillimeters / 2 + marker.ThicknessMillimeters / 2;
        double clearance = adjacent.YMillimeters > pole.YMillimeters
            ? marker.Bounds.YMillimeters - marker.ThicknessMillimeters / 2 -
                (envelope.YMillimeters + envelope.HeightMillimeters)
            : envelope.YMillimeters -
                (marker.Bounds.YMillimeters + marker.Bounds.HeightMillimeters + marker.ThicknessMillimeters / 2);
        Assert.Equal(DrawingMetrics.Default.Line.GroundingAccessClearance, clearance, 8);
        Assert.True(halfVisible > 0 && center.YMillimeters != pole.YMillimeters);
    }
    private static void AssertClearance(DrawingScene scene, GroundingAccessPoint gap, DocumentRect envelope, bool leftSide)
    {
        SceneEllipse marker = Marker(scene, gap);
        Assert.NotNull(marker.Fill);
        Assert.Equal(DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter, marker.Bounds.WidthMillimeters);
        double clearance = leftSide
            ? envelope.XMillimeters - (marker.Bounds.XMillimeters + marker.Bounds.WidthMillimeters + marker.ThicknessMillimeters / 2)
            : marker.Bounds.XMillimeters - marker.ThicknessMillimeters / 2 - (envelope.XMillimeters + envelope.WidthMillimeters);
        Assert.Equal(DrawingMetrics.Default.Line.GroundingAccessClearance, clearance, 8);
        OrthogonalRoute route = Assert.Single(scene.Routes, candidate => candidate.ConnectionId == gap.ConnectionId);
        DocumentPoint center = Center(marker);
        Assert.Contains(route.Segments, segment => segment.IsHorizontal && center.YMillimeters == segment.Start.YMillimeters &&
            center.XMillimeters >= Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) &&
            center.XMillimeters <= Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters));
    }
}
