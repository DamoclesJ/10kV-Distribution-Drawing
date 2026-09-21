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

    [Fact]
    public void H3_2B_ProductionMaterializerMatchesLegacyPipeline()
    {
        foreach (MaterializationFixture fixture in MaterializationFixtures())
        {
            var router = new OrthogonalRouter();
            var productionTrace = new List<CandidateMaterializationTrace>();
            OrthogonalRouter.Candidate[] production = router.MaterializeCandidates(
                fixture.Request,
                fixture.Start,
                fixture.StartStub,
                fixture.RawCandidates,
                fixture.EndStub,
                fixture.End,
                fixture.StartDirection,
                fixture.EndOutwardDirection,
                out int productionRawCount,
                productionTrace);
            LegacyMaterializationResult reference = LegacyMaterialize(router, fixture);

            Assert.Equal(reference.RawCandidateCount, productionRawCount);
            Assert.Equal(reference.Trace.Count, productionTrace.Count);
            for (int index = 0; index < reference.Trace.Count; index++)
            {
                CandidateMaterializationTrace expected = reference.Trace[index];
                CandidateMaterializationTrace actual = productionTrace[index];
                Assert.True(expected.RawCandidate.SequenceEqual(actual.RawCandidate), fixture.Name);
                AssertCandidateEqual(fixture.Name, expected.Candidate, actual.Candidate);
                Assert.Equal(expected.TerminalStubValid, actual.TerminalStubValid);
                Assert.Equal(expected.BacktrackingValid, actual.BacktrackingValid);
                Assert.Equal(expected.Outcome, actual.Outcome);
            }

            Assert.Equal(reference.Candidates.Length, production.Length);
            for (int index = 0; index < reference.Candidates.Length; index++)
            {
                AssertCandidateEqual(
                    fixture.Name,
                    reference.Candidates[index],
                    production[index]);
            }
        }
    }

    [Fact]
    public void H3_2B_RejectedCandidatesRetainRawPriorityAndFirstKeyWins()
    {
        MaterializationFixture fixture = MaterializationFixtures()
            .Single(candidate => candidate.Name == "rejected-and-duplicate-sequence");
        var trace = new List<CandidateMaterializationTrace>();
        OrthogonalRouter.Candidate[] candidates = new OrthogonalRouter().MaterializeCandidates(
            fixture.Request,
            fixture.Start,
            fixture.StartStub,
            fixture.RawCandidates,
            fixture.EndStub,
            fixture.End,
            fixture.StartDirection,
            fixture.EndOutwardDirection,
            out int rawCount,
            trace);

        Assert.Equal(fixture.RawCandidates.Count, rawCount);
        Assert.Equal(Enumerable.Range(0, rawCount),
            trace.Select(item => item.Candidate.Priority));
        Assert.Equal(
            [
                CandidateMaterializationOutcome.Accepted,
                CandidateMaterializationOutcome.DuplicateKeyRejected,
                CandidateMaterializationOutcome.StubRejected,
                CandidateMaterializationOutcome.BacktrackingRejected,
                CandidateMaterializationOutcome.Accepted
            ],
            trace.Select(item => item.Outcome));
        Assert.Equal([0, 4], candidates.Select(candidate => candidate.Priority));
        Assert.Equal(trace[0].Candidate.Key, candidates[0].Key);
    }

    [Fact]
    public void H3_2B_ProductionMaterializerMatchesLegacyExceptionType()
    {
        MaterializationFixture fixture = MaterializationFixtures()
            .Single(candidate => candidate.Name == "single-candidate");
        fixture = fixture with
        {
            Request = fixture.Request with { EndTerminalId = fixture.Request.StartTerminalId }
        };
        var router = new OrthogonalRouter();

        Exception reference = Assert.ThrowsAny<Exception>(() => LegacyMaterialize(router, fixture));
        Exception production = Assert.ThrowsAny<Exception>(() => router.MaterializeCandidates(
            fixture.Request,
            fixture.Start,
            fixture.StartStub,
            fixture.RawCandidates,
            fixture.EndStub,
            fixture.End,
            fixture.StartDirection,
            fixture.EndOutwardDirection,
            out _));

        Assert.Equal(reference.GetType(), production.GetType());
    }

    private static IEnumerable<MaterializationFixture> MaterializationFixtures()
    {
        var start = new DocumentPoint(0, 0);
        var startStub = new DocumentPoint(8, 0);
        var endStub = new DocumentPoint(92, 0);
        var end = new DocumentPoint(100, 0);
        ConnectionRouteRequest request = new(
            Connection1,
            ConnectionType.Cable,
            Start1,
            End1,
            new TerminalAnchor(Start1, start, TerminalAnchorDirection.Right),
            new TerminalAnchor(End1, end, TerminalAnchorDirection.Left),
            DisallowBacktracking: true);
        IReadOnlyList<DocumentPoint> direct = [startStub, endStub];
        IReadOnlyList<DocumentPoint> alternate =
        [
            startStub,
            new DocumentPoint(8, 20),
            new DocumentPoint(92, 20),
            endStub
        ];
        IReadOnlyList<DocumentPoint> stubInvalid =
        [
            startStub,
            new DocumentPoint(-5, 0),
            new DocumentPoint(-5, 10),
            new DocumentPoint(92, 10),
            endStub
        ];
        IReadOnlyList<DocumentPoint> backtrackingInvalid =
        [
            startStub,
            new DocumentPoint(20, 0),
            new DocumentPoint(20, 10),
            new DocumentPoint(10, 10),
            new DocumentPoint(10, 0),
            new DocumentPoint(5, 0),
            new DocumentPoint(5, 20),
            new DocumentPoint(92, 20),
            endStub
        ];

        yield return new MaterializationFixture(
            "zero-candidates", request, start, startStub, [], endStub, end,
            TerminalAnchorDirection.Right, TerminalAnchorDirection.Left);
        yield return new MaterializationFixture(
            "single-candidate", request, start, startStub, [direct], endStub, end,
            TerminalAnchorDirection.Right, TerminalAnchorDirection.Left);
        yield return new MaterializationFixture(
            "rejected-and-duplicate-sequence", request, start, startStub,
            [direct, direct, stubInvalid, backtrackingInvalid, alternate], endStub, end,
            TerminalAnchorDirection.Right, TerminalAnchorDirection.Left);
        yield return new MaterializationFixture(
            "guide-obstacle-visibility-and-continuity-candidates", request, start, startStub,
            [
                alternate,
                [startStub, new DocumentPoint(8, 25), new DocumentPoint(92, 25), endStub],
                [startStub, new DocumentPoint(30, 0), new DocumentPoint(30, 35),
                    new DocumentPoint(92, 35), endStub],
                [startStub, new DocumentPoint(8, -20), new DocumentPoint(55, -20),
                    new DocumentPoint(55, 30), new DocumentPoint(92, 30), endStub],
                alternate,
                direct,
                direct
            ], endStub, end, TerminalAnchorDirection.Right, TerminalAnchorDirection.Left);
    }

    private static LegacyMaterializationResult LegacyMaterialize(
        OrthogonalRouter router,
        MaterializationFixture fixture)
    {
        var entries = new List<LegacyMaterializationEntry>();
        LegacyMaterializationEntry[] valid = fixture.RawCandidates
            .Select((core, priority) =>
            {
                var entry = new LegacyMaterializationEntry(
                    core,
                    CreateLegacyCandidate(
                        fixture.Request,
                        fixture.Start,
                        fixture.StartStub,
                        core,
                        fixture.EndStub,
                        fixture.End,
                        priority));
                entries.Add(entry);
                return entry;
            })
            .Where(entry =>
            {
                entry.TerminalStubValid = router.HasTerminalStubs(
                    entry.Candidate.Route,
                    fixture.Start,
                    fixture.StartDirection,
                    fixture.Request.Start.MinimumStubLength,
                    fixture.End,
                    fixture.EndOutwardDirection,
                    fixture.Request.End.MinimumStubLength);
                entry.Outcome = entry.TerminalStubValid
                    ? CandidateMaterializationOutcome.Accepted
                    : CandidateMaterializationOutcome.StubRejected;
                return entry.TerminalStubValid;
            })
            .Where(entry =>
            {
                entry.BacktrackingValid = !fixture.Request.DisallowBacktracking ||
                    !OrthogonalRouter.HasBacktracking(entry.Candidate.Route);
                entry.Outcome = entry.BacktrackingValid.Value
                    ? CandidateMaterializationOutcome.Accepted
                    : CandidateMaterializationOutcome.BacktrackingRejected;
                return entry.BacktrackingValid.Value;
            })
            .ToArray();

        OrthogonalRouter.Candidate[] candidates = valid
            .GroupBy(entry => entry.Candidate.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                LegacyMaterializationEntry[] grouped = group.ToArray();
                for (int index = 1; index < grouped.Length; index++)
                {
                    grouped[index].Outcome = CandidateMaterializationOutcome.DuplicateKeyRejected;
                }
                return grouped[0].Candidate;
            })
            .ToArray();
        CandidateMaterializationTrace[] trace = entries.Select(entry =>
            new CandidateMaterializationTrace(
                entry.RawCandidate,
                entry.Candidate,
                entry.TerminalStubValid,
                entry.BacktrackingValid,
                entry.Outcome)).ToArray();
        return new LegacyMaterializationResult(fixture.RawCandidates.Count, candidates, trace);
    }

    private static OrthogonalRouter.Candidate CreateLegacyCandidate(
        ConnectionRouteRequest request,
        DocumentPoint start,
        DocumentPoint startStub,
        IReadOnlyList<DocumentPoint> core,
        DocumentPoint endStub,
        DocumentPoint end,
        int priority)
    {
        var points = new List<DocumentPoint> { start, startStub };
        points.AddRange(core.Skip(1).SkipLast(1));
        points.Add(endStub);
        points.Add(end);
        var route = new OrthogonalRoute(
            request.ConnectionId,
            request.ConnectionType,
            request.StartTerminalId,
            request.EndTerminalId,
            points);
        string key = string.Join(
            ";",
            route.Points.Select(point => $"{point.XMillimeters:R},{point.YMillimeters:R}"));
        return new OrthogonalRouter.Candidate(route, priority, key, default);
    }

    private static void AssertCandidateEqual(
        string name,
        OrthogonalRouter.Candidate expected,
        OrthogonalRouter.Candidate actual)
    {
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.Key, actual.Key);
        Assert.True(expected.Route.Points.SequenceEqual(actual.Route.Points), name);
        Assert.Equal(expected.Route.Segments, actual.Route.Segments);
        Assert.Equal(expected.Route.StartTerminalId, actual.Route.StartTerminalId);
        Assert.Equal(expected.Route.EndTerminalId, actual.Route.EndTerminalId);
        Assert.Equal(expected.Route.Bounds, actual.Route.Bounds);
        Assert.Equal(expected.Route.Length, actual.Route.Length);
        Assert.Equal(expected.Route.Midpoint, actual.Route.Midpoint);
        Assert.Equal(expected.Score, actual.Score);
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

    private sealed record MaterializationFixture(
        string Name,
        ConnectionRouteRequest Request,
        DocumentPoint Start,
        DocumentPoint StartStub,
        IReadOnlyList<IReadOnlyList<DocumentPoint>> RawCandidates,
        DocumentPoint EndStub,
        DocumentPoint End,
        TerminalAnchorDirection StartDirection,
        TerminalAnchorDirection EndOutwardDirection);

    private sealed class LegacyMaterializationEntry(
        IReadOnlyList<DocumentPoint> rawCandidate,
        OrthogonalRouter.Candidate candidate)
    {
        public IReadOnlyList<DocumentPoint> RawCandidate { get; } = rawCandidate;

        public OrthogonalRouter.Candidate Candidate { get; } = candidate;

        public bool TerminalStubValid { get; set; }

        public bool? BacktrackingValid { get; set; }

        public CandidateMaterializationOutcome Outcome { get; set; }
    }

    private sealed record LegacyMaterializationResult(
        int RawCandidateCount,
        OrthogonalRouter.Candidate[] Candidates,
        IReadOnlyList<CandidateMaterializationTrace> Trace);

    private sealed record RoutingRun(
        IReadOnlyList<OrthogonalRoute> Routes,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> ProvisionalPreferences,
        IReadOnlyDictionary<Guid, RouteContinuityPreference?> AcceptedPreferences,
        int FamilyClassificationCount);
}
