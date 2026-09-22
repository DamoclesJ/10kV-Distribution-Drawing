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
        LegacyMaterializationResult reference = LegacyMaterialize(
            new OrthogonalRouter(), fixture);
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
        Assert.Equal(reference.Trace.Select(item => item.Outcome),
            trace.Select(item => item.Outcome));
        Assert.Equal([0, 4], candidates.Select(candidate => candidate.Priority));
        Assert.Equal(trace[0].Candidate.Key, candidates[0].Key);
    }

    [Fact]
    public void H3_2B_ProductionAndLegacyCoverEveryMaterializationOutcome()
    {
        var referenceOutcomes = new HashSet<CandidateMaterializationOutcome>();
        var productionOutcomes = new HashSet<CandidateMaterializationOutcome>();

        foreach (MaterializationFixture fixture in MaterializationFixtures())
        {
            var router = new OrthogonalRouter();
            LegacyMaterializationResult reference = LegacyMaterialize(router, fixture);
            var productionTrace = new List<CandidateMaterializationTrace>();
            router.MaterializeCandidates(
                fixture.Request,
                fixture.Start,
                fixture.StartStub,
                fixture.RawCandidates,
                fixture.EndStub,
                fixture.End,
                fixture.StartDirection,
                fixture.EndOutwardDirection,
                out _,
                productionTrace);

            Assert.Equal(reference.Trace.Select(item => item.Outcome),
                productionTrace.Select(item => item.Outcome));
            if (fixture.Name == "stub-invalid-short-start")
            {
                CandidateMaterializationTrace referenceStub = Assert.Single(reference.Trace);
                CandidateMaterializationTrace productionStub = Assert.Single(productionTrace);
                Assert.Equal(CandidateMaterializationOutcome.StubRejected,
                    referenceStub.Outcome);
                Assert.Equal(CandidateMaterializationOutcome.StubRejected,
                    productionStub.Outcome);
                Assert.False(referenceStub.TerminalStubValid);
                Assert.False(productionStub.TerminalStubValid);
                Assert.Null(referenceStub.BacktrackingValid);
                Assert.Null(productionStub.BacktrackingValid);
            }
            referenceOutcomes.UnionWith(reference.Trace.Select(item => item.Outcome));
            productionOutcomes.UnionWith(productionTrace.Select(item => item.Outcome));
        }

        CandidateMaterializationOutcome[] expected =
            Enum.GetValues<CandidateMaterializationOutcome>();
        Assert.Equal(expected.Order(), referenceOutcomes.Order());
        Assert.Equal(expected.Order(), productionOutcomes.Order());
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

    [Fact]
    public void H3_2C_ProductionScoringMatchesLegacyAcrossCandidateMatrix()
    {
        foreach (ScoringFixture fixture in ScoringFixtures())
        {
            CandidateScoringContext context = OrthogonalRouter.BuildScoringContext(
                fixture.Candidates[0].Route,
                fixture.Obstacles,
                fixture.PriorRoutes);
            var reference = new List<ScoredInput>();
            var production = new List<ScoredInput>();

            for (int index = 0; index < fixture.Candidates.Count; index++)
            {
                ScoringInput input = fixture.Candidates[index];
                reference.Add(new ScoredInput(index, input, LegacyScore(
                    input.Route,
                    input.Priority,
                    fixture.Obstacles,
                    fixture.PriorRoutes,
                    fixture.PreferredHorizontalY,
                    input.CoordinateKey)));
                production.Add(new ScoredInput(index, input, OrthogonalRouter.ScoreCandidate(
                    input.Route,
                    input.Priority,
                    context,
                    fixture.PreferredHorizontalY,
                    input.CoordinateKey)));
            }

            Assert.Equal(reference.Select(item => item.InputIndex),
                production.Select(item => item.InputIndex));
            for (int index = 0; index < reference.Count; index++)
            {
                AssertScoreEqual(fixture.Name, reference[index].Score, production[index].Score);
            }

            ScoredInput[] referenceLegal = reference
                .Where(item => item.Score.ObstacleIntersections == 0)
                .ToArray();
            ScoredInput[] productionLegal = production
                .Where(item => item.Score.ObstacleIntersections == 0)
                .ToArray();
            Assert.Equal(referenceLegal.Select(item => item.InputIndex),
                productionLegal.Select(item => item.InputIndex));

            ScoredInput[] referenceRanked = Rank(referenceLegal);
            ScoredInput[] productionRanked = Rank(productionLegal);
            Assert.Equal(referenceRanked.Select(item => item.InputIndex),
                productionRanked.Select(item => item.InputIndex));
            Assert.Equal(referenceRanked.FirstOrDefault()?.InputIndex,
                productionRanked.FirstOrDefault()?.InputIndex);
            AssertScoringFixtureCoverage(fixture, reference, referenceRanked);
        }
    }

    private static void AssertScoringFixtureCoverage(
        ScoringFixture fixture,
        IReadOnlyList<ScoredInput> scores,
        IReadOnlyList<ScoredInput> ranked)
    {
        switch (fixture.Name)
        {
            case "many-obstacles":
                Assert.Contains(scores, item => item.Score.ObstacleIntersections > 1);
                break;
            case "endpoint-inside-ordinary-obstacle":
                Assert.Equal(0, scores[0].Score.ObstacleIntersections);
                break;
            case "endpoint-inside-transformer-obstacle":
            case "endpoint-inside-customer-station-obstacle":
                Assert.True(scores[0].Score.ObstacleIntersections > 0);
                break;
            case "shared-endpoint-routes":
                Assert.True(fixture.Candidates[0].Route.SharesTerminalWith(fixture.PriorRoutes[0]));
                break;
            case "horizontal-overlap":
            case "vertical-overlap":
            case "reverse-direction-collinear-overlap":
            case "partial-overlap":
                Assert.True(scores[0].Score.OverlapLength > 0);
                break;
            case "interior-crossing":
                Assert.True(scores[0].Score.Crossings > 0);
                break;
            case "endpoint-only-touching":
                Assert.Equal(0, scores[0].Score.Crossings);
                break;
            case "multiple-prior-routes":
                Assert.Equal(3, fixture.PriorRoutes.Count);
                Assert.True(scores[0].Score.OverlapLength > 0);
                Assert.True(scores[0].Score.Crossings > 0);
                break;
            case "many-prior-segments":
                Assert.True(fixture.PriorRoutes[0].Segments.Count >= 10);
                break;
            case "guide":
                Assert.Contains(scores, item => item.Score.HorizontalGuideDeviation == 0.5);
                break;
            case "equal-score-tie":
                Assert.Equal(scores[0].Score, scores[1].Score);
                Assert.Equal(0, ranked[0].InputIndex);
                break;
            case "coordinate-key-tie-break":
            case "priority-tie-break":
                Assert.Equal(1, ranked[0].InputIndex);
                break;
            case "switching-margin-boundary":
                Assert.Equal(OrthogonalRouter.RouteFamilySwitchingMargin,
                    scores[1].Score.Length - scores[0].Score.Length);
                break;
            case "fixed-seed-randomized-orthogonal-routes":
                Assert.Equal(24, scores.Count);
                break;
        }
    }

    private static IEnumerable<ScoringFixture> ScoringFixtures()
    {
        OrthogonalRoute direct = ScoringRoute(Connection1,
            [new DocumentPoint(0, 0), new DocumentPoint(100, 0)]);
        OrthogonalRoute upper = ScoringRoute(Connection1,
            [new DocumentPoint(0, 0), new DocumentPoint(0, -20),
                new DocumentPoint(100, -20), new DocumentPoint(100, 0)]);
        OrthogonalRoute lower = ScoringRoute(Connection1,
            [new DocumentPoint(0, 0), new DocumentPoint(0, 20),
                new DocumentPoint(100, 20), new DocumentPoint(100, 0)]);
        ScoringInput[] basic =
        [
            Input(direct, 0),
            Input(upper, 1),
            Input(lower, 2)
        ];

        yield return Fixture("no-obstacles", basic);
        yield return Fixture("many-obstacles", basic,
        [
            Obstacle(401, RoutingObstacleKind.Pole, 15, -5, 10, 10),
            Obstacle(402, RoutingObstacleKind.RingCabinet, 40, 10, 20, 20),
            Obstacle(403, RoutingObstacleKind.PoleAttachment, 70, -25, 8, 10),
            Obstacle(404, RoutingObstacleKind.IntermediateTerminal, 88, -4, 7, 8)
        ]);
        yield return Fixture("endpoint-inside-ordinary-obstacle", basic,
            [Obstacle(405, RoutingObstacleKind.RingCabinet, -5, -5, 12, 12)]);
        yield return Fixture("endpoint-inside-transformer-obstacle", basic,
            [Obstacle(406, RoutingObstacleKind.Transformer, -5, -5, 12, 12)]);
        yield return Fixture("endpoint-inside-customer-station-obstacle", basic,
            [Obstacle(407, RoutingObstacleKind.CustomerStation, 93, -5, 12, 12)]);

        OrthogonalRoute sharedEndpoint = ScoringRoute(Connection2,
            [new DocumentPoint(0, 0), new DocumentPoint(0, 30),
                new DocumentPoint(60, 30)], Start1, End2);
        yield return Fixture("shared-endpoint-routes", basic, priorRoutes: [sharedEndpoint]);

        OrthogonalRoute horizontalPrior = ScoringRoute(Connection2,
            [new DocumentPoint(20, 0), new DocumentPoint(80, 0)], Start2, End2);
        yield return Fixture("horizontal-overlap", basic, priorRoutes: [horizontalPrior]);

        OrthogonalRoute verticalCandidate = ScoringRoute(Connection1,
            [new DocumentPoint(0, 0), new DocumentPoint(0, 100),
                new DocumentPoint(100, 100), new DocumentPoint(100, 0)]);
        OrthogonalRoute verticalPrior = ScoringRoute(Connection2,
            [new DocumentPoint(0, 20), new DocumentPoint(0, 80)], Start2, End2);
        yield return Fixture("vertical-overlap", [Input(verticalCandidate, 0)],
            priorRoutes: [verticalPrior]);

        OrthogonalRoute reversePrior = ScoringRoute(Connection2,
            [new DocumentPoint(80, 0), new DocumentPoint(20, 0)], Start2, End2);
        yield return Fixture("reverse-direction-collinear-overlap", basic,
            priorRoutes: [reversePrior]);

        OrthogonalRoute partialPrior = ScoringRoute(Connection2,
            [new DocumentPoint(75, 0), new DocumentPoint(125, 0)], Start2, End2);
        yield return Fixture("partial-overlap", basic, priorRoutes: [partialPrior]);

        OrthogonalRoute crossingPrior = ScoringRoute(Connection2,
            [new DocumentPoint(50, -20), new DocumentPoint(50, 20)], Start2, End2);
        yield return Fixture("interior-crossing", basic, priorRoutes: [crossingPrior]);

        OrthogonalRoute touchingPrior = ScoringRoute(Connection2,
            [new DocumentPoint(100, 0), new DocumentPoint(100, 30)], Start2, End2);
        yield return Fixture("endpoint-only-touching", basic, priorRoutes: [touchingPrior]);

        yield return Fixture("multiple-prior-routes", basic,
            priorRoutes: [horizontalPrior, crossingPrior, sharedEndpoint]);

        OrthogonalRoute manySegmentPrior = ScoringRoute(Connection3,
        [
            new DocumentPoint(10, -30), new DocumentPoint(10, 30),
            new DocumentPoint(25, 30), new DocumentPoint(25, -30),
            new DocumentPoint(40, -30), new DocumentPoint(40, 30),
            new DocumentPoint(55, 30), new DocumentPoint(55, -30),
            new DocumentPoint(70, -30), new DocumentPoint(70, 30),
            new DocumentPoint(85, 30), new DocumentPoint(85, -30)
        ], Id(107), Id(108));
        yield return Fixture("many-prior-segments", basic, priorRoutes: [manySegmentPrior]);
        yield return Fixture("guide", basic, preferredHorizontalY: -19.5);

        yield return Fixture("equal-score-tie",
            [Input(upper, 4, "same"), Input(upper, 4, "same")]);
        yield return Fixture("coordinate-key-tie-break",
            [Input(upper, 4, "b"), Input(upper, 4, "a")]);
        yield return Fixture("priority-tie-break",
            [Input(upper, 9, "same"), Input(upper, 3, "same")]);

        OrthogonalRoute marginRoute = ScoringRoute(Connection1,
            [new DocumentPoint(0, 0), new DocumentPoint(0, 4),
                new DocumentPoint(100, 4), new DocumentPoint(100, 0)]);
        yield return Fixture("switching-margin-boundary",
            [Input(direct, 0), Input(marginRoute, 1)]);

        yield return Fixture(
            "fixed-seed-randomized-orthogonal-routes",
            RandomScoringInputs(8675309),
            [
                Obstacle(420, RoutingObstacleKind.Pole, 15, -30, 6, 60),
                Obstacle(421, RoutingObstacleKind.Transformer, 45, -18, 9, 36),
                Obstacle(422, RoutingObstacleKind.CustomerStation, 75, -40, 11, 80)
            ],
            [horizontalPrior, crossingPrior, manySegmentPrior],
            12.25);
    }

    private static ScoringFixture Fixture(
        string name,
        IReadOnlyList<ScoringInput> candidates,
        IReadOnlyList<RoutingObstacle>? obstacles = null,
        IReadOnlyList<OrthogonalRoute>? priorRoutes = null,
        double? preferredHorizontalY = null) => new(
            name,
            candidates,
            obstacles ?? [],
            priorRoutes ?? [],
            preferredHorizontalY);

    private static RoutingObstacle Obstacle(
        int id,
        RoutingObstacleKind kind,
        double x,
        double y,
        double width,
        double height) => new(Id(id), kind, new DocumentRect(x, y, width, height));

    private static ScoringInput Input(
        OrthogonalRoute route,
        int priority,
        string? coordinateKey = null) => new(
            route,
            priority,
            coordinateKey ?? CoordinateKey(route));

    private static OrthogonalRoute ScoringRoute(
        Guid connectionId,
        IReadOnlyList<DocumentPoint> points,
        Guid? startTerminalId = null,
        Guid? endTerminalId = null) => new(
            connectionId,
            ConnectionType.Cable,
            startTerminalId ?? Start1,
            endTerminalId ?? End1,
            points);

    private static ScoringInput[] RandomScoringInputs(int seed)
    {
        var random = new Random(seed);
        var inputs = new ScoringInput[24];
        for (int index = 0; index < inputs.Length; index++)
        {
            double firstY = random.Next(-45, 46);
            double secondX = random.Next(25, 76);
            double secondY = random.Next(-45, 46);
            OrthogonalRoute route = ScoringRoute(Connection1,
            [
                new DocumentPoint(0, 0),
                new DocumentPoint(0, firstY),
                new DocumentPoint(secondX, firstY),
                new DocumentPoint(secondX, secondY),
                new DocumentPoint(100, secondY),
                new DocumentPoint(100, 0)
            ]);
            inputs[index] = Input(route, index);
        }

        return inputs;
    }

    private static RouteCandidateScore LegacyScore(
        OrthogonalRoute route,
        int priority,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> priorRoutes,
        double? preferredHorizontalY,
        string coordinateKey)
    {
        int obstacleIntersections = 0;
        foreach (OrthogonalRouteSegment segment in route.Segments)
        {
            foreach (RoutingObstacle obstacle in obstacles)
            {
                bool sourceObstacle = !RequiresStableOwnerExclusion(obstacle) &&
                    obstacle.Contains(route.Points[0]);
                bool targetObstacle = !RequiresStableOwnerExclusion(obstacle) &&
                    obstacle.Contains(route.Points[^1]);
                if (sourceObstacle && obstacle.Contains(segment.Start) ||
                    targetObstacle && obstacle.Contains(segment.End))
                {
                    continue;
                }

                if (LegacyIntersectsInterior(segment, obstacle.Bounds))
                {
                    obstacleIntersections++;
                }
            }
        }

        double overlap = 0;
        int crossings = 0;
        foreach (OrthogonalRoute prior in priorRoutes)
        {
            foreach (OrthogonalRouteSegment current in route.Segments)
            {
                foreach (OrthogonalRouteSegment existing in prior.Segments)
                {
                    overlap += OrthogonalRouter.CollinearOverlap(current, existing);
                    if (OrthogonalRouter.HasInteriorCrossing(current, existing))
                    {
                        crossings++;
                    }
                }
            }
        }

        return new RouteCandidateScore(
            obstacleIntersections,
            preferredHorizontalY is double guideY
                ? route.Segments
                    .Where(segment => segment.IsHorizontal)
                    .Select(segment => Math.Abs(segment.Start.YMillimeters - guideY))
                    .DefaultIfEmpty(double.MaxValue)
                    .Min()
                : 0,
            overlap,
            crossings,
            Math.Max(0, route.Points.Count - 2),
            route.Length,
            priority,
            coordinateKey);
    }

    private static bool RequiresStableOwnerExclusion(RoutingObstacle obstacle) =>
        obstacle.Kind is RoutingObstacleKind.Transformer or
            RoutingObstacleKind.CustomerStation;

    private static bool LegacyIntersectsInterior(
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

    private static ScoredInput[] Rank(IEnumerable<ScoredInput> inputs) => inputs
        .OrderBy(item => item.Score.ObstacleIntersections)
        .ThenBy(item => item.Score.HorizontalGuideDeviation)
        .ThenBy(item => item.Score.OverlapLength)
        .ThenBy(item => item.Score.Crossings)
        .ThenBy(item => item.Score.Bends)
        .ThenBy(item => item.Score.Length)
        .ThenBy(item => item.Score.Priority)
        .ThenBy(item => item.Score.CoordinateKey, StringComparer.Ordinal)
        .ToArray();

    private static void AssertScoreEqual(
        string name,
        RouteCandidateScore expected,
        RouteCandidateScore actual)
    {
        Assert.True(expected.ObstacleIntersections == actual.ObstacleIntersections, name);
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected.HorizontalGuideDeviation) ==
            BitConverter.DoubleToInt64Bits(actual.HorizontalGuideDeviation),
            name);
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected.OverlapLength) ==
            BitConverter.DoubleToInt64Bits(actual.OverlapLength),
            name);
        Assert.True(expected.Crossings == actual.Crossings, name);
        Assert.True(expected.Bends == actual.Bends, name);
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected.Length) ==
            BitConverter.DoubleToInt64Bits(actual.Length),
            name);
        Assert.True(expected.Priority == actual.Priority, name);
        Assert.True(expected.CoordinateKey == actual.CoordinateKey, name);
    }

    private static string CoordinateKey(OrthogonalRoute route) => string.Join(
        ";",
        route.Points.Select(point => $"{point.XMillimeters:R},{point.YMillimeters:R}"));

    private static IEnumerable<MaterializationFixture> MaterializationFixtures()
    {
        var start = new DocumentPoint(0, 0);
        var startStub = new DocumentPoint(8, 0);
        var shortStartStub = new DocumentPoint(4, 0);
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
            "stub-invalid-short-start", request, start, shortStartStub,
            [[shortStartStub, new DocumentPoint(4, 20),
                new DocumentPoint(92, 20), endStub]], endStub, end,
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

    private sealed record ScoringFixture(
        string Name,
        IReadOnlyList<ScoringInput> Candidates,
        IReadOnlyList<RoutingObstacle> Obstacles,
        IReadOnlyList<OrthogonalRoute> PriorRoutes,
        double? PreferredHorizontalY);

    private sealed record ScoringInput(
        OrthogonalRoute Route,
        int Priority,
        string CoordinateKey);

    private sealed record ScoredInput(
        int InputIndex,
        ScoringInput Input,
        RouteCandidateScore Score);

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
