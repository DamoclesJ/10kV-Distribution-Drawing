using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class OrthogonalRoutingHotPathDifferentialTests
{
    private static readonly Guid Connection1 = Id(1);
    private static readonly Guid Connection2 = Id(2);
    private static readonly Guid Connection3 = Id(3);
    private static readonly Guid Start1 = Id(101);
    private static readonly Guid End1 = Id(102);
    private static readonly Guid Start2 = Id(103);
    private static readonly Guid End2 = Id(104);
    private static readonly RoutingObstacle CenterObstacle = new(
        Id(201), RoutingObstacleKind.RingCabinet, new DocumentRect(40, 35, 20, 30));

    [Fact]
    public void H1_MatchesEagerReferenceAcrossCandidateAndConstraintMatrix()
    {
        foreach (Scenario scenario in Scenarios())
        {
            RoutingRun reference = Run(
                scenario.Requests,
                scenario.Obstacles,
                CandidateFamilyEvaluationMode.EagerReference);
            RoutingRun h1 = Run(
                scenario.Requests,
                scenario.Obstacles,
                CandidateFamilyEvaluationMode.Lazy);

            AssertEquivalent(scenario.Name, reference, h1);

            RoutingRun reorderedReference = Run(
                scenario.Requests.Reverse().ToArray(),
                scenario.Obstacles.Reverse().ToArray(),
                CandidateFamilyEvaluationMode.EagerReference);
            RoutingRun reorderedH1 = Run(
                scenario.Requests.Reverse().ToArray(),
                scenario.Obstacles.Reverse().ToArray(),
                CandidateFamilyEvaluationMode.Lazy);
            AssertEquivalent($"{scenario.Name} reordered", reorderedReference, reorderedH1);
            AssertRoutesEqual(reference.Routes, reorderedH1.Routes, scenario.Name);
        }
    }

    [Fact]
    public void H1_MatchesEagerReferenceForContinuityAndHysteresisOutcomes()
    {
        ConnectionRouteRequest initialRequest = Request(Connection1, Start1, End1, 49.9);
        OrthogonalRoute initial = new OrthogonalRouter().Route(initialRequest, [CenterObstacle]);
        var upperBlocker = new RoutingObstacle(
            Id(202), RoutingObstacleKind.Pole, new DocumentRect(0, 20, 100, 20));
        (string Name, ConnectionRouteRequest Request, RoutingObstacle[] Obstacles)[] candidates =
        [
            ("ranked-first-or-near-family", Request(Connection1, Start1, End1, 50.1),
                [CenterObstacle]),
            ("current-family-ranked-first", initialRequest, [CenterObstacle]),
            ("switching-margin", Request(Connection1, Start1, End1, 54), [CenterObstacle]),
            ("clearly-superior-switch", Request(Connection1, Start1, End1, 55), [CenterObstacle]),
            ("guide-priority", Request(Connection1, Start1, End1, 50.1) with
                { PreferredHorizontalY = 69 }, [CenterObstacle]),
            ("current-family-unavailable", Request(Connection1, Start1, End1, 50.1),
                [CenterObstacle, upperBlocker])
        ];

        foreach (var candidate in candidates)
        {
            RoutingRun reference = Run(
                [candidate.Request],
                candidate.Obstacles,
                CandidateFamilyEvaluationMode.EagerReference,
                [initial]);
            RoutingRun h1 = Run(
                [candidate.Request],
                candidate.Obstacles,
                CandidateFamilyEvaluationMode.Lazy,
                [initial]);

            AssertEquivalent(candidate.Name, reference, h1);
        }
    }

    [Fact]
    public void H1_ClassifiesOnlyWinnerWhenContinuityIsInactive()
    {
        ConnectionRouteRequest request = Request(Connection1, Start1, End1, 50.1);
        RoutingRun reference = Run(
            [request], [CenterObstacle], CandidateFamilyEvaluationMode.EagerReference);
        RoutingRun h1 = Run(
            [request], [CenterObstacle], CandidateFamilyEvaluationMode.Lazy);

        AssertEquivalent("inactive-continuity-classification-count", reference, h1);
        Assert.Equal(1, h1.FamilyClassificationCount);
        Assert.True(reference.FamilyClassificationCount > h1.FamilyClassificationCount);
    }

    [Fact]
    public void H1_ReducesFamilyClassificationsForStableTypicalCandidate()
    {
        RoutingObstacle[] obstacles = [CenterObstacle];
        ConnectionRouteRequest initialRequest = Request(Connection1, Start1, End1, 49.9);
        OrthogonalRoute initial = new OrthogonalRouter().Route(initialRequest, obstacles);
        ConnectionRouteRequest candidate = Request(Connection1, Start1, End1, 50.1);

        RoutingRun reference = Run(
            [candidate], obstacles, CandidateFamilyEvaluationMode.EagerReference, [initial]);
        RoutingRun h1 = Run(
            [candidate], obstacles, CandidateFamilyEvaluationMode.Lazy, [initial]);

        AssertEquivalent("classification-count", reference, h1);
        Assert.True(reference.FamilyClassificationCount > h1.FamilyClassificationCount,
            $"Expected H1 to classify fewer families, reference={reference.FamilyClassificationCount}, " +
            $"H1={h1.FamilyClassificationCount}.");
        Assert.True(h1.FamilyClassificationCount > 1,
            "The retained family should occur after the formal rank-zero candidate.");
    }

    [Fact]
    public void H1_MatchesEagerReferenceExceptionTypeForImpossibleRequiredStub()
    {
        var request = new ConnectionRouteRequest(
            Connection1,
            ConnectionType.OverheadLine,
            Start1,
            End1,
            new TerminalAnchor(Start1, new DocumentPoint(0, 90.5)),
            new TerminalAnchor(End1, new DocumentPoint(40.5, 92.2)),
            RequiredWaypoints:
            [
                new RequiredRouteWaypoint(
                    Id(301),
                    new DocumentPoint(40.5, 90.5),
                    SuccessorMinimumStubLength: 3.4)
            ]);

        Exception reference = Assert.ThrowsAny<Exception>(() => Run(
            [request], [], CandidateFamilyEvaluationMode.EagerReference));
        Exception h1 = Assert.ThrowsAny<Exception>(() => Run(
            [request], [], CandidateFamilyEvaluationMode.Lazy));

        Assert.Equal(typeof(RoutingConstraintException), reference.GetType());
        Assert.Equal(reference.GetType(), h1.GetType());
    }

    private static IEnumerable<Scenario> Scenarios()
    {
        yield return new Scenario(
            "no-obstacle-direct-and-l",
            [
                Request(Connection1, Start1, End1, 0, endX: 100, startY: 0),
                Request(Connection2, Start2, End2, 45, endX: 90, startY: 10)
            ],
            []);

        yield return new Scenario(
            "single-obstacle-visibility",
            [Request(Connection1, Start1, End1, 50.1)],
            [CenterObstacle]);

        yield return new Scenario(
            "multiple-obstacles-guide-and-owner-exclusions",
            [
                Request(Connection1, Start1, End1, 50.1) with
                {
                    PreferredHorizontalY = 72,
                    ExcludedObstacleSourceIds = new HashSet<Guid> { Id(205), Id(206) }
                }
            ],
            [
                CenterObstacle,
                new RoutingObstacle(Id(205), RoutingObstacleKind.Transformer,
                    new DocumentRect(-5, 35, 20, 30)),
                new RoutingObstacle(Id(206), RoutingObstacleKind.CustomerStation,
                    new DocumentRect(85, 35, 20, 30))
            ]);

        yield return new Scenario(
            "overlap-and-crossing-plan",
            [
                Request(Connection3, Id(105), Id(106), 0, 100, 40),
                Request(Connection1, Start1, End1, 40, 100, 0),
                Request(Connection2, Start2, End2, 0, 100, 0)
            ],
            []);

        yield return new Scenario(
            "required-waypoint-composite-gap-and-multiple-legs",
            [
                new ConnectionRouteRequest(
                    Connection1,
                    ConnectionType.OverheadLine,
                    Start1,
                    End1,
                    new TerminalAnchor(Start1, new DocumentPoint(0, 20),
                        TerminalAnchorDirection.Right, MinimumStubLength: 8),
                    new TerminalAnchor(End1, new DocumentPoint(120, 65),
                        TerminalAnchorDirection.Left, MinimumStubLength: 8),
                    RequiredWaypoints:
                    [
                        new RequiredRouteWaypoint(
                            Id(301),
                            new DocumentPoint(40, 20),
                            CompositeSourceIds: [Id(302)],
                            SuccessorMinimumStubLength: 6),
                        new RequiredRouteWaypoint(
                            Id(303),
                            new DocumentPoint(80, 65),
                            CompositeSourceIds: [Id(304)],
                            PredecessorMinimumStubLength: 6)
                    ])
            ],
            [
                new RoutingObstacle(Id(301), RoutingObstacleKind.Pole,
                    new DocumentRect(36, 16, 8, 8)),
                new RoutingObstacle(Id(302), RoutingObstacleKind.PoleAttachment,
                    new DocumentRect(38, 18, 4, 4)),
                new RoutingObstacle(Id(303), RoutingObstacleKind.Pole,
                    new DocumentRect(76, 61, 8, 8)),
                new RoutingObstacle(Id(304), RoutingObstacleKind.PoleAttachment,
                    new DocumentRect(78, 63, 4, 4)),
                CenterObstacle
            ]);

        yield return new Scenario(
            "fallback-route",
            [Request(Connection1, Start1, End1, 50, 100, 50)],
            [new RoutingObstacle(Id(207), RoutingObstacleKind.RingCabinet,
                new DocumentRect(-100, -100, 300, 300))]);
    }

    private static RoutingRun Run(
        IReadOnlyList<ConnectionRouteRequest> requests,
        IReadOnlyList<RoutingObstacle> obstacles,
        CandidateFamilyEvaluationMode mode,
        IReadOnlyList<OrthogonalRoute>? acceptedRoutes = null)
    {
        var continuity = new RouteContinuityContext();
        if (acceptedRoutes is not null)
        {
            continuity.BeginGesture(acceptedRoutes);
            continuity.BeginProvisionalBuild();
        }

        var statistics = new RoutingEvaluationStatistics();
        var router = new OrthogonalRouter(DrawingMetrics.Default, continuity, mode, statistics);
        var planner = new OrthogonalRoutePlanner(router, continuity, DrawingMetrics.Default);
        IReadOnlyList<OrthogonalRoute> routes = planner.Plan(requests, obstacles);
        var provisional = routes.ToDictionary(
            route => route.ConnectionId,
            route => continuity.GetProvisional(route.ConnectionId));
        if (acceptedRoutes is not null)
        {
            continuity.AcceptProvisional();
        }

        var accepted = routes.ToDictionary(
            route => route.ConnectionId,
            route => continuity.GetAccepted(route.ConnectionId));
        return new RoutingRun(
            routes,
            provisional,
            accepted,
            statistics.FamilyClassificationCount);
    }

    private static void AssertEquivalent(string name, RoutingRun expected, RoutingRun actual)
    {
        AssertRoutesEqual(expected.Routes, actual.Routes, name);
        AssertPreferencesEqual(expected.ProvisionalPreferences, actual.ProvisionalPreferences);
        AssertPreferencesEqual(expected.AcceptedPreferences, actual.AcceptedPreferences);

        IReadOnlyList<RouteIntersection> expectedCrossings =
            new RouteCrossingDetector().Detect(expected.Routes);
        IReadOnlyList<RouteIntersection> actualCrossings =
            new RouteCrossingDetector().Detect(actual.Routes);
        Assert.Equal(expectedCrossings, actualCrossings);

        var decorator = new LineJumpDecorator();
        SceneElement[] expectedProjection = expected.Routes.SelectMany(route => decorator.Project(
            route,
            expectedCrossings,
            Colors.Black,
            route.ConnectionType == ConnectionType.Cable
                ? SceneStrokeStyle.Dashed
                : SceneStrokeStyle.Solid)).ToArray();
        SceneElement[] actualProjection = actual.Routes.SelectMany(route => decorator.Project(
            route,
            actualCrossings,
            Colors.Black,
            route.ConnectionType == ConnectionType.Cable
                ? SceneStrokeStyle.Dashed
                : SceneStrokeStyle.Solid)).ToArray();
        Assert.Equal(expectedProjection, actualProjection);
        Assert.Equal(
            expected.Routes.Select(RouteHitTestBounds),
            actual.Routes.Select(RouteHitTestBounds));
    }

    private static void AssertPreferencesEqual(
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> expected,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(id => id), actual.Keys.OrderBy(id => id));
        foreach (Guid id in expected.Keys)
        {
            Assert.Equal(expected[id], actual[id]);
        }
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

    private static void AssertRoutesEqual(
        IReadOnlyList<OrthogonalRoute> expected,
        IReadOnlyList<OrthogonalRoute> actual,
        string name)
    {
        Assert.Equal(expected.Select(route => route.ConnectionId),
            actual.Select(route => route.ConnectionId));
        for (int index = 0; index < expected.Count; index++)
        {
            OrthogonalRoute left = expected[index];
            OrthogonalRoute right = actual[index];
            Assert.True(left.Points.SequenceEqual(right.Points), name);
            Assert.Equal(left.Segments, right.Segments);
            Assert.Equal(left.StartTerminalId, right.StartTerminalId);
            Assert.Equal(left.EndTerminalId, right.EndTerminalId);
            Assert.Equal(left.Length, right.Length);
            Assert.Equal(left.Midpoint, right.Midpoint);
            Assert.Equal(left.ContinuityFamily, right.ContinuityFamily);
            Assert.Equal(left.ContinuityScore, right.ContinuityScore);
            Assert.Equal(
                left.ContinuityScore?.CoordinateKey,
                right.ContinuityScore?.CoordinateKey);
        }
    }

    private static ConnectionRouteRequest Request(
        Guid connectionId,
        Guid startId,
        Guid endId,
        double endY,
        double endX = 100,
        double startY = 50) => new(
        connectionId,
        ConnectionType.Cable,
        startId,
        endId,
        new TerminalAnchor(startId, new DocumentPoint(0, startY), TerminalAnchorDirection.Right),
        new TerminalAnchor(endId, new DocumentPoint(endX, endY), TerminalAnchorDirection.Left));

    private static Guid Id(int value) => Guid.Parse($"81000000-0000-0000-0000-{value:D12}");

    private sealed record Scenario(
        string Name,
        ConnectionRouteRequest[] Requests,
        RoutingObstacle[] Obstacles);

    private sealed record RoutingRun(
        IReadOnlyList<OrthogonalRoute> Routes,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> ProvisionalPreferences,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> AcceptedPreferences,
        int FamilyClassificationCount);
}
