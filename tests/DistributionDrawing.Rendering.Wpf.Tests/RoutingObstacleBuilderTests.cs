using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RoutingObstacleBuilderTests
{
    [Fact]
    public void Build_UsesProfessionalLogicalBoundsForSupportedDeviceKinds()
    {
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "柜",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open, SwitchState.Open)
            ]));
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(pole.Pole.Id, new DocumentPoint(100, 30)));
        foreach (PoleAttachment attachment in pole.Attachments)
        {
            layout.Add(new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(15, 0)));
        }

        IReadOnlyList<RoutingObstacle> obstacles = new RoutingObstacleBuilder().Build(
            new Device[] { cabinet, pole.Pole }.Concat(pole.Devices),
            pole.Attachments,
            layout,
            new Dictionary<Guid, RingCabinetLayout>
            {
                [cabinet.Id] = new RingCabinetLayoutFactory().Create(
                    cabinet,
                    new DocumentPoint(10, 10))
            },
            [new JointLayout(Guid.NewGuid(), new DocumentPoint(70, 70))]);

        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.RingCabinet);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.Pole);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.PoleAttachment);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.IntermediateTerminal);
        Assert.All(obstacles, obstacle =>
        {
            Assert.True(obstacle.Bounds.WidthMillimeters > 0);
            Assert.True(obstacle.Bounds.HeightMillimeters > 0);
        });
    }

    [Fact]
    public void Build_PTObstacleMatchesProfessionalCoilBounds()
    {
        RingCabinet cabinet = CreatePTCabinet();
        RingCabinetInterval pt = Assert.Single(
            cabinet.Intervals,
            interval => interval.IntervalKind == IntervalKind.PTInterval);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(20, 30));
        RingCabinetIntervalLayout ptLayout = layout.IntervalLayouts[pt.IntervalId];
        DocumentPoint position = Assert.IsType<DocumentPoint>(ptLayout.PTSymbolPosition);

        RoutingObstacle obstacle = Assert.Single(
            new RoutingObstacleBuilder(DrawingMetrics.Default).Build(
                [cabinet],
                [],
                new DrawingLayout(),
                new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout }),
            item => item.SourceId == pt.IntervalId);

        double diameter = DrawingMetrics.Default.PT.CoilRadius * 2;
        Assert.Equal(new DocumentRect(
            layout.Position.XMillimeters + ptLayout.RelativePosition.XMillimeters +
            position.XMillimeters,
            layout.Position.YMillimeters + ptLayout.RelativePosition.YMillimeters +
            position.YMillimeters,
            diameter,
            diameter * 2 - DrawingMetrics.Default.PT.CoilSpacing), obstacle.Bounds);
    }

    [Theory]
    [InlineData(ConnectionType.Cable)]
    [InlineData(ConnectionType.OverheadLine)]
    public void Route_BetweenOppositeSidesAvoidsPTCoilObstacle(ConnectionType type)
    {
        RingCabinet cabinet = CreatePTCabinet();
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(20, 30));
        RingCabinetInterval pt = Assert.Single(
            cabinet.Intervals,
            interval => interval.IntervalKind == IntervalKind.PTInterval);
        RoutingObstacle obstacle = Assert.Single(
            new RoutingObstacleBuilder().Build(
                [cabinet],
                [],
                new DrawingLayout(),
                new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout }),
            item => item.SourceId == pt.IntervalId);
        double centerY = obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters / 2;
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        var request = new ConnectionRouteRequest(
            Guid.NewGuid(),
            type,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(obstacle.Bounds.XMillimeters - 30, centerY),
                TerminalAnchorDirection.Right),
            new TerminalAnchor(
                endId,
                new DocumentPoint(
                    obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters + 30,
                    centerY),
                TerminalAnchorDirection.Left));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, [obstacle]);

        Assert.DoesNotContain(route.Segments, segment =>
            IntersectsInterior(segment, obstacle.Bounds));
    }

    [Fact]
    public void Route_FromPTExternalTerminalRemainsAvailable()
    {
        RingCabinet cabinet = CreatePTCabinet();
        var document = new DistributionDrawing.Domain.Documents.DrawingDocument(
            Guid.NewGuid(),
            "PT route obstacle test");
        document.AddDevice(cabinet);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(20, 30));
        RingCabinetInterval pt = Assert.Single(
            cabinet.Intervals,
            interval => interval.IntervalKind == IntervalKind.PTInterval);
        var layouts = new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout };
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            new DrawingLayout(),
            layouts);
        Assert.True(anchors.TryGet(pt.ExternalTerminalId, out TerminalAnchor start));
        RoutingObstacle[] obstacles = new RoutingObstacleBuilder().Build(
            [cabinet],
            [],
            new DrawingLayout(),
            layouts).ToArray();
        Guid endId = Guid.NewGuid();
        var request = new ConnectionRouteRequest(
            Guid.NewGuid(),
            ConnectionType.Cable,
            pt.ExternalTerminalId,
            endId,
            start,
            new TerminalAnchor(
                endId,
                new DocumentPoint(start.Position.XMillimeters + 100, start.Position.YMillimeters + 80),
                TerminalAnchorDirection.Auto));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, obstacles);

        Assert.Equal(start.Position, route.Points[0]);
        Assert.True(route.Segments[0].IsVertical);
        Assert.True(route.Segments[0].End.YMillimeters - route.Segments[0].Start.YMillimeters >=
            DrawingMetrics.Default.CableTermination.CableTerminalExitMinimumStubLength);
    }

    private static RingCabinet CreatePTCabinet() => RingCabinet.Create(
        RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "PT 柜",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    1,
                    SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreatePT(
                    2,
                    SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    3,
                    SwitchState.Open,
                    SwitchState.Open)
            ]));

    private static bool IntersectsInterior(
        OrthogonalRouteSegment segment,
        DocumentRect bounds)
    {
        if (segment.IsHorizontal)
        {
            double y = segment.Start.YMillimeters;
            return y > bounds.YMillimeters &&
                   y < bounds.YMillimeters + bounds.HeightMillimeters &&
                   Math.Max(Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters) <
                   Math.Min(Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters + bounds.WidthMillimeters);
        }

        double x = segment.Start.XMillimeters;
        return x > bounds.XMillimeters &&
               x < bounds.XMillimeters + bounds.WidthMillimeters &&
               Math.Max(Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters) <
               Math.Min(Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters + bounds.HeightMillimeters);
    }
}
