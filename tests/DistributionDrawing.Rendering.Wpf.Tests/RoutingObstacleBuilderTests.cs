using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RoutingObstacleBuilderTests
{
    [Fact]
    public void Build_UsesTransformerAndCustomerStationProfessionalBodyBoundsOnly()
    {
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(60, 50),
            "T1");
        CustomerStationCreation station = new CustomerStationCreationFactory().Create(
            StationKind.BoxStation,
            ["主供"],
            new DocumentPoint(140, 50));

        IReadOnlyList<RoutingObstacle> obstacles = new RoutingObstacleBuilder().Build(
            [transformer.Transformer, station.CustomerStation],
            [],
            new DrawingLayout(),
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [transformer.Transformer.Id] = transformer.Layout
            },
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [station.CustomerStation.Id] = station.Layout
            });

        RoutingObstacle transformerObstacle = Assert.Single(obstacles,
            item => item.SourceId == transformer.Transformer.Id);
        Assert.Equal(RoutingObstacleKind.Transformer, transformerObstacle.Kind);
        Assert.Equal(
            TransformerProfessionalGeometry.Create(
                transformer.Transformer,
                transformer.Layout,
                DrawingMetrics.Default.Transformer).Bounds,
            transformerObstacle.Bounds);

        RoutingObstacle stationObstacle = Assert.Single(obstacles,
            item => item.SourceId == station.CustomerStation.Id);
        Assert.Equal(RoutingObstacleKind.CustomerStation, stationObstacle.Kind);
        CustomerStationProfessionalGeometry stationGeometry =
            CustomerStationProfessionalGeometry.Create(
                station.CustomerStation,
                station.Layout,
                DrawingMetrics.Default.CustomerStation);
        DocumentRect expectedStationBody = Union(
            stationGeometry.Units.Select(unit => unit.Body)
                .Append(BoundsOf(stationGeometry.Roof))
                .ToArray());
        Assert.Equal(expectedStationBody, stationObstacle.Bounds);
        Assert.NotEqual(stationGeometry.Bounds, stationObstacle.Bounds);
    }

    [Theory]
    [InlineData(RoutingObstacleKind.Transformer)]
    [InlineData(RoutingObstacleKind.CustomerStation)]
    public void Route_ExcludesOnlyEndpointOwnerAndStillAvoidsOverlappingUnrelatedBody(
        RoutingObstacleKind kind)
    {
        Guid ownerId = Guid.NewGuid();
        Guid unrelatedId = Guid.NewGuid();
        var bounds = new DocumentRect(40, 35, 20, 30);
        RoutingObstacle[] obstacles =
        [
            new(ownerId, kind, bounds),
            new(unrelatedId, kind, bounds)
        ];
        ConnectionRouteRequest request = CreateHorizontalRequest(50) with
        {
            ExcludedObstacleSourceIds = new HashSet<Guid> { ownerId }
        };

        OrthogonalRoute route = new OrthogonalRouter().Route(request, obstacles);

        Assert.DoesNotContain(route.Segments, segment =>
            IntersectsInterior(segment, bounds));
        Assert.Contains(route.Points, point => point.YMillimeters != 50);
    }

    [Theory]
    [InlineData(RoutingObstacleKind.Transformer)]
    [InlineData(RoutingObstacleKind.CustomerStation)]
    public void Route_EndpointOwnerBodyCanBeExcludedByStableIdentity(
        RoutingObstacleKind kind)
    {
        Guid ownerId = Guid.NewGuid();
        var bounds = new DocumentRect(40, 35, 20, 30);
        ConnectionRouteRequest request = CreateHorizontalRequest(50) with
        {
            ExcludedObstacleSourceIds = new HashSet<Guid> { ownerId }
        };

        OrthogonalRoute route = new OrthogonalRouter().Route(
            request,
            [new RoutingObstacle(ownerId, kind, bounds)]);

        Assert.Equal(50, Assert.Single(route.Segments).Start.YMillimeters);
    }

    [Fact]
    public void SceneBuilder_OverlappingTransformerExcludesOwnersButAvoidsUnrelatedBody()
    {
        TransformerCreation start = CreateTransformer(new DocumentPoint(50, 50), "Start");
        TransformerCreation unrelated = CreateTransformer(new DocumentPoint(70, 50), "Obstacle");
        TransformerCreation end = CreateTransformer(new DocumentPoint(250, 50), "End");
        var document = new DrawingDocument(Guid.NewGuid(), "Transformer obstacle identity");
        foreach (TransformerCreation creation in new[] { start, unrelated, end })
        {
            document.AddTransformer(creation.Transformer, creation.HvTerminal);
        }
        Connection connection = AddCable(
            document,
            start.HvTerminal.Id,
            end.HvTerminal.Id);
        RuntimeLayoutDocument runtime = RuntimeWithTransformers(start, unrelated, end);
        DocumentRect unrelatedBounds = TransformerProfessionalGeometry.Create(
            unrelated.Transformer,
            unrelated.Layout,
            DrawingMetrics.Default.Transformer).Bounds;

        OrthogonalRoute route = Assert.Single(
            new DrawingSceneBuilder().Build(document, runtime).Routes,
            item => item.ConnectionId == connection.Id);

        Assert.DoesNotContain(route.Segments, segment => IntersectsInterior(
            segment,
            Expand(unrelatedBounds)));
        DocumentRect startBounds = TransformerProfessionalGeometry.Create(
            start.Transformer,
            start.Layout,
            DrawingMetrics.Default.Transformer).Bounds;
        Assert.True(Intersects(startBounds, unrelatedBounds));
    }

    [Fact]
    public void SceneBuilder_CustomerStationEndpointOwnersAreExcludedAndUnrelatedBodyIsAvoided()
    {
        CustomerStationCreation start = CreateStation(new DocumentPoint(50, 50));
        CustomerStationCreation unrelated = CreateStation(new DocumentPoint(70, 50));
        CustomerStationCreation end = CreateStation(new DocumentPoint(250, 50));
        var document = new DrawingDocument(Guid.NewGuid(), "Station obstacle identity");
        foreach (CustomerStationCreation creation in new[] { start, unrelated, end })
        {
            document.AddCustomerStation(creation.CustomerStation);
        }
        Connection connection = AddCable(
            document,
            Assert.Single(start.CustomerStation.IncomingFeeders).CableTerminalId,
            Assert.Single(end.CustomerStation.IncomingFeeders).CableTerminalId);
        RuntimeLayoutDocument runtime = RuntimeWithStations(start, unrelated, end);
        CustomerStationProfessionalGeometry unrelatedGeometry =
            CustomerStationProfessionalGeometry.Create(
                unrelated.CustomerStation,
                unrelated.Layout,
                DrawingMetrics.Default.CustomerStation);
        DocumentRect unrelatedBody = Union(
            unrelatedGeometry.Units.Select(unit => unit.Body).ToArray());

        OrthogonalRoute route = Assert.Single(
            new DrawingSceneBuilder().Build(document, runtime).Routes,
            item => item.ConnectionId == connection.Id);

        Assert.DoesNotContain(route.Segments, segment => IntersectsInterior(
            segment,
            Expand(unrelatedBody)));
    }

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
        double coilTop = layout.Position.YMillimeters +
                         ptLayout.RelativePosition.YMillimeters +
                         position.YMillimeters;
        double terminalY = layout.Position.YMillimeters +
                           ptLayout.RelativePosition.YMillimeters +
                           ptLayout.HeightMillimeters;
        double coilBottom = coilTop + diameter * 2 - DrawingMetrics.Default.PT.CoilSpacing;
        Assert.Equal(new DocumentRect(
            layout.Position.XMillimeters + ptLayout.RelativePosition.XMillimeters +
            position.XMillimeters,
            Math.Min(terminalY, coilTop),
            diameter,
            coilBottom - Math.Min(terminalY, coilTop)), obstacle.Bounds);
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
        Assert.True(anchors.TryGet(pt.CableTerminalId!.Value, out TerminalAnchor start));
        RoutingObstacle[] obstacles = new RoutingObstacleBuilder().Build(
            [cabinet],
            [],
            new DrawingLayout(),
            layouts).ToArray();
        Guid endId = Guid.NewGuid();
        var request = new ConnectionRouteRequest(
            Guid.NewGuid(),
            ConnectionType.Cable,
            pt.CableTerminalId!.Value,
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

    private static ConnectionRouteRequest CreateHorizontalRequest(double y)
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        return new ConnectionRouteRequest(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(0, y),
                TerminalAnchorDirection.Right),
            new TerminalAnchor(
                endId,
                new DocumentPoint(100, y),
                TerminalAnchorDirection.Left));
    }

    private static TransformerCreation CreateTransformer(DocumentPoint position, string name) =>
        new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            position,
            name,
            TransformerOrientation.Horizontal);

    private static CustomerStationCreation CreateStation(DocumentPoint position) =>
        new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供"],
            position);

    private static RuntimeLayoutDocument RuntimeWithTransformers(
        params TransformerCreation[] creations) => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>(),
        transformerLayouts: creations.ToDictionary(
            creation => creation.Transformer.Id,
            creation => creation.Layout));

    private static RuntimeLayoutDocument RuntimeWithStations(
        params CustomerStationCreation[] creations) => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>(),
        customerStationLayouts: creations.ToDictionary(
            creation => creation.CustomerStation.Id,
            creation => creation.Layout));

    private static Connection AddCable(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId)
    {
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            "Obstacle identity cable",
            Transformer.TenKilovolts);
        document.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(),
                "Obstacle identity cable",
                "YJV",
                100,
                Transformer.TenKilovolts,
                connectionId,
                startTerminalId,
                endTerminalId),
            connection);
        return connection;
    }

    private static DocumentRect Expand(DocumentRect bounds) => new(
        bounds.XMillimeters - DrawingMetrics.Default.Routing.ObstacleClearance,
        bounds.YMillimeters - DrawingMetrics.Default.Routing.ObstacleClearance,
        bounds.WidthMillimeters + DrawingMetrics.Default.Routing.ObstacleClearance * 2,
        bounds.HeightMillimeters + DrawingMetrics.Default.Routing.ObstacleClearance * 2);

    private static bool Intersects(DocumentRect first, DocumentRect second) =>
        first.XMillimeters < second.XMillimeters + second.WidthMillimeters &&
        first.XMillimeters + first.WidthMillimeters > second.XMillimeters &&
        first.YMillimeters < second.YMillimeters + second.HeightMillimeters &&
        first.YMillimeters + first.HeightMillimeters > second.YMillimeters;

    private static DocumentRect BoundsOf(IReadOnlyList<DocumentPoint> points)
    {
        double minX = points.Min(point => point.XMillimeters);
        double minY = points.Min(point => point.YMillimeters);
        double maxX = points.Max(point => point.XMillimeters);
        double maxY = points.Max(point => point.YMillimeters);
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static DocumentRect Union(IReadOnlyList<DocumentRect> bounds)
    {
        double minX = bounds.Min(item => item.XMillimeters);
        double minY = bounds.Min(item => item.YMillimeters);
        double maxX = bounds.Max(item => item.XMillimeters + item.WidthMillimeters);
        double maxY = bounds.Max(item => item.YMillimeters + item.HeightMillimeters);
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }

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
