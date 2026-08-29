using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class OrthogonalRoutingTests
{
    [Theory]
    [InlineData(0, 0, 80, 0)]
    [InlineData(0, 0, 0, 80)]
    public void Route_UsesDirectPathForAlignedAutoAnchors(
        double startX,
        double startY,
        double endX,
        double endY)
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000009",
            new DocumentPoint(startX, startY),
            TerminalAnchorDirection.Auto,
            new DocumentPoint(endX, endY),
            TerminalAnchorDirection.Auto);

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        Assert.Single(route.Segments);
    }

    [Fact]
    public void Route_ProducesOnlyOrthogonalSegmentsAndRespectsPortDirections()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000010",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(80, 50),
            TerminalAnchorDirection.Up);

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        Assert.All(route.Segments, segment =>
            Assert.True(segment.IsHorizontal || segment.IsVertical));
        Assert.True(route.Segments[0].IsHorizontal);
        Assert.True(route.Segments[0].End.XMillimeters >= 8);
        Assert.True(route.Segments[^1].IsVertical);
        Assert.True(route.Segments[^1].Start.YMillimeters <= 42);
    }

    [Fact]
    public void Route_AvoidsObstacleWhenAFreeCandidateExists()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000011",
            new DocumentPoint(0, 20),
            TerminalAnchorDirection.Right,
            new DocumentPoint(100, 20),
            TerminalAnchorDirection.Left);
        var obstacle = new RoutingObstacle(
            Guid.Parse("00000000-0000-0000-0000-000000000099"),
            RoutingObstacleKind.Pole,
            new DocumentRect(40, 10, 20, 20));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, [obstacle]);

        Assert.Contains(route.Points, point => point.YMillimeters <= 6 || point.YMillimeters >= 34);
        Assert.DoesNotContain(route.Segments, segment =>
            segment.IsHorizontal && segment.Start.YMillimeters > 6 &&
            segment.Start.YMillimeters < 34 &&
            Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) < 64 &&
            Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters) > 36);
    }

    [Fact]
    public void Route_UsesVerticalDoglegAroundObstacleOnAlignedVerticalPath()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000014",
            new DocumentPoint(20, 0),
            TerminalAnchorDirection.Down,
            new DocumentPoint(20, 100),
            TerminalAnchorDirection.Up);
        var obstacle = new RoutingObstacle(
            Guid.NewGuid(),
            RoutingObstacleKind.RingCabinet,
            new DocumentRect(10, 40, 20, 20));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, [obstacle]);

        Assert.Contains(route.Points, point => point.XMillimeters <= 6 || point.XMillimeters >= 34);
        Assert.True(route.Segments.Count >= 4);
        Assert.All(route.Segments, segment => Assert.True(
            segment.IsHorizontal || segment.IsVertical));
    }

    [Fact]
    public void Route_UsesStableHvLRouteForUnobstructedDiagonalEndpoints()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000015",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(80, 50),
            TerminalAnchorDirection.Left);

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        Assert.Contains(route.Points, point => point == new DocumentPoint(72, 0));
        Assert.All(route.Segments, segment => Assert.True(
            segment.IsHorizontal || segment.IsVertical));
    }

    [Fact]
    public void Route_UsesVhLRouteWhenHvCandidateIsBlocked()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000016",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(80, 50),
            TerminalAnchorDirection.Left);
        var obstacle = new RoutingObstacle(
            Guid.NewGuid(),
            RoutingObstacleKind.Pole,
            new DocumentRect(35, -5, 10, 10));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, [obstacle]);

        Assert.True(route.Segments[0].IsHorizontal);
        Assert.True(route.Segments[0].End.XMillimeters >= 8);
        Assert.True(route.Segments[^1].IsHorizontal);
        Assert.True(route.Segments[^1].End.XMillimeters > route.Segments[^1].Start.XMillimeters);
        Assert.DoesNotContain(route.Segments, segment =>
            segment.IsHorizontal &&
            segment.Start.YMillimeters > -5 &&
            segment.Start.YMillimeters < 5 &&
            Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) < 45 &&
            Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters) > 35);
    }

    [Fact]
    public void Route_UsesDeterministicOrthogonalFallbackWhenAllRoutesAreBlocked()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000017",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(80, 50),
            TerminalAnchorDirection.Left);
        RoutingObstacle[] obstacles =
        [
            new RoutingObstacle(
                Guid.NewGuid(),
                RoutingObstacleKind.RingCabinet,
                new DocumentRect(-100, -100, 300, 300))
        ];

        OrthogonalRoute first = new OrthogonalRouter().Route(request, obstacles);
        OrthogonalRoute second = new OrthogonalRouter().Route(request, obstacles);

        Assert.Equal(request.Start.Position, first.Points[0]);
        Assert.Equal(request.End.Position, first.Points[^1]);
        Assert.All(first.Segments, segment => Assert.True(
            segment.IsHorizontal || segment.IsVertical));
        Assert.Equal(first.Points, second.Points);
    }

    [Fact]
    public void Route_AllowsDirectionalExitFromSourceAndTargetObstacles()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000013",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(100, 0),
            TerminalAnchorDirection.Left);
        RoutingObstacle[] obstacles =
        [
            new RoutingObstacle(
                Guid.NewGuid(),
                RoutingObstacleKind.RingCabinet,
                new DocumentRect(-20, -20, 20, 40)),
            new RoutingObstacle(
                Guid.NewGuid(),
                RoutingObstacleKind.PoleAttachment,
                new DocumentRect(100, -10, 20, 20))
        ];

        OrthogonalRoute route = new OrthogonalRouter().Route(request, obstacles);

        Assert.Equal(request.Start.Position, route.Points[0]);
        Assert.Equal(request.End.Position, route.Points[^1]);
        Assert.True(route.Segments[0].End.XMillimeters > route.Segments[0].Start.XMillimeters);
        Assert.True(route.Segments[^1].Start.XMillimeters < route.Segments[^1].End.XMillimeters);
    }

    [Fact]
    public void Planner_IsDeterministicRegardlessOfInputOrder()
    {
        ConnectionRouteRequest first = CreateRequest(
            "00000000-0000-0000-0000-000000000001",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(60, 40),
            TerminalAnchorDirection.Left);
        ConnectionRouteRequest second = CreateRequest(
            "00000000-0000-0000-0000-000000000002",
            new DocumentPoint(0, 40),
            TerminalAnchorDirection.Right,
            new DocumentPoint(60, 0),
            TerminalAnchorDirection.Left);
        var planner = new OrthogonalRoutePlanner();

        IReadOnlyList<OrthogonalRoute> forward = planner.Plan([first, second], []);
        IReadOnlyList<OrthogonalRoute> reverse = planner.Plan([second, first], []);

        Assert.Equal(
            forward.Select(RouteKey),
            reverse.Select(RouteKey));
    }

    [Fact]
    public void Planner_OffsetsParallelConnectionsToReduceLongOverlap()
    {
        ConnectionRouteRequest first = CreateRequest(
            "00000000-0000-0000-0000-000000000001",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Auto,
            new DocumentPoint(100, 0),
            TerminalAnchorDirection.Auto);
        ConnectionRouteRequest second = CreateRequest(
            "00000000-0000-0000-0000-000000000002",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Auto,
            new DocumentPoint(100, 0),
            TerminalAnchorDirection.Auto);

        IReadOnlyList<OrthogonalRoute> routes =
            new OrthogonalRoutePlanner().Plan([second, first], []);

        Assert.Equal(2, routes.Count);
        Assert.NotEqual(PointsKey(routes[0]), PointsKey(routes[1]));
        Assert.Contains(routes[1].Points, point => point.YMillimeters != 0);
    }

    [Fact]
    public void RouteMidpoint_UsesAccumulatedPathLength()
    {
        OrthogonalRoute route = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [
                new DocumentPoint(0, 0),
                new DocumentPoint(100, 0),
                new DocumentPoint(100, 20)
            ]);

        Assert.Equal(new DocumentPoint(60, 0), route.Midpoint);
    }

    [Fact]
    public void Route_RemovesDuplicatePointsAndMergesCollinearSegments()
    {
        OrthogonalRoute route = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [
                new DocumentPoint(0, 0),
                new DocumentPoint(0, 0),
                new DocumentPoint(10, 0),
                new DocumentPoint(20, 0),
                new DocumentPoint(20, 10)
            ]);

        Assert.Equal(
            [new DocumentPoint(0, 0), new DocumentPoint(20, 0), new DocumentPoint(20, 10)],
            route.Points);
        Assert.DoesNotContain(route.Segments, segment => segment.Length == 0);
    }

    [Theory]
    [InlineData(TerminalAnchorDirection.Left, -8, 0)]
    [InlineData(TerminalAnchorDirection.Right, 8, 0)]
    [InlineData(TerminalAnchorDirection.Up, 0, -8)]
    [InlineData(TerminalAnchorDirection.Down, 0, 8)]
    public void Route_UsesEveryExplicitStartDirection(
        TerminalAnchorDirection direction,
        double expectedX,
        double expectedY)
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000012",
            new DocumentPoint(0, 0),
            direction,
            new DocumentPoint(80, 40),
            TerminalAnchorDirection.Auto);

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        DocumentPoint firstEnd = route.Segments[0].End;
        Assert.Equal(expectedY == 0, route.Segments[0].IsHorizontal);
        Assert.True(direction switch
        {
            TerminalAnchorDirection.Left => firstEnd.XMillimeters <= expectedX,
            TerminalAnchorDirection.Right => firstEnd.XMillimeters >= expectedX,
            TerminalAnchorDirection.Up => firstEnd.YMillimeters <= expectedY,
            TerminalAnchorDirection.Down => firstEnd.YMillimeters >= expectedY,
            _ => false
        });
    }

    [Fact]
    public void Route_RespectsMinimumTerminalStubLength()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        ConnectionRouteRequest request = new(
            Guid.Parse("00000000-0000-0000-0000-000000000013"),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(0, 0),
                TerminalAnchorDirection.Down,
                MinimumStubLength: 50),
            new TerminalAnchor(
                endId,
                new DocumentPoint(80, 100),
                TerminalAnchorDirection.Auto));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        Assert.True(route.Segments[0].IsVertical);
        Assert.True(route.Segments[0].End.YMillimeters >= 50);
        Assert.All(route.Segments, segment =>
            Assert.True(segment.IsHorizontal || segment.IsVertical));
    }

    [Fact]
    public void Route_RespectsMinimumStubWhenConstrainedTerminalIsConnectionEnd()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        DocumentPoint terminal = new(80, 100);
        ConnectionRouteRequest request = new(
            Guid.Parse("00000000-0000-0000-0000-000000000014"),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(0, 0),
                TerminalAnchorDirection.Auto),
            new TerminalAnchor(
                endId,
                terminal,
                TerminalAnchorDirection.Down,
                MinimumStubLength: 50));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        OrthogonalRouteSegment last = route.Segments[^1];
        Assert.True(last.IsVertical);
        Assert.Equal(terminal, last.End);
        Assert.True(last.Start.YMillimeters - last.End.YMillimeters >= 50);
    }

    [Fact]
    public void Route_UsesPreferredHorizontalGuideWithoutChangingTerminalStubs()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        ConnectionRouteRequest request = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(0, 0),
                TerminalAnchorDirection.Down,
                MinimumStubLength: 50),
            new TerminalAnchor(
                endId,
                new DocumentPoint(100, 0),
                TerminalAnchorDirection.Down,
                MinimumStubLength: 50),
            PreferredHorizontalY: 80);

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        Assert.Contains(route.Segments, segment =>
            segment.IsHorizontal && segment.Start.YMillimeters == 80);
        Assert.True(route.Segments[0].IsVertical);
        Assert.True(route.Segments[0].Length >= 50);
        Assert.True(route.Segments[^1].IsVertical);
        Assert.True(route.Segments[^1].Length >= 50);
    }

    [Fact]
    public void Route_PrefersNearestObstacleFreeHorizontalChannelToGuide()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        ConnectionRouteRequest request = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(startId, new DocumentPoint(0, 0), TerminalAnchorDirection.Down, 50),
            new TerminalAnchor(endId, new DocumentPoint(100, 0), TerminalAnchorDirection.Down, 50),
            PreferredHorizontalY: 80);
        var obstacle = new RoutingObstacle(
            Guid.NewGuid(),
            RoutingObstacleKind.Pole,
            new DocumentRect(40, 70, 20, 20));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, [obstacle]);

        Assert.DoesNotContain(route.Segments, segment =>
            IntersectsInterior(
                segment,
                obstacle.Expand(DrawingMetrics.Default.Routing.ObstacleClearance).Bounds));
        Assert.True(route.Segments[0].Length >= 50);
        Assert.True(route.Segments[^1].Length >= 50);
    }

    [Theory]
    [InlineData(TerminalAnchorDirection.Left)]
    [InlineData(TerminalAnchorDirection.Right)]
    [InlineData(TerminalAnchorDirection.Up)]
    [InlineData(TerminalAnchorDirection.Down)]
    public void Route_RespectsMinimumStubForEveryExplicitEndDirection(
        TerminalAnchorDirection direction)
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        DocumentPoint terminal = new(80, 100);
        ConnectionRouteRequest request = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(
                startId,
                new DocumentPoint(0, 0),
                TerminalAnchorDirection.Right),
            new TerminalAnchor(
                endId,
                terminal,
                direction,
                MinimumStubLength: 50));

        OrthogonalRoute route = new OrthogonalRouter().Route(request, []);

        OrthogonalRouteSegment last = route.Segments[^1];
        Assert.Equal(terminal, last.End);
        Assert.True(last.Length >= 50);
        Assert.True(direction switch
        {
            TerminalAnchorDirection.Left => last.IsHorizontal &&
                last.Start.XMillimeters <= terminal.XMillimeters - 50,
            TerminalAnchorDirection.Right => last.IsHorizontal &&
                last.Start.XMillimeters >= terminal.XMillimeters + 50,
            TerminalAnchorDirection.Up => last.IsVertical &&
                last.Start.YMillimeters <= terminal.YMillimeters - 50,
            TerminalAnchorDirection.Down => last.IsVertical &&
                last.Start.YMillimeters >= terminal.YMillimeters + 50,
            _ => false
        });
    }

    [Fact]
    public void Route_UsesMultiTurnShortestPathWithoutCrossingDeviceObstacles()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000017",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Down,
            new DocumentPoint(100, 100),
            TerminalAnchorDirection.Down);
        RoutingObstacle[] obstacles =
        [
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000091"),
                RoutingObstacleKind.RingCabinet,
                new DocumentRect(20, 20, 60, 20)),
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000092"),
                RoutingObstacleKind.PoleAttachment,
                new DocumentRect(40, 55, 60, 20))
        ];

        OrthogonalRoute route = new OrthogonalRouter().Route(request, obstacles);

        Assert.All(route.Segments, segment => Assert.True(
            segment.IsHorizontal || segment.IsVertical));
        Assert.DoesNotContain(route.Segments, segment =>
            obstacles.Any(obstacle => IntersectsInterior(
                segment,
                obstacle.Expand(DrawingMetrics.Default.Routing.ObstacleClearance).Bounds)));
    }

    [Fact]
    public void Route_FindsMultiTurnPathWhenTerminalStubsStartInsideEndpointObstacles()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000018",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Down,
            new DocumentPoint(100, 100),
            TerminalAnchorDirection.Down);
        RoutingObstacle[] endpointObstacles =
        [
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000081"),
                RoutingObstacleKind.RingCabinet,
                new DocumentRect(-5, -5, 10, 10)),
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000082"),
                RoutingObstacleKind.PoleAttachment,
                new DocumentRect(95, 95, 10, 10))
        ];
        RoutingObstacle[] routingObstacles =
        [
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000083"),
                RoutingObstacleKind.Pole,
                new DocumentRect(30, -100, 10, 170)),
            new RoutingObstacle(
                Guid.Parse("00000000-0000-0000-0000-000000000084"),
                RoutingObstacleKind.Pole,
                new DocumentRect(60, 40, 10, 160))
        ];

        OrthogonalRoute route = new OrthogonalRouter().Route(
            request,
            endpointObstacles.Concat(routingObstacles));

        Assert.Equal(request.Start.Position, route.Points[0]);
        Assert.Equal(request.End.Position, route.Points[^1]);
        Assert.All(route.Segments, segment => Assert.True(
            segment.IsHorizontal || segment.IsVertical));
        Assert.True(route.Segments[0].IsVertical);
        Assert.True(
            route.Segments[0].End.YMillimeters >
            route.Segments[0].Start.YMillimeters);
        Assert.True(route.Segments[^1].IsVertical);
        Assert.True(
            route.Segments[^1].Start.YMillimeters >
            route.Segments[^1].End.YMillimeters);
        Assert.DoesNotContain(route.Segments, segment =>
            routingObstacles.Any(obstacle => IntersectsInterior(
                segment,
                obstacle.Expand(DrawingMetrics.Default.Routing.ObstacleClearance).Bounds)));
    }

    [Fact]
    public void Route_AllowsMultipleTurnsWhileLeavingEndpointDeviceObstacle()
    {
        ConnectionRouteRequest request = CreateRequest(
            "00000000-0000-0000-0000-000000000019",
            new DocumentPoint(0, 0),
            TerminalAnchorDirection.Right,
            new DocumentPoint(100, 50),
            TerminalAnchorDirection.Left);
        var endpointObstacle = new RoutingObstacle(
            Guid.Parse("00000000-0000-0000-0000-000000000085"),
            RoutingObstacleKind.Pole,
            new DocumentRect(-20, -20, 60, 100));
        var blockingObstacle = new RoutingObstacle(
            Guid.Parse("00000000-0000-0000-0000-000000000086"),
            RoutingObstacleKind.PoleAttachment,
            new DocumentRect(30, -10, 40, 30));

        OrthogonalRoute route = new OrthogonalRouter().Route(
            request,
            [endpointObstacle, blockingObstacle]);

        Assert.Equal(request.Start.Position, route.Points[0]);
        Assert.Equal(request.End.Position, route.Points[^1]);
        Assert.DoesNotContain(route.Segments, segment =>
            IntersectsInterior(
                segment,
                blockingObstacle.Expand(
                    DrawingMetrics.Default.Routing.ObstacleClearance).Bounds));
    }

    private static bool IntersectsInterior(
        OrthogonalRouteSegment segment,
        DocumentRect bounds)
    {
        if (segment.IsHorizontal)
        {
            return segment.Start.YMillimeters > bounds.YMillimeters &&
                   segment.Start.YMillimeters < bounds.YMillimeters + bounds.HeightMillimeters &&
                   Math.Max(Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters) <
                   Math.Min(Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters),
                       bounds.XMillimeters + bounds.WidthMillimeters);
        }

        return segment.Start.XMillimeters > bounds.XMillimeters &&
               segment.Start.XMillimeters < bounds.XMillimeters + bounds.WidthMillimeters &&
               Math.Max(Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters) <
               Math.Min(Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters),
                   bounds.YMillimeters + bounds.HeightMillimeters);
    }

    private static ConnectionRouteRequest CreateRequest(
        string connectionId,
        DocumentPoint start,
        TerminalAnchorDirection startDirection,
        DocumentPoint end,
        TerminalAnchorDirection endDirection)
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        return new ConnectionRouteRequest(
            Guid.Parse(connectionId),
            ConnectionType.Cable,
            startId,
            endId,
            new TerminalAnchor(startId, start, startDirection),
            new TerminalAnchor(endId, end, endDirection));
    }

    private static string RouteKey(OrthogonalRoute route)
    {
        return $"{route.ConnectionId}:{PointsKey(route)}";
    }

    private static string PointsKey(OrthogonalRoute route)
    {
        return string.Join(';', route.Points.Select(point =>
            $"{point.XMillimeters:R},{point.YMillimeters:R}"));
    }
}
