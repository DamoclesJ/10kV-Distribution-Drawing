using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RoutingEnvironmentDifferentialTests
{
    private static readonly Guid Obstacle1 = Id(201);
    private static readonly Guid Obstacle2 = Id(202);
    private static readonly Guid Obstacle3 = Id(203);

    [Fact]
    public void PerPlanEnvironment_MatchesH1ReferenceAcrossConnectionAndExclusionMatrix()
    {
        RoutingObstacle[] obstacles =
        [
            new(Obstacle1, RoutingObstacleKind.RingCabinet, new DocumentRect(35, 28, 24, 32)),
            new(Obstacle2, RoutingObstacleKind.Transformer, new DocumentRect(72, 15, 25, 35)),
            new(Obstacle3, RoutingObstacleKind.CustomerStation, new DocumentRect(112, 35, 30, 28)),
            new(Id(204), RoutingObstacleKind.Pole, new DocumentRect(52, 82, 10, 10)),
            new(Id(205), RoutingObstacleKind.PoleAttachment, new DocumentRect(85, 80, 8, 8)),
            new(Id(206), RoutingObstacleKind.IntermediateTerminal, new DocumentRect(126, 85, 8, 8))
        ];
        ConnectionRouteRequest[] requests =
        [
            Request(1, ConnectionType.Cable, new DocumentPoint(0, 50), new DocumentPoint(155, 50)),
            Request(2, ConnectionType.Cable, new DocumentPoint(0, 45), new DocumentPoint(155, 45)),
            Request(3, ConnectionType.Cable, new DocumentPoint(0, 60), new DocumentPoint(155, 60)) with
            {
                ExcludedObstacleSourceIds = new HashSet<Guid> { Obstacle1 }
            },
            Request(4, ConnectionType.OverheadLine, new DocumentPoint(0, 90), new DocumentPoint(155, 90)) with
            {
                RequiredWaypoints =
                [
                    new RequiredRouteWaypoint(
                        Obstacle1,
                        new DocumentPoint(42, 90),
                        CompositeSourceIds: [Obstacle2],
                        SuccessorMinimumStubLength: 3.4),
                    new RequiredRouteWaypoint(
                        Obstacle3,
                        new DocumentPoint(104, 90),
                        PredecessorMinimumStubLength: 3.4),
                    new RequiredRouteWaypoint(
                        Id(206),
                        new DocumentPoint(130, 90),
                        CompositeSourceIds: [Id(204), Id(205)],
                        PredecessorMinimumStubLength: 3.4)
                ]
            },
            Request(5, ConnectionType.Cable, new DocumentPoint(0, 0), new DocumentPoint(155, 0)) with
            {
                PreferredHorizontalY = -20
            },
            Request(6, ConnectionType.Cable, new DocumentPoint(40, 40), new DocumentPoint(155, 70))
        ];

        RoutingRun reference = Run(
            requests,
            obstacles,
            DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy,
            useEnvironment: false);
        RoutingRun optimized = Run(
            requests,
            obstacles.Reverse().ToArray(),
            DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy,
            useEnvironment: true);

        AssertEquivalent(reference.Routes, optimized.Routes);
    }

    [Fact]
    public void PerPlanEnvironment_PreservesAcceptedContinuityPreference()
    {
        RoutingObstacle[] obstacles =
        [new(Obstacle1, RoutingObstacleKind.RingCabinet, new DocumentRect(40, 35, 20, 30))];
        ConnectionRouteRequest request = Request(
            21,
            ConnectionType.Cable,
            new DocumentPoint(0, 50),
            new DocumentPoint(100, 50.1));
        OrthogonalRoute seed = new OrthogonalRouter().Route(
            request with { End = request.End with { Position = new DocumentPoint(100, 49.9) } },
            obstacles);

        RoutingRun reference = Run(
            [request], obstacles, DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy,
            useEnvironment: false,
            acceptedRoutes: [seed]);
        RoutingRun optimized = Run(
            [request], obstacles, DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy,
            useEnvironment: true,
            acceptedRoutes: [seed]);

        AssertEquivalent(reference.Routes, optimized.Routes);
        Assert.Equal(seed.ContinuityFamily, optimized.Routes.Single().ContinuityFamily);
        Assert.Equal(reference.ContinuityPreferences, optimized.ContinuityPreferences);
    }

    [Fact]
    public void PerPlanEnvironment_ReusesOnlyMatchingEffectiveAndVisibilitySignatures()
    {
        RoutingObstacle[] obstacles =
        [
            new(Obstacle1, RoutingObstacleKind.RingCabinet, new DocumentRect(35, 28, 24, 32)),
            new(Obstacle2, RoutingObstacleKind.Transformer, new DocumentRect(72, 15, 25, 35)),
            new(Obstacle3, RoutingObstacleKind.CustomerStation, new DocumentRect(112, 35, 30, 28))
        ];
        ConnectionRouteRequest[] requests =
        [
            Request(1, ConnectionType.Cable, new DocumentPoint(0, 0), new DocumentPoint(155, 0)),
            Request(2, ConnectionType.Cable, new DocumentPoint(0, 5), new DocumentPoint(155, 5)),
            Request(3, ConnectionType.Cable, new DocumentPoint(0, 10), new DocumentPoint(155, 10)) with
            {
                ExcludedObstacleSourceIds = new HashSet<Guid> { Obstacle1 }
            },
            Request(4, ConnectionType.OverheadLine, new DocumentPoint(0, 15), new DocumentPoint(155, 15)) with
            {
                ExcludedObstacleSourceIds = new HashSet<Guid> { Obstacle2 }
            }
        ];
        var counters = new RoutingEnvironmentCounters();
        var router = new OrthogonalRouter(DrawingMetrics.Default);
        var planner = new OrthogonalRoutePlanner(
            router,
            null,
            DrawingMetrics.Default,
            counters,
            useRoutingEnvironment: true);

        _ = planner.Plan(requests, obstacles);

        Assert.Equal(1, counters.ObstacleSortCount);
        Assert.Equal(obstacles.Length, counters.ObstacleExpansionCount);
        Assert.Equal(3, counters.EffectiveViewBuildCount);
        Assert.Equal(3, counters.CandidateAxisBasisBuildCount);
        Assert.True(counters.VisibilityAxisBasisBuildCount >= 3);
        Assert.True(counters.CacheHitCount >= 2);
        Assert.True(counters.CacheMissCount >= 6);
        Assert.True(counters.VisibilityAxisBasisBuildCount <= requests.Length);
    }

    [Fact]
    public void PerPlanEnvironment_DropsEveryCacheWhenPlanReturns()
    {
        RoutingObstacle[] obstacles =
        [new(Obstacle1, RoutingObstacleKind.RingCabinet, new DocumentRect(35, 28, 24, 32))];
        ConnectionRouteRequest[] requests =
        [
            Request(31, ConnectionType.Cable, new DocumentPoint(0, 0), new DocumentPoint(100, 0)),
            Request(32, ConnectionType.Cable, new DocumentPoint(0, 5), new DocumentPoint(100, 5))
        ];
        var counters = new RoutingEnvironmentCounters();
        var planner = new OrthogonalRoutePlanner(
            new OrthogonalRouter(DrawingMetrics.Default),
            null,
            DrawingMetrics.Default,
            counters,
            useRoutingEnvironment: true);

        _ = planner.Plan(requests, obstacles);
        _ = planner.Plan(requests, obstacles);

        Assert.Equal(2, counters.ObstacleSortCount);
        Assert.Equal(2, counters.ObstacleExpansionCount);
        Assert.Equal(2, counters.EffectiveViewBuildCount);
        Assert.Equal(2, counters.CandidateAxisBasisBuildCount);
        Assert.Equal(2, counters.VisibilityAxisBasisBuildCount);
        Assert.Equal(4, counters.CacheHitCount);
        Assert.Equal(4, counters.CacheMissCount);
    }

    [Fact]
    public void PerPlanEnvironment_DoesNotReuseAcrossMetricsIdentityOrPlans()
    {
        RoutingObstacle[] obstacles =
        [new(Obstacle1, RoutingObstacleKind.RingCabinet, new DocumentRect(35, 28, 24, 32))];
        ConnectionRouteRequest[] requests =
        [
            Request(1, ConnectionType.Cable, new DocumentPoint(0, 0), new DocumentPoint(100, 0)),
            Request(2, ConnectionType.Cable, new DocumentPoint(0, 5), new DocumentPoint(100, 5))
        ];
        DrawingMetrics alternateMetrics = DrawingMetrics.Default with
        {
            Routing = DrawingMetrics.Default.Routing with
            {
                ObstacleClearance = 5,
                ParallelSpacing = 7
            }
        };

        RoutingRun referenceDefault = Run(
            requests, obstacles, DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy, useEnvironment: false);
        RoutingRun optimizedDefault = Run(
            requests, obstacles, DrawingMetrics.Default,
            CandidateFamilyEvaluationMode.Lazy, useEnvironment: true);
        RoutingRun referenceAlternate = Run(
            requests, obstacles, alternateMetrics,
            CandidateFamilyEvaluationMode.Lazy, useEnvironment: false);
        RoutingRun optimizedAlternate = Run(
            requests, obstacles, alternateMetrics,
            CandidateFamilyEvaluationMode.Lazy, useEnvironment: true);

        AssertEquivalent(referenceDefault.Routes, optimizedDefault.Routes);
        AssertEquivalent(referenceAlternate.Routes, optimizedAlternate.Routes);
        var defaultEnvironment = new RoutingEnvironment(obstacles, DrawingMetrics.Default);
        var alternateEnvironment = new RoutingEnvironment(obstacles, alternateMetrics);
        var changedObstacleEnvironment = new RoutingEnvironment(
            [obstacles[0] with { Kind = RoutingObstacleKind.Transformer }],
            DrawingMetrics.Default);
        Assert.NotEqual(
            defaultEnvironment.GetEffectiveView(null, 4, 6).Signature,
            alternateEnvironment.GetEffectiveView(null, 5, 7).Signature);
        Assert.NotEqual(
            defaultEnvironment.GetEffectiveView(null, 4, 6).Signature,
            changedObstacleEnvironment.GetEffectiveView(null, 4, 6).Signature);
        Assert.Equal(2, optimizedDefault.Counters!.ObstacleSortCount +
            optimizedAlternate.Counters!.ObstacleSortCount);
        Assert.Equal(2, optimizedDefault.Counters.ObstacleExpansionCount +
            optimizedAlternate.Counters.ObstacleExpansionCount);
    }

    [Fact]
    public void PerPlanEnvironment_PreservesRequiredStubExceptionType()
    {
        var request = new ConnectionRouteRequest(
            Id(1),
            ConnectionType.OverheadLine,
            Id(101),
            Id(102),
            new TerminalAnchor(Id(101), new DocumentPoint(0, 90.5)),
            new TerminalAnchor(Id(102), new DocumentPoint(40.5, 92.2)),
            RequiredWaypoints:
            [
                new RequiredRouteWaypoint(
                    Id(301),
                    new DocumentPoint(40.5, 90.5),
                    SuccessorMinimumStubLength: 3.4)
            ]);

        RoutingConstraintException reference = Assert.Throws<RoutingConstraintException>(() =>
            Run([request], [], DrawingMetrics.Default,
                CandidateFamilyEvaluationMode.Lazy, useEnvironment: false));
        RoutingConstraintException optimized = Assert.Throws<RoutingConstraintException>(() =>
            Run([request], [], DrawingMetrics.Default,
                CandidateFamilyEvaluationMode.Lazy, useEnvironment: true));

        Assert.Equal(reference.GetType(), optimized.GetType());
    }

    private static RoutingRun Run(
        IReadOnlyList<ConnectionRouteRequest> requests,
        IReadOnlyList<RoutingObstacle> obstacles,
        DrawingMetrics metrics,
        CandidateFamilyEvaluationMode familyMode,
        bool useEnvironment,
        IReadOnlyList<OrthogonalRoute>? acceptedRoutes = null)
    {
        var counters = new RoutingEnvironmentCounters();
        var statistics = new RoutingEvaluationStatistics();
        var continuity = new RouteContinuityContext();
        if (acceptedRoutes is not null)
        {
            continuity.BeginGesture(acceptedRoutes);
            continuity.BeginProvisionalBuild();
        }
        var router = new OrthogonalRouter(metrics, continuity, familyMode, statistics);
        var planner = new OrthogonalRoutePlanner(
            router,
            continuity,
            metrics,
            counters,
            useEnvironment);
        IReadOnlyList<OrthogonalRoute> routes = planner.Plan(requests, obstacles);
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> continuityPreferences =
            routes.ToDictionary(
                route => route.ConnectionId,
                route => continuity.GetProvisional(route.ConnectionId));
        return new RoutingRun(
            routes,
            statistics.FamilyClassificationCount,
            counters,
            continuityPreferences);
    }

    private static void AssertEquivalent(
        IReadOnlyList<OrthogonalRoute> expected,
        IReadOnlyList<OrthogonalRoute> actual)
    {
        Assert.Equal(expected.Select(route => route.ConnectionId),
            actual.Select(route => route.ConnectionId));
        for (int index = 0; index < expected.Count; index++)
        {
            OrthogonalRoute left = expected[index];
            OrthogonalRoute right = actual[index];
            Assert.True(left.Points.SequenceEqual(right.Points));
            Assert.Equal(left.Segments, right.Segments);
            Assert.Equal(left.StartTerminalId, right.StartTerminalId);
            Assert.Equal(left.EndTerminalId, right.EndTerminalId);
            Assert.Equal(left.Length, right.Length);
            Assert.Equal(left.Midpoint, right.Midpoint);
            Assert.Equal(left.ContinuityFamily, right.ContinuityFamily);
            Assert.Equal(left.ContinuityScore, right.ContinuityScore);
            Assert.Equal(left.ContinuityScore?.CoordinateKey, right.ContinuityScore?.CoordinateKey);
            Assert.Equal(RouteHitTestBounds(left), RouteHitTestBounds(right));
        }

        IReadOnlyList<RouteIntersection> expectedCrossings =
            new RouteCrossingDetector().Detect(expected);
        IReadOnlyList<RouteIntersection> actualCrossings =
            new RouteCrossingDetector().Detect(actual);
        Assert.Equal(expectedCrossings, actualCrossings);
        var decorator = new LineJumpDecorator();
        SceneElement[] expectedJumps = expected.SelectMany(route => decorator.Project(
            route,
            expectedCrossings,
            Colors.Black,
            route.ConnectionType == ConnectionType.Cable
                ? SceneStrokeStyle.Dashed : SceneStrokeStyle.Solid)).ToArray();
        SceneElement[] actualJumps = actual.SelectMany(route => decorator.Project(
            route,
            actualCrossings,
            Colors.Black,
            route.ConnectionType == ConnectionType.Cable
                ? SceneStrokeStyle.Dashed : SceneStrokeStyle.Solid)).ToArray();
        Assert.Equal(expectedJumps, actualJumps);
        Assert.Equal(expected.Select(route => route.Bounds), actual.Select(route => route.Bounds));
    }

    private static DocumentRect RouteHitTestBounds(OrthogonalRoute route)
    {
        double padding = route.ConnectionType == ConnectionType.Cable ? 2 : 3;
        return new DocumentRect(
            route.Bounds.XMillimeters - padding,
            route.Bounds.YMillimeters - padding,
            Math.Max(route.Bounds.WidthMillimeters + padding * 2, padding * 2),
            Math.Max(route.Bounds.HeightMillimeters + padding * 2, padding * 2));
    }

    private static ConnectionRouteRequest Request(
        int connection,
        ConnectionType type,
        DocumentPoint start,
        DocumentPoint end) => new(
        Id(connection),
        type,
        Id(1000 + connection * 2),
        Id(1001 + connection * 2),
        new TerminalAnchor(Id(1000 + connection * 2), start, TerminalAnchorDirection.Right),
        new TerminalAnchor(Id(1001 + connection * 2), end, TerminalAnchorDirection.Left));

    private static Guid Id(int value) => Guid.Parse($"82000000-0000-0000-0000-{value:D12}");

    private sealed record RoutingRun(
        IReadOnlyList<OrthogonalRoute> Routes,
        int FamilyClassificationCount,
        RoutingEnvironmentCounters? Counters,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> ContinuityPreferences);
}
