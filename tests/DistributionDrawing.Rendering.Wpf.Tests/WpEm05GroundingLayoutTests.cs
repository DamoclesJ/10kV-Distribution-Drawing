using System.Windows.Media;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
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

public sealed class WpEm05GroundingLayoutTests
{
    [Theory]
    [InlineData(TerminalAnchorDirection.Left)]
    [InlineData(TerminalAnchorDirection.Right)]
    [InlineData(TerminalAnchorDirection.Up)]
    [InlineData(TerminalAnchorDirection.Down)]
    public void Resolver_AppliesManualOffsetAndKeepsFixedDownwardBody(
        TerminalAnchorDirection direction)
    {
        Guid pointId = Guid.NewGuid();
        GroundingPoint point = GroundingPoint.Create(
            pointId,
            GroundingTarget.ForTerminal(Guid.NewGuid()),
            "电缆侧",
            "S01");
        var offset = new DocumentPoint(13, -31);
        var resolver = new GroundingPointLayoutResolver();

        GroundingPointResolvedLayout automatic = resolver.Resolve(
            point,
            new GroundingPresentationAnchor(new DocumentPoint(100, 100), direction),
            null);
        GroundingPointResolvedLayout manual = resolver.Resolve(
            point,
            new GroundingPresentationAnchor(new DocumentPoint(100, 100), direction),
            new GroundingPointLayout(pointId, offset));

        Assert.Equal(
            automatic.DefaultSymbolTop.XMillimeters + offset.XMillimeters,
            manual.SymbolTop.XMillimeters);
        Assert.Equal(
            automatic.DefaultSymbolTop.YMillimeters + offset.YMillimeters,
            manual.SymbolTop.YMillimeters);
        Assert.Equal(manual.SymbolTop, manual.Stem.Start);
        Assert.Equal(DrawingMetrics.Default.Grounding.StemLength, manual.Stem.Length);
        Assert.True(manual.Stem.End.YMillimeters > manual.Stem.Start.YMillimeters);
        Assert.Equal(3, manual.Bars.Count);
        Assert.All(manual.Bars, bar => Assert.True(bar.IsHorizontal));
        Assert.Equal(
            [
                DrawingMetrics.Default.Grounding.TopBarWidth,
                DrawingMetrics.Default.Grounding.MiddleBarWidth,
                DrawingMetrics.Default.Grounding.BottomBarWidth
            ],
            manual.Bars.Select(bar => bar.Length));
        Assert.NotNull(manual.NumberOrigin);
        Assert.Equal(manual.SymbolTop.XMillimeters, manual.NumberOrigin!.Value.XMillimeters);
        Assert.True(manual.NumberOrigin.Value.YMillimeters > manual.Bars[^1].Start.YMillimeters);
        Assert.All(manual.LeaderSegments, segment =>
            Assert.True(segment.IsHorizontal || segment.IsVertical));
        OrthogonalRouteSegment final = manual.LeaderSegments[^1];
        Assert.True(final.IsVertical);
        Assert.Equal(manual.SymbolTop, final.End);
        Assert.True(final.End.YMillimeters > final.Start.YMillimeters);
    }

    [Fact]
    public void GapDefaults_UseSimpleVerticalLeaderForHorizontalHalfEdgeAndDoglegForVertical()
    {
        GroundingPoint point = GroundingPoint.Create(
            Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(Guid.NewGuid()),
            "线路侧",
            "L01");
        var resolver = new GroundingPointLayoutResolver();
        var anchor = new DocumentPoint(50, 60);

        GroundingPointResolvedLayout horizontal = resolver.Resolve(
            point,
            new GroundingPresentationAnchor(anchor, TerminalAnchorDirection.Right),
            null);
        GroundingPointResolvedLayout vertical = resolver.Resolve(
            point,
            new GroundingPresentationAnchor(anchor, TerminalAnchorDirection.Up),
            null);

        Assert.Single(horizontal.LeaderSegments);
        Assert.True(horizontal.LeaderSegments[0].IsVertical);
        Assert.Equal(anchor.XMillimeters, horizontal.SymbolTop.XMillimeters);
        Assert.True(vertical.LeaderSegments.Count >= 2);
        Assert.NotEqual(anchor.XMillimeters, vertical.SymbolTop.XMillimeters);
        Assert.Equal(vertical.SymbolTop, vertical.LeaderSegments[^1].End);
    }

    [Fact]
    public void TerminalUpWithSameXSymbolBelow_RemainsOutwardFirst()
    {
        Guid pointId = Guid.NewGuid();
        GroundingPoint point = GroundingPoint.Create(
            pointId,
            GroundingTarget.ForTerminal(Guid.NewGuid()),
            "电缆终端",
            "S01");
        var anchor = new GroundingPresentationAnchor(
            new DocumentPoint(100, 100),
            TerminalAnchorDirection.Up);
        double leaderLength = DrawingMetrics.Default.Grounding.LeaderLength;
        var offset = new DocumentPoint(-leaderLength, leaderLength);

        GroundingPointResolvedLayout resolved = new GroundingPointLayoutResolver().Resolve(
            point,
            anchor,
            new GroundingPointLayout(pointId, offset));

        Assert.Equal(anchor.Position.XMillimeters, resolved.SymbolTop.XMillimeters);
        Assert.True(resolved.SymbolTop.YMillimeters > anchor.Position.YMillimeters);
        OrthogonalRouteSegment first = resolved.LeaderSegments[0];
        Assert.Equal(anchor.Position, first.Start);
        Assert.True(first.IsVertical);
        Assert.True(first.End.YMillimeters < first.Start.YMillimeters);
        Assert.All(resolved.LeaderSegments, segment =>
            Assert.True(segment.IsHorizontal || segment.IsVertical));
        Assert.Contains(resolved.LeaderSegments, segment => segment.IsHorizontal);
        Assert.Contains(resolved.LeaderSegments.Skip(1).SkipLast(1), segment =>
            segment.IsVertical);
        OrthogonalRouteSegment final = resolved.LeaderSegments[^1];
        Assert.True(final.IsVertical);
        Assert.True(final.End.YMillimeters > final.Start.YMillimeters);
        Assert.Equal(resolved.SymbolTop, final.End);
    }

    [Fact]
    public void RingCabinetCableTerminal_UsesRealDownwardOutwardDirection()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Ring grounding");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        AddRingCabinetCommand add = new DeviceCommandFactory().CreateAddRingCabinet(
            document,
            runtime,
            new RingCabinetCreationConfiguration(
                "测试柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(200, 20));
        add.Execute();
        Guid terminalId = add.Cabinet.Intervals[0].CableTerminalId!.Value;
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(), terminalId, "环网柜电缆侧", "S01");
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts);

        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            point,
            document,
            runtime.DrawingLayout,
            anchors,
            out GroundingPresentationAnchor anchor));
        Assert.Equal(TerminalAnchorDirection.Down, anchor.Direction);
        GroundingPointResolvedLayout resolved = new GroundingPointLayoutResolver().Resolve(
            point,
            anchor,
            null);
        OrthogonalRouteSegment first = resolved.LeaderSegments[0];
        Assert.Equal(anchor.Position, first.Start);
        Assert.True(first.IsVertical);
        Assert.True(first.End.YMillimeters > first.Start.YMillimeters);
        OrthogonalRouteSegment final = resolved.LeaderSegments[^1];
        Assert.True(final.IsVertical);
        Assert.True(final.End.YMillimeters > final.Start.YMillimeters);
        Assert.Equal(resolved.SymbolTop, final.End);
    }

    [Fact]
    public void GapMarkerRemainsSelectableWhenManualBodyOverlapsIt()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "GAP overlap");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        var devices = new DeviceCommandFactory();
        AddPoleCommand start = devices.CreateAddPole(document, runtime, new DocumentPoint(0, 0));
        AddPoleCommand end = devices.CreateAddPole(document, runtime, new DocumentPoint(200, 0));
        start.Execute();
        end.Execute();
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            start.Terminal.Id,
            end.Terminal.Id,
            "测试线路",
            "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(
            connection.Id,
            "JKLYJ",
            [start.Pole.Id, end.Pole.Id]));
        runtime.DrawingLayout.Add(new OverheadLineLayout(
            connection.Id,
            start.Layout.Position,
            end.Layout.Position));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            connection.Id,
            start.Pole.Id,
            end.Pole.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "测试位置",
            "L01");
        runtime.SetGroundingPointLayout(new GroundingPointLayout(
            point.GroundingPointId,
            new DocumentPoint(0, -DrawingMetrics.Default.Grounding.LeaderLength)));

        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        SceneEllipse marker = Assert.Single(scene.Elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.TargetId == gap.GroundingAccessPointId);
        DocumentPoint markerCenter = Center(marker.Bounds);
        SceneLine stem = Assert.Single(scene.Elements.OfType<SceneLine>(), line =>
            line.TargetId == point.GroundingPointId &&
            line.Start.XMillimeters == line.End.XMillimeters &&
            line.End.YMillimeters - line.Start.YMillimeters ==
            DrawingMetrics.Default.Grounding.StemLength);

        SelectionHitTestEntry markerHit = Assert.IsType<SelectionHitTestEntry>(
            scene.HitTestIndex.HitTestEntry(markerCenter));
        Assert.Equal(
            new SelectionReference(
                SelectionTargetKind.GroundingAccessPoint,
                gap.GroundingAccessPointId),
            markerHit.Target);
        SelectionHitTestEntry bodyHit = Assert.IsType<SelectionHitTestEntry>(
            scene.HitTestIndex.HitTestEntry(stem.End));
        Assert.Equal(
            new SelectionReference(
                SelectionTargetKind.GroundingPoint,
                point.GroundingPointId),
            bodyHit.Target);
        Assert.True(bodyHit.CanStartDrag);
        Assert.False(new GroundingPointDragController().TryBeginDrag(
            markerHit,
            markerCenter,
            document,
            runtime));
    }

    [Fact]
    public void DragPreviewCommitCancelAndUndoRedo_OnlyMutateSymbolOffset()
    {
        (DrawingDocument document, GroundingPoint point) = CreateGroundingDocument();
        var layout = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        var target = new SelectionReference(
            SelectionTargetKind.GroundingPoint,
            point.GroundingPointId);
        var bodyHit = new SelectionHitTestEntry(target, new DocumentRect(0, 0, 10, 10), 80);
        var leaderHit = new SelectionHitTestEntry(
            target,
            new DocumentRect(0, 0, 20, 2),
            80,
            new DocumentPoint(0, 1),
            new DocumentPoint(20, 1),
            CanStartDrag: false);
        var controller = new GroundingPointDragController();
        GroundingTarget originalTarget = point.Target;
        string? originalNumber = point.Number;
        string originalLocation = point.Location;

        Assert.False(controller.TryBeginDrag(
            leaderHit, new DocumentPoint(2, 2), document, layout));
        Assert.True(controller.TryBeginDrag(
            bodyHit, new DocumentPoint(2, 2), document, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(17, -6)));
        Assert.Equal(
            new DocumentPoint(15, -8),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        ICommand command = Assert.IsType<MoveGroundingPointLayoutCommand>(controller.Commit());
        command.Execute();
        command.Undo();
        Assert.DoesNotContain(point.GroundingPointId, layout.GroundingPointLayouts.Keys);
        command.Redo();
        Assert.Equal(
            new DocumentPoint(15, -8),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        Assert.Equal(originalTarget, point.Target);
        Assert.Equal(originalNumber, point.Number);
        Assert.Equal(originalLocation, point.Location);

        Assert.True(controller.TryBeginDrag(
            bodyHit, new DocumentPoint(0, 0), document, layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(4, 5)));
        Assert.True(controller.Cancel());
        Assert.Equal(
            new DocumentPoint(15, -8),
            layout.GroundingPointLayouts[point.GroundingPointId].SymbolOffset);
        Assert.True(controller.TryBeginDrag(
            bodyHit, new DocumentPoint(0, 0), document, layout));
        Assert.Null(controller.Commit());
    }

    [Fact]
    public void RemoveCommand_RemovesAndRestoresDomainAndLayoutTogether()
    {
        (DrawingDocument document, GroundingPoint point) = CreateGroundingDocument();
        var layout = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        var original = new GroundingPointLayout(
            point.GroundingPointId,
            new DocumentPoint(22, -9));
        layout.SetGroundingPointLayout(original);
        ICommand command = new ProfessionalCommandFactory().CreateRemoveGroundingPoint(
            document,
            layout,
            point.GroundingPointId);

        command.Execute();
        Assert.Empty(document.GroundingPoints);
        Assert.Empty(layout.GroundingPointLayouts);
        command.Undo();
        GroundingPoint restored = Assert.Single(document.GroundingPoints);
        Assert.Equal(point.GroundingPointId, restored.GroundingPointId);
        Assert.Equal(point.Target, restored.Target);
        Assert.Equal(original, layout.GroundingPointLayouts[point.GroundingPointId]);
        command.Redo();
        Assert.Empty(document.GroundingPoints);
        Assert.Empty(layout.GroundingPointLayouts);
    }

    [Fact]
    public void GroundingLeaderBridge_DecoratesLeaderWithoutChangingElectricalRoute()
    {
        var leader = new[]
        {
            new OrthogonalRouteSegment(
                new DocumentPoint(0, 50),
                new DocumentPoint(100, 50),
                0)
        };
        var electrical = new OrthogonalRoute(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new DocumentPoint(50, 0), new DocumentPoint(50, 100)]);
        DocumentPoint[] before = electrical.Points.ToArray();

        IReadOnlyList<SceneElement> projected = new GroundingLeaderCrossingDecorator().Project(
            leader,
            [electrical],
            Colors.DarkGreen,
            0.5);

        Assert.Single(projected.OfType<SceneArc>());
        Assert.Equal(before, electrical.Points);
        Assert.Equal(2, projected.OfType<SceneLine>().Count());
    }

    private static (DrawingDocument Document, GroundingPoint Point) CreateGroundingDocument()
    {
        PoleCreationResult result = new PoleCreationFactory().Create("P-1");
        var document = new DrawingDocument(Guid.NewGuid(), "WP-EM-05");
        document.AddDevice(result.Pole);
        foreach (ElectricalNode node in result.ElectricalNodes) document.AddElectricalNode(node);
        foreach (Terminal terminal in result.Terminals) document.AddTerminal(terminal);
        Guid terminalId = Assert.Single(result.Pole.OverheadAnchorTerminalIds);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            terminalId,
            "测试位置",
            "S01");
        return (document, point);
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);
}
