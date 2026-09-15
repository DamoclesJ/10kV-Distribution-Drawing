using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RouteContinuitySliceCTests
{
    private static readonly Guid ConnectionId = Guid.Parse(
        "10000000-0000-0000-0000-000000000001");
    private static readonly Guid StartTerminalId = Guid.Parse(
        "10000000-0000-0000-0000-000000000002");
    private static readonly Guid EndTerminalId = Guid.Parse(
        "10000000-0000-0000-0000-000000000003");
    private static readonly RoutingObstacle SymmetricObstacle = new(
        Guid.Parse("10000000-0000-0000-0000-000000000010"),
        RoutingObstacleKind.RingCabinet,
        new DocumentRect(40, 35, 20, 30));

    [Fact]
    public void SymmetryMovement_PreservesFamilyUntilLengthAdvantageExceedsMargin()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        OrthogonalRoute initial = router.Route(Request(49.9), [SymmetricObstacle]);
        AssertUpper(initial);
        continuity.BeginGesture([initial]);

        OrthogonalRoute near = BuildCandidate(router, continuity, 50.1);
        AssertUpper(near);
        continuity.AcceptProvisional();

        OrthogonalRoute atMargin = BuildCandidate(router, continuity, 54);
        AssertUpper(atMargin);
        continuity.AcceptProvisional();

        OrthogonalRoute beyondMargin = BuildCandidate(router, continuity, 55);
        AssertLower(beyondMargin);
        continuity.AcceptProvisional();

        OrthogonalRoute jitterBack = BuildCandidate(router, continuity, 49.9);
        AssertLower(jitterBack);
        continuity.AcceptProvisional();

        OrthogonalRoute decisiveReturn = BuildCandidate(router, continuity, 45);
        AssertUpper(decisiveReturn);
    }

    [Fact]
    public void MultiObstacle_ChannelSourceChangeProducesDifferentFamily()
    {
        RoutingObstacle[] obstacles =
        [
            new(Guid.Parse("20000000-0000-0000-0000-000000000011"),
                RoutingObstacleKind.Pole, new DocumentRect(30, 35, 15, 30)),
            new(Guid.Parse("20000000-0000-0000-0000-000000000012"),
                RoutingObstacleKind.Pole, new DocumentRect(55, 35, 15, 30))
        ];
        var router = new OrthogonalRouter();

        OrthogonalRoute boundaryChannel = router.Route(Request(49, 105), obstacles);
        OrthogonalRoute doglegChannel = router.Route(Request(49.25, 105), obstacles);

        Assert.NotEqual(boundaryChannel.ContinuityFamily, doglegChannel.ContinuityFamily);
        Assert.Contains(boundaryChannel.Points, point => point.YMillimeters == 31);
        Assert.Contains(doglegChannel.Points, point => point.YMillimeters == 25);
    }

    [Fact]
    public void CurrentFamilyUnavailable_ImmediatelySelectsLegalAlternative()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        OrthogonalRoute initial = router.Route(Request(49.9), [SymmetricObstacle]);
        AssertUpper(initial);
        continuity.BeginGesture([initial]);
        var upperBlocker = new RoutingObstacle(
            Guid.Parse("30000000-0000-0000-0000-000000000011"),
            RoutingObstacleKind.Pole,
            new DocumentRect(0, 20, 100, 20));

        continuity.BeginProvisionalBuild();
        OrthogonalRoute switched = router.Route(
            Request(50.1),
            [SymmetricObstacle, upperBlocker]);

        AssertLower(switched);
        Assert.DoesNotContain(switched.Segments, segment =>
            IntersectsInterior(segment, upperBlocker.Expand(
                DrawingMetrics.Default.Routing.ObstacleClearance).Bounds));
    }

    [Fact]
    public void ExplicitHorizontalGuide_IsNeverOverriddenByContinuity()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        OrthogonalRoute initial = router.Route(Request(49.9), [SymmetricObstacle]);
        continuity.BeginGesture([initial]);
        continuity.BeginProvisionalBuild();

        OrthogonalRoute guided = router.Route(
            Request(50.1) with { PreferredHorizontalY = 69 },
            [SymmetricObstacle]);

        AssertLower(guided);
        Assert.Contains(guided.Segments, segment =>
            segment.IsHorizontal && segment.Start.YMillimeters == 69);
    }

    [Fact]
    public void GuidedRouting_UsesContinuityOnlyWhenGuideDeviationIsEqual()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        OrthogonalRoute initial = router.Route(Request(49.9), [SymmetricObstacle]);
        AssertUpper(initial);
        continuity.BeginGesture([initial]);

        var tiedGuideRequest = Request(50.1) with { PreferredHorizontalY = 50 };
        OrthogonalRoute formalBest = new OrthogonalRouter().Route(
            tiedGuideRequest,
            [SymmetricObstacle]);
        AssertLower(formalBest);

        continuity.BeginProvisionalBuild();
        OrthogonalRoute stabilized = router.Route(
            tiedGuideRequest,
            [SymmetricObstacle]);
        AssertUpper(stabilized);
        Assert.Equal(
            formalBest.ContinuityScore!.Value.HorizontalGuideDeviation,
            stabilized.ContinuityScore!.Value.HorizontalGuideDeviation);
        continuity.AcceptProvisional();

        continuity.BeginProvisionalBuild();
        OrthogonalRoute guidePreferred = router.Route(
            Request(50.1) with { PreferredHorizontalY = 69 },
            [SymmetricObstacle]);
        AssertLower(guidePreferred);
        Assert.Equal(0, guidePreferred.ContinuityScore!.Value.HorizontalGuideDeviation);
    }

    [Fact]
    public void TransformerProfessionalBody_SmallSymmetryMovementPreservesFamily()
    {
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(50, 50),
            "T1");
        RoutingObstacle obstacle = Assert.Single(new RoutingObstacleBuilder().Build(
            [transformer.Transformer],
            [],
            new DrawingLayout(),
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [transformer.Transformer.Id] = transformer.Layout
            }));

        AssertProfessionalObstacleContinuity(obstacle);
    }

    [Fact]
    public void CustomerStationProfessionalBody_SmallSymmetryMovementPreservesFamily()
    {
        CustomerStationCreation station = new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供"],
            new DocumentPoint(50, 50));
        RoutingObstacle obstacle = Assert.Single(new RoutingObstacleBuilder().Build(
            [station.CustomerStation],
            [],
            new DrawingLayout(),
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [station.CustomerStation.Id] = station.Layout
            }));

        AssertProfessionalObstacleContinuity(obstacle);
    }

    [Fact]
    public void Context_PublishesAllConnectionsTogetherAndCleansEveryLifecycleEnd()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        continuity.BeginGesture([]);
        continuity.BeginProvisionalBuild();

        _ = router.Route(Request(49.9), [SymmetricObstacle]);
        _ = router.Route(Request(50.1) with
        {
            ConnectionId = Guid.Parse("40000000-0000-0000-0000-000000000001")
        }, [SymmetricObstacle]);

        Assert.Equal(0, continuity.AcceptedEntryCount);
        Assert.Equal(2, continuity.ProvisionalEntryCount);
        continuity.DiscardProvisional();
        Assert.Equal(0, continuity.AcceptedEntryCount);
        Assert.Equal(0, continuity.ProvisionalEntryCount);

        continuity.BeginProvisionalBuild();
        _ = router.Route(Request(49.9), [SymmetricObstacle]);
        _ = router.Route(Request(50.1) with
        {
            ConnectionId = Guid.Parse("40000000-0000-0000-0000-000000000001")
        }, [SymmetricObstacle]);
        continuity.AcceptProvisional();
        Assert.Equal(2, continuity.AcceptedEntryCount);
        RouteContinuityPreference first = Assert.IsType<RouteContinuityPreference>(
            continuity.GetAccepted(ConnectionId));
        RouteContinuityPreference second = Assert.IsType<RouteContinuityPreference>(
            continuity.GetAccepted(
                Guid.Parse("40000000-0000-0000-0000-000000000001")));
        Assert.NotEqual(first.Family, second.Family);

        continuity.BeginProvisionalBuild();
        _ = router.Route(Request(55), [SymmetricObstacle]);
        continuity.DiscardProvisional();
        Assert.Equal(first, continuity.GetAccepted(ConnectionId));

        continuity.EndGesture();
        Assert.False(continuity.IsActive);
        Assert.Equal(0, continuity.AcceptedEntryCount);
        Assert.Equal(0, continuity.ProvisionalEntryCount);
    }

    [Fact]
    public void EndGesture_DeterministicRebuildUsesFormalGeometryOnly()
    {
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        OrthogonalRoute initial = router.Route(Request(49.9), [SymmetricObstacle]);
        continuity.BeginGesture([initial]);
        OrthogonalRoute stabilized = BuildCandidate(router, continuity, 50.1);
        AssertUpper(stabilized);
        continuity.AcceptProvisional();

        continuity.EndGesture();
        OrthogonalRoute rebuilt = router.Route(Request(50.1), [SymmetricObstacle]);
        OrthogonalRoute independentlyRebuilt = new OrthogonalRouter().Route(
            Request(50.1),
            [SymmetricObstacle]);

        AssertLower(rebuilt);
        Assert.Equal(independentlyRebuilt.Points, rebuilt.Points);
        Assert.False(continuity.IsActive);
        Assert.Equal(0, continuity.AcceptedEntryCount);
    }

    [Fact]
    public void RequiredStubConstraint_RemainsHardFailureDuringContinuityGesture()
    {
        Guid waypointId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var request = new ConnectionRouteRequest(
            ConnectionId,
            ConnectionType.OverheadLine,
            StartTerminalId,
            EndTerminalId,
            new TerminalAnchor(StartTerminalId, new DocumentPoint(0, 90.5)),
            new TerminalAnchor(EndTerminalId, new DocumentPoint(40.5, 92.2)),
            RequiredWaypoints:
            [
                new RequiredRouteWaypoint(
                    waypointId,
                    new DocumentPoint(40.5, 90.5),
                    SuccessorMinimumStubLength: 3.4)
            ]);
        var continuity = new RouteContinuityContext();
        continuity.BeginGesture([]);
        continuity.BeginProvisionalBuild();
        var planner = new OrthogonalRoutePlanner(
            new OrthogonalRouter(DrawingMetrics.Default, continuity),
            continuity,
            DrawingMetrics.Default);

        Assert.Throws<RoutingConstraintException>(() => planner.Plan([request], []));
        Assert.Equal(0, continuity.AcceptedEntryCount);
    }

    private static OrthogonalRoute BuildCandidate(
        OrthogonalRouter router,
        RouteContinuityContext continuity,
        double endY)
    {
        continuity.BeginProvisionalBuild();
        return router.Route(Request(endY), [SymmetricObstacle]);
    }

    private static void AssertProfessionalObstacleContinuity(RoutingObstacle obstacle)
    {
        double centerY = obstacle.Bounds.YMillimeters +
            obstacle.Bounds.HeightMillimeters / 2;
        double startX = obstacle.Bounds.XMillimeters - 40;
        double endX = obstacle.Bounds.XMillimeters +
            obstacle.Bounds.WidthMillimeters + 40;
        var continuity = new RouteContinuityContext();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity);
        ConnectionRouteRequest initialRequest = Request(
            centerY - 0.1,
            endX) with
        {
            Start = new TerminalAnchor(
                StartTerminalId,
                new DocumentPoint(startX, centerY),
                TerminalAnchorDirection.Right)
        };
        OrthogonalRoute initial = router.Route(initialRequest, [obstacle]);
        continuity.BeginGesture([initial]);
        continuity.BeginProvisionalBuild();

        OrthogonalRoute near = router.Route(initialRequest with
        {
            End = initialRequest.End with
            {
                Position = new DocumentPoint(endX, centerY + 0.1)
            }
        }, [obstacle]);

        Assert.Equal(initial.ContinuityFamily, near.ContinuityFamily);
        Assert.DoesNotContain(near.Segments, segment =>
            IntersectsInterior(segment, obstacle.Expand(
                DrawingMetrics.Default.Routing.ObstacleClearance).Bounds));
    }

    private static ConnectionRouteRequest Request(double endY, double endX = 100) => new(
        ConnectionId,
        ConnectionType.Cable,
        StartTerminalId,
        EndTerminalId,
        new TerminalAnchor(
            StartTerminalId,
            new DocumentPoint(0, 50),
            TerminalAnchorDirection.Right),
        new TerminalAnchor(
            EndTerminalId,
            new DocumentPoint(endX, endY),
            TerminalAnchorDirection.Left));

    private static void AssertUpper(OrthogonalRoute route) =>
        Assert.Contains(route.Points, point => point.YMillimeters == 31);

    private static void AssertLower(OrthogonalRoute route) =>
        Assert.Contains(route.Points, point => point.YMillimeters == 69);

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
