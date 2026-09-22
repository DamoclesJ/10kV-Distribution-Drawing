using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Diagnostics;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class OrthogonalRouter
{
    // One formal port-stub length is large enough to suppress the 0.2 mm
    // symmetry flip, but small enough to permit an intentional route change.
    internal const double RouteFamilySwitchingMargin = 8;
    private readonly DrawingMetrics _metrics;
    private readonly RouteContinuityContext? _continuity;
    private readonly CandidateFamilyEvaluationMode _familyEvaluationMode;
    private readonly RoutingEvaluationStatistics? _evaluationStatistics;
    internal DrawingMetrics Metrics => _metrics;

    public OrthogonalRouter(
        DrawingMetrics? metrics = null,
        RouteContinuityContext? continuity = null)
        : this(metrics, continuity, CandidateFamilyEvaluationMode.Lazy, null)
    {
    }

    internal OrthogonalRouter(
        DrawingMetrics? metrics,
        RouteContinuityContext? continuity,
        CandidateFamilyEvaluationMode familyEvaluationMode,
        RoutingEvaluationStatistics? evaluationStatistics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
        _continuity = continuity;
        _familyEvaluationMode = familyEvaluationMode;
        _evaluationStatistics = evaluationStatistics;
    }

    public OrthogonalRoute Route(
        ConnectionRouteRequest request,
        IEnumerable<RoutingObstacle> obstacles,
        IEnumerable<OrthogonalRoute>? plannedRoutes = null)
    {
        return RouteCore(
            request,
            obstacles,
            plannedRoutes,
            useContinuity: true,
            plannedRoutesAreSorted: false);
    }

    internal OrthogonalRoute RouteFromPlanner(
        ConnectionRouteRequest request,
        RoutingEnvironment environment,
        IReadOnlyList<OrthogonalRoute> plannedRoutes,
        IEnumerable<Guid>? additionalExcludedSourceIds = null) => RouteCore(
            request,
            environment.SourceObstacles,
            plannedRoutes,
            useContinuity: true,
            plannedRoutesAreSorted: true,
            environment,
            additionalExcludedSourceIds);

    internal OrthogonalRoute RouteFromPlanner(
        ConnectionRouteRequest request,
        IEnumerable<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> plannedRoutes) => RouteCore(
            request,
            obstacles,
            plannedRoutes,
            useContinuity: true,
            plannedRoutesAreSorted: true);

    internal OrthogonalRoute RouteWithoutContinuity(
        ConnectionRouteRequest request,
        RoutingEnvironment environment,
        IReadOnlyList<OrthogonalRoute>? plannedRoutes = null,
        IEnumerable<Guid>? additionalExcludedSourceIds = null)
    {
        return RouteCore(
            request,
            environment.SourceObstacles,
            plannedRoutes,
            useContinuity: false,
            plannedRoutesAreSorted: true,
            environment,
            additionalExcludedSourceIds);
    }

    internal OrthogonalRoute RouteWithoutContinuity(
        ConnectionRouteRequest request,
        IEnumerable<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute>? plannedRoutes = null) => RouteCore(
            request,
            obstacles,
            plannedRoutes,
            useContinuity: false,
            plannedRoutesAreSorted: true);

    private OrthogonalRoute RouteCore(
        ConnectionRouteRequest request,
        IEnumerable<RoutingObstacle> obstacles,
        IEnumerable<OrthogonalRoute>? plannedRoutes,
        bool useContinuity,
        bool plannedRoutesAreSorted,
        RoutingEnvironment? environment = null,
        IEnumerable<Guid>? additionalExcludedSourceIds = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(obstacles);
        bool diagnosticsEnabled = DrawingPerformanceTrace.IsEnabled;

        IReadOnlyList<RoutingObstacle> expandedObstacles;
        IReadOnlyList<OrthogonalRoute> priorRoutes;
        RoutingObstacle[] pathfindingObstacles;
        TerminalAnchorDirection startDirection;
        TerminalAnchorDirection endOutwardDirection;
        DocumentPoint startStub;
        DocumentPoint endStub;
        RouteContinuityPreference? continuityPreference;
        EffectiveRoutingView? effectiveView = null;
        VisibilityAxisBasis? visibilityAxisBasis = null;
        using (DrawingPerformanceTrace.PhaseOperation prepare =
               DrawingPerformanceTrace.Measure("RoutingPrepare", request.ConnectionId))
        {
            if (environment is not null)
            {
                IEnumerable<Guid> exclusions =
                    (request.ExcludedObstacleSourceIds?.AsEnumerable() ?? Enumerable.Empty<Guid>())
                    .Concat(additionalExcludedSourceIds ?? []);
                effectiveView = environment.GetEffectiveView(
                    exclusions,
                    _metrics.Routing.ObstacleClearance,
                    _metrics.Routing.ParallelSpacing);
                expandedObstacles = effectiveView.Obstacles;
            }
            else
            {
                HashSet<Guid> excludedSourceIds = request.ExcludedObstacleSourceIds?.ToHashSet() ?? [];
                expandedObstacles = obstacles
                    .Where(obstacle => !excludedSourceIds.Contains(obstacle.SourceId))
                    .OrderBy(obstacle => obstacle.SourceId)
                    .Select(obstacle => obstacle.Expand(_metrics.Routing.ObstacleClearance))
                    .ToArray();
            }
            priorRoutes = plannedRoutesAreSorted && plannedRoutes is IReadOnlyList<OrthogonalRoute> sorted
                ? sorted
                : plannedRoutes?.OrderBy(route => route.ConnectionId).ToArray() ?? [];
            pathfindingObstacles = expandedObstacles
                .Where(obstacle =>
                    RequiresStableOwnerExclusion(obstacle) ||
                    !obstacle.Contains(request.Start.Position) &&
                    !obstacle.Contains(request.End.Position))
                .ToArray();
            if (environment is not null && effectiveView is not null)
            {
                visibilityAxisBasis = environment.GetVisibilityAxisBasis(
                    effectiveView,
                    pathfindingObstacles);
            }

            startDirection = ResolveDirection(
                request.Start.Direction,
                request.Start.Position,
                request.End.Position);
            endOutwardDirection = request.End.Direction == TerminalAnchorDirection.Auto
                ? Opposite(ResolveDirection(
                    TerminalAnchorDirection.Auto,
                    request.Start.Position,
                    request.End.Position))
                : request.End.Direction;
            startStub = Move(
                request.Start.Position,
                startDirection,
                Math.Max(_metrics.Routing.PortStubLength, request.Start.MinimumStubLength));
            endStub = Move(
                request.End.Position,
                endOutwardDirection,
                Math.Max(_metrics.Routing.PortStubLength, request.End.MinimumStubLength));
            continuityPreference = useContinuity
                ? _continuity?.GetAccepted(request.ConnectionId)
                : null;
            prepare.SetCounts(
                expandedObstacles.Count,
                pathfindingObstacles.Length,
                priorRoutes.Count,
                diagnosticsEnabled ? priorRoutes.Sum(route => route.Segments.Count) : 0);
        }

        int rawCandidateCount;
        Candidate[] candidates;
        using (DrawingPerformanceTrace.PhaseOperation materialization =
               DrawingPerformanceTrace.Measure("CandidateMaterialization", request.ConnectionId))
        {
            candidates = MaterializeCandidates(
                request,
                request.Start.Position,
                startStub,
                CreateCandidates(
                    request.ConnectionId,
                    startStub,
                    endStub,
                    expandedObstacles,
                    pathfindingObstacles,
                    request.PreferredHorizontalY,
                    includeContinuityAlternatives: continuityPreference is not null,
                    effectiveView?.CandidateAxes,
                    visibilityAxisBasis),
                endStub,
                request.End.Position,
                startDirection,
                endOutwardDirection,
                out rawCandidateCount);
            materialization.SetCounts(
                rawCandidateCount,
                candidates.Length,
                expandedObstacles.Count,
                continuityPreference is null ? 0 : 1);
        }

        if (candidates.Length == 0)
        {
            OrthogonalRoute fallback = CreateFallbackRoute(
                request,
                request.Start.Position,
                startStub,
                endStub,
                request.End.Position,
                startDirection,
                endOutwardDirection,
                pathfindingObstacles);
            CandidateScoringContext scoringContext = BuildScoringContext(
                fallback,
                expandedObstacles,
                priorRoutes);
            return CompleteSelection(
                request,
                fallback,
                ScoreCandidate(fallback, int.MaxValue, scoringContext,
                    request.PreferredHorizontalY, CoordinateKey(fallback)),
                Classify(request, fallback, expandedObstacles, environment),
                useContinuity);
        }

        Candidate[] scoredCandidates;
        CandidateScoringContext scoringContextForSelection;
        using (DrawingPerformanceTrace.PhaseOperation scoring =
               DrawingPerformanceTrace.Measure("CandidateScoring", request.ConnectionId))
        {
            scoringContextForSelection = BuildScoringContext(
                candidates[0].Route,
                expandedObstacles,
                priorRoutes);
            scoredCandidates = candidates
                .Select(candidate => candidate with
                {
                    Score = ScoreCandidate(
                        candidate.Route,
                        candidate.Priority,
                        scoringContextForSelection,
                        request.PreferredHorizontalY,
                        candidate.Key),
                    Family = _familyEvaluationMode == CandidateFamilyEvaluationMode.EagerReference
                        ? Classify(request, candidate.Route, expandedObstacles, environment)
                        : null
                })
                .Where(candidate => candidate.Score.ObstacleIntersections == 0)
                .ToArray();
            scoring.SetCounts(
                scoredCandidates.Length,
                candidates.Length,
                priorRoutes.Count,
                diagnosticsEnabled ? priorRoutes.Sum(route => route.Segments.Count) : 0);
        }

        if (scoredCandidates.Length == 0)
        {
            OrthogonalRoute fallback = CreateFallbackRoute(
                request,
                request.Start.Position,
                startStub,
                endStub,
                request.End.Position,
                startDirection,
                endOutwardDirection,
                pathfindingObstacles);
            return CompleteSelection(
                request,
                fallback,
                ScoreCandidate(
                    fallback,
                    int.MaxValue,
                    scoringContextForSelection,
                    request.PreferredHorizontalY,
                    CoordinateKey(fallback)),
                Classify(request, fallback, expandedObstacles, environment),
                useContinuity);
        }

        Candidate[] ranked;
        using (DrawingPerformanceTrace.PhaseOperation ranking =
               DrawingPerformanceTrace.Measure("CandidateRanking", request.ConnectionId))
        {
            ranked = scoredCandidates
                .OrderBy(candidate => candidate.Score.ObstacleIntersections)
                .ThenBy(candidate => candidate.Score.HorizontalGuideDeviation)
                .ThenBy(candidate => candidate.Score.OverlapLength)
                .ThenBy(candidate => candidate.Score.Crossings)
                .ThenBy(candidate => candidate.Score.Bends)
                .ThenBy(candidate => candidate.Score.Length)
                .ThenBy(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
                .ToArray();
            ranking.SetCounts(ranked.Length);
        }
        Candidate selected = ranked[0];
        RouteFamilyKey selectedFamily;
        int classificationCount = 0;
        using (DrawingPerformanceTrace.PhaseOperation classification =
               DrawingPerformanceTrace.Measure("RouteFamilyClassification", request.ConnectionId))
        {
            if (_familyEvaluationMode == CandidateFamilyEvaluationMode.EagerReference)
            {
                Candidate? currentFamily = continuityPreference is { } preference
                    ? ranked.FirstOrDefault(candidate => candidate.Family == preference.Family)
                    : null;
                if (currentFamily is not null &&
                    IsWithinSwitchingMargin(currentFamily.Score, selected.Score))
                {
                    selected = currentFamily;
                }

                selectedFamily = Classify(request, selected.Route, expandedObstacles, environment);
                classificationCount = candidates.Length + 1;
            }
            else if (continuityPreference is { } preference)
            {
                RouteFamilyKey? overallFamily = null;
                RouteFamilyKey? matchingFamily = null;
                Candidate? currentFamily = null;
                for (int index = 0; index < ranked.Length; index++)
                {
                    Candidate candidate = ranked[index];
                    RouteFamilyKey family = Classify(request, candidate.Route, expandedObstacles, environment);
                    classificationCount++;
                    if (index == 0)
                    {
                        overallFamily = family;
                    }
                    if (family != preference.Family)
                    {
                        continue;
                    }

                    currentFamily = candidate;
                    matchingFamily = family;
                    break;
                }

                if (currentFamily is not null &&
                    IsWithinSwitchingMargin(currentFamily.Score, selected.Score))
                {
                    selected = currentFamily;
                    selectedFamily = matchingFamily!.Value;
                }
                else
                {
                    selectedFamily = overallFamily!.Value;
                }
            }
            else
            {
                selectedFamily = Classify(request, selected.Route, expandedObstacles, environment);
                classificationCount = 1;
            }

            classification.SetCounts(classificationCount, ranked.Length);
        }

        return CompleteSelection(
            request,
            selected.Route,
            selected.Score,
            selectedFamily,
            useContinuity);
    }

    private OrthogonalRoute CompleteSelection(
        ConnectionRouteRequest request,
        OrthogonalRoute route,
        RouteCandidateScore score,
        RouteFamilyKey family,
        bool stage)
    {
        route.ContinuityFamily = family;
        route.ContinuityScore = score;
        if (stage)
        {
            _continuity?.Stage(request.ConnectionId, family, score);
        }
        return route;
    }

    private RouteFamilyKey Classify(
        ConnectionRouteRequest request,
        OrthogonalRoute route,
        IReadOnlyList<RoutingObstacle> obstacles,
        RoutingEnvironment? environment)
    {
        _evaluationStatistics?.RecordFamilyClassification();
        return RouteFamilyClassifier.ClassifySorted(
            request,
            route,
            obstacles,
            _metrics,
            environment?.SourceIdText);
    }

    private static bool IsWithinSwitchingMargin(
        RouteCandidateScore current,
        RouteCandidateScore overall)
    {
        return current.ObstacleIntersections == overall.ObstacleIntersections &&
               current.HorizontalGuideDeviation == overall.HorizontalGuideDeviation &&
               current.OverlapLength == overall.OverlapLength &&
               current.Crossings == overall.Crossings &&
               current.Bends == overall.Bends &&
               current.Length <= overall.Length + RouteFamilySwitchingMargin;
    }

    internal static bool HasBacktracking(OrthogonalRoute route)
    {
        for (int firstIndex = 0; firstIndex < route.Segments.Count; firstIndex++)
        {
            OrthogonalRouteSegment first = route.Segments[firstIndex];
            for (int secondIndex = firstIndex + 1;
                 secondIndex < route.Segments.Count;
                 secondIndex++)
            {
                OrthogonalRouteSegment second = route.Segments[secondIndex];
                if (first.IsHorizontal && second.IsHorizontal &&
                    first.Start.YMillimeters == second.Start.YMillimeters &&
                    Math.Sign(first.End.XMillimeters - first.Start.XMillimeters) !=
                    Math.Sign(second.End.XMillimeters - second.Start.XMillimeters) &&
                    OverlapLength(
                        first.Start.XMillimeters,
                        first.End.XMillimeters,
                        second.Start.XMillimeters,
                        second.End.XMillimeters) > 0)
                {
                    return true;
                }

                if (first.IsVertical && second.IsVertical &&
                    first.Start.XMillimeters == second.Start.XMillimeters &&
                    Math.Sign(first.End.YMillimeters - first.Start.YMillimeters) !=
                    Math.Sign(second.End.YMillimeters - second.Start.YMillimeters) &&
                    OverlapLength(
                        first.Start.YMillimeters,
                        first.End.YMillimeters,
                        second.Start.YMillimeters,
                        second.End.YMillimeters) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private OrthogonalRoute CreateFallbackRoute(
        ConnectionRouteRequest request,
        DocumentPoint start,
        DocumentPoint startStub,
        DocumentPoint endStub,
        DocumentPoint end,
        TerminalAnchorDirection startDirection,
        TerminalAnchorDirection endOutwardDirection,
        IReadOnlyList<RoutingObstacle> obstacles)
    {
        var points = new List<DocumentPoint> { start, startStub };
        if (startStub.XMillimeters != endStub.XMillimeters &&
            startStub.YMillimeters != endStub.YMillimeters)
        {
            points.Add(new DocumentPoint(endStub.XMillimeters, startStub.YMillimeters));
        }

        points.Add(endStub);
        points.Add(end);
        OrthogonalRoute route = new(
            request.ConnectionId,
            request.ConnectionType,
            request.StartTerminalId,
            request.EndTerminalId,
            points);
        if ((request.DisallowBacktracking && HasBacktracking(route)) ||
            (request.EnforceRequiredStubConstraints &&
            (!HasTerminalStubs(route, start, startDirection, request.Start.MinimumStubLength,
                 end, endOutwardDirection, request.End.MinimumStubLength) ||
             route.Segments.Any(segment => obstacles.Any(obstacle =>
                 IntersectsInterior(segment, obstacle.Bounds))))))
        {
            throw new RoutingConstraintException(
                "无法在当前杆间距或障碍物条件下生成满足最小导线段的线路。 ");
        }

        return route;
    }

    private static bool StartsInDirection(
        OrthogonalRoute route,
        TerminalAnchorDirection direction)
    {
        OrthogonalRouteSegment first = route.Segments[0];
        return direction switch
        {
            TerminalAnchorDirection.Left => first.IsHorizontal &&
                first.End.XMillimeters < first.Start.XMillimeters,
            TerminalAnchorDirection.Right => first.IsHorizontal &&
                first.End.XMillimeters > first.Start.XMillimeters,
            TerminalAnchorDirection.Up => first.IsVertical &&
                first.End.YMillimeters < first.Start.YMillimeters,
            TerminalAnchorDirection.Down => first.IsVertical &&
                first.End.YMillimeters > first.Start.YMillimeters,
            _ => true
        };
    }

    internal bool HasTerminalStubs(
        OrthogonalRoute route,
        DocumentPoint start,
        TerminalAnchorDirection startDirection,
        double requestedStartLength,
        DocumentPoint end,
        TerminalAnchorDirection endOutwardDirection,
        double requestedEndLength)
    {
        if (route.Segments.Count == 0)
        {
            return false;
        }

        double startLength = Math.Max(
            _metrics.Routing.PortStubLength,
            requestedStartLength);
        double endLength = Math.Max(
            _metrics.Routing.PortStubLength,
            requestedEndLength);

        return StartsInDirection(route, startDirection) &&
               HasLengthFromStart(route.Segments[0], start, startDirection, startLength) &&
               HasLengthIntoEnd(
                   route.Segments[^1],
                   end,
                   Opposite(endOutwardDirection),
                   endLength);
    }

    private static bool HasLengthFromStart(
        OrthogonalRouteSegment segment,
        DocumentPoint terminal,
        TerminalAnchorDirection direction,
        double minimumLength)
    {
        if (segment.Start != terminal)
        {
            return false;
        }

        return IsInDirection(segment, direction) && segment.Length >= minimumLength;
    }

    private static bool HasLengthIntoEnd(
        OrthogonalRouteSegment segment,
        DocumentPoint terminal,
        TerminalAnchorDirection direction,
        double minimumLength)
    {
        if (segment.End != terminal)
        {
            return false;
        }

        return IsInDirection(segment, direction) &&
               segment.Length >= minimumLength;
    }

    private static bool IsInDirection(
        OrthogonalRouteSegment segment,
        TerminalAnchorDirection direction)
    {
        return direction switch
        {
            TerminalAnchorDirection.Left => segment.End.XMillimeters < segment.Start.XMillimeters,
            TerminalAnchorDirection.Right => segment.End.XMillimeters > segment.Start.XMillimeters,
            TerminalAnchorDirection.Up => segment.End.YMillimeters < segment.Start.YMillimeters,
            TerminalAnchorDirection.Down => segment.End.YMillimeters > segment.Start.YMillimeters,
            _ => false
        };
    }

    private static TerminalAnchorDirection Opposite(TerminalAnchorDirection direction)
    {
        return direction switch
        {
            TerminalAnchorDirection.Left => TerminalAnchorDirection.Right,
            TerminalAnchorDirection.Right => TerminalAnchorDirection.Left,
            TerminalAnchorDirection.Up => TerminalAnchorDirection.Down,
            TerminalAnchorDirection.Down => TerminalAnchorDirection.Up,
            _ => TerminalAnchorDirection.Auto
        };
    }

    public IReadOnlyList<DocumentPoint> CreatePreview(
        TerminalAnchor start,
        DocumentPoint end)
    {
        if (start.Position == end)
        {
            return [];
        }

        TerminalAnchorDirection direction = ResolveDirection(
            start.Direction,
            start.Position,
            end);
        DocumentPoint stub = Move(
            start.Position,
            direction,
            Math.Max(
                _metrics.Routing.PortStubLength,
                start.MinimumStubLength));
        DocumentPoint corner = Math.Abs(end.XMillimeters - stub.XMillimeters) >=
                               Math.Abs(end.YMillimeters - stub.YMillimeters)
            ? new DocumentPoint(end.XMillimeters, stub.YMillimeters)
            : new DocumentPoint(stub.XMillimeters, end.YMillimeters);
        return NormalizePreview([start.Position, stub, corner, end]);
    }

    private IEnumerable<IReadOnlyList<DocumentPoint>> CreateCandidates(
        Guid connectionId,
        DocumentPoint start,
        DocumentPoint end,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<RoutingObstacle> pathfindingObstacles,
        double? preferredHorizontalY,
        bool includeContinuityAlternatives,
        CandidateAxisBasis? candidateAxisBasis,
        VisibilityAxisBasis? visibilityAxisBasis)
    {
        if (start.XMillimeters == end.XMillimeters ||
            start.YMillimeters == end.YMillimeters)
        {
            yield return [start, end];
        }

        yield return [start, new DocumentPoint(end.XMillimeters, start.YMillimeters), end];
        yield return [start, new DocumentPoint(start.XMillimeters, end.YMillimeters), end];

        if (preferredHorizontalY is double guideY)
        {
            yield return
            [
                start,
                new DocumentPoint(start.XMillimeters, guideY),
                new DocumentPoint(end.XMillimeters, guideY),
                end
            ];
        }

        var xChannels = new SortedSet<double>
        {
            (start.XMillimeters + end.XMillimeters) / 2,
            start.XMillimeters - _metrics.Routing.MinimumDoglegLength,
            start.XMillimeters + _metrics.Routing.MinimumDoglegLength,
            end.XMillimeters - _metrics.Routing.MinimumDoglegLength,
            end.XMillimeters + _metrics.Routing.MinimumDoglegLength
        };
        var yChannels = new SortedSet<double>
        {
            (start.YMillimeters + end.YMillimeters) / 2,
            start.YMillimeters - _metrics.Routing.MinimumDoglegLength,
            start.YMillimeters + _metrics.Routing.MinimumDoglegLength,
            end.YMillimeters - _metrics.Routing.MinimumDoglegLength,
            end.YMillimeters + _metrics.Routing.MinimumDoglegLength
        };

        if (candidateAxisBasis is null)
        {
            foreach (RoutingObstacle obstacle in obstacles)
            {
                xChannels.Add(obstacle.Bounds.XMillimeters - _metrics.Routing.ParallelSpacing);
                xChannels.Add(
                    obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters +
                    _metrics.Routing.ParallelSpacing);
                yChannels.Add(obstacle.Bounds.YMillimeters - _metrics.Routing.ParallelSpacing);
                yChannels.Add(
                    obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters +
                    _metrics.Routing.ParallelSpacing);
            }
        }
        else
        {
            xChannels.UnionWith(candidateAxisBasis.XCoordinates);
            yChannels.UnionWith(candidateAxisBasis.YCoordinates);
        }

        foreach (double x in xChannels)
        {
            yield return
            [
                start,
                new DocumentPoint(x, start.YMillimeters),
                new DocumentPoint(x, end.YMillimeters),
                end
            ];
        }

        foreach (double y in yChannels)
        {
            yield return
            [
                start,
                new DocumentPoint(start.XMillimeters, y),
                new DocumentPoint(end.XMillimeters, y),
                end
            ];
        }

        IReadOnlyList<DocumentPoint>? obstacleAvoiding = FindObstacleAvoidingPath(
            connectionId,
            start,
            end,
            pathfindingObstacles,
            visibilityAxisBasis);
        if (obstacleAvoiding is not null)
        {
            yield return obstacleAvoiding;
        }

        if (!includeContinuityAlternatives)
        {
            yield break;
        }

        // Keep the non-winning side of an obstacle available for continuity.
        // These candidates follow the existing shortest-path candidate, so they
        // do not change the formal winner when no continuity preference exists.
        foreach (RoutingObstacle obstacle in obstacles)
        {
            foreach (double x in new[]
                     {
                         obstacle.Bounds.XMillimeters,
                         obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters
                     })
            {
                yield return
                [
                    start,
                    new DocumentPoint(x, start.YMillimeters),
                    new DocumentPoint(x, end.YMillimeters),
                    end
                ];
            }

            foreach (double y in new[]
                     {
                         obstacle.Bounds.YMillimeters,
                         obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters
                     })
            {
                yield return
                [
                    start,
                    new DocumentPoint(start.XMillimeters, y),
                    new DocumentPoint(end.XMillimeters, y),
                    end
                ];
            }
        }
    }

    private IReadOnlyList<DocumentPoint>? FindObstacleAvoidingPath(
        Guid connectionId,
        DocumentPoint start,
        DocumentPoint end,
        IReadOnlyList<RoutingObstacle> obstacles,
        VisibilityAxisBasis? axisBasis)
    {
        var xCoordinates = new SortedSet<double> { start.XMillimeters, end.XMillimeters };
        var yCoordinates = new SortedSet<double> { start.YMillimeters, end.YMillimeters };
        if (axisBasis is null)
        {
            foreach (RoutingObstacle obstacle in obstacles)
            {
                xCoordinates.Add(obstacle.Bounds.XMillimeters);
                xCoordinates.Add(obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters);
                yCoordinates.Add(obstacle.Bounds.YMillimeters);
                yCoordinates.Add(obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters);
            }
        }
        else
        {
            xCoordinates.UnionWith(axisBasis.XCoordinates);
            yCoordinates.UnionWith(axisBasis.YCoordinates);
        }

        VisibilityGraphConstruction construction;
        bool diagnosticsEnabled = DrawingPerformanceTrace.IsEnabled;
        using (DrawingPerformanceTrace.PhaseOperation graph =
               DrawingPerformanceTrace.Measure("VisibilityGraphBuild", connectionId))
        {
            construction = ConstructVisibilityGraph(
                xCoordinates,
                yCoordinates,
                obstacles,
                diagnosticsEnabled);
            graph.SetCounts(
                xCoordinates.Count,
                yCoordinates.Count,
                construction.Nodes.Length,
                construction.EdgeCount);
        }

        DocumentPoint[] nodes = construction.Nodes;
        Dictionary<DocumentPoint, List<DocumentPoint>> adjacency = construction.Adjacency;
        int edgeCount = construction.EdgeCount;
        if (!adjacency.ContainsKey(start) || !adjacency.ContainsKey(end))
        {
            return null;
        }

        using DrawingPerformanceTrace.PhaseOperation dijkstra =
            DrawingPerformanceTrace.Measure("VisibilityDijkstra", connectionId);
        var distances = nodes.ToDictionary(point => point, _ => double.PositiveInfinity);
        var previous = new Dictionary<DocumentPoint, DocumentPoint>();
        var queue = new PriorityQueue<DocumentPoint, (double Distance, double X, double Y)>();
        distances[start] = 0;
        queue.Enqueue(start, (0, start.XMillimeters, start.YMillimeters));
        int dequeuedCount = 0;

        while (queue.TryDequeue(out DocumentPoint current, out var priority))
        {
            if (diagnosticsEnabled)
            {
                dequeuedCount++;
            }
            if (priority.Distance > distances[current])
            {
                continue;
            }

            if (current == end)
            {
                break;
            }

            foreach (DocumentPoint neighbor in adjacency[current]
                         .OrderBy(point => point.XMillimeters)
                         .ThenBy(point => point.YMillimeters))
            {
                double distance = distances[current] + ManhattanDistance(current, neighbor);
                if (distance >= distances[neighbor])
                {
                    continue;
                }

                distances[neighbor] = distance;
                previous[neighbor] = current;
                queue.Enqueue(
                    neighbor,
                    (distance, neighbor.XMillimeters, neighbor.YMillimeters));
            }
        }

        if (!previous.ContainsKey(end))
        {
            dijkstra.SetCounts(nodes.Length, edgeCount, dequeuedCount);
            return null;
        }

        var path = new List<DocumentPoint> { end };
        while (path[^1] != start)
        {
            path.Add(previous[path[^1]]);
        }

        path.Reverse();
        dijkstra.SetCounts(nodes.Length, edgeCount, dequeuedCount, path.Count);
        return NormalizePreview(path);
    }

    internal static VisibilityGraphConstruction ConstructVisibilityGraph(
        IEnumerable<double> xCoordinates,
        IEnumerable<double> yCoordinates,
        IReadOnlyList<RoutingObstacle> obstacles,
        bool countEdges)
    {
        var obstacleIndex = new VisibilityObstacleIndex(
            xCoordinates,
            yCoordinates,
            obstacles);
        double[] orderedXCoordinates = xCoordinates.ToArray();
        double[] orderedYCoordinates = yCoordinates.ToArray();
        var nodeList = new List<DocumentPoint>(
            orderedXCoordinates.Length * orderedYCoordinates.Length);
        var columns = new List<List<DocumentPoint>>(orderedXCoordinates.Length);
        var rows = new List<DocumentPoint>?[orderedYCoordinates.Length];
        var rowEncounterOrder = new List<int>(orderedYCoordinates.Length);

        foreach (double x in orderedXCoordinates)
        {
            var column = new List<DocumentPoint>(orderedYCoordinates.Length);
            for (int yIndex = 0; yIndex < orderedYCoordinates.Length; yIndex++)
            {
                var point = new DocumentPoint(x, orderedYCoordinates[yIndex]);
                if (obstacleIndex.ContainsInterior(point))
                {
                    continue;
                }

                nodeList.Add(point);
                column.Add(point);
                List<DocumentPoint>? row = rows[yIndex];
                if (row is null)
                {
                    row = new List<DocumentPoint>(orderedXCoordinates.Length);
                    rows[yIndex] = row;
                    rowEncounterOrder.Add(yIndex);
                }

                row.Add(point);
            }

            if (column.Count > 0)
            {
                columns.Add(column);
            }
        }

        DocumentPoint[] nodes = nodeList.ToArray();
        var adjacency = new Dictionary<DocumentPoint, List<DocumentPoint>>(nodes.Length);
        foreach (DocumentPoint node in nodes)
        {
            adjacency.Add(node, new List<DocumentPoint>());
        }

        int edgeCount = 0;
        foreach (List<DocumentPoint> column in columns)
        {
            edgeCount += ConnectVisibleNeighbors(
                column,
                adjacency,
                obstacleIndex,
                countEdges);
        }

        foreach (int yIndex in rowEncounterOrder)
        {
            edgeCount += ConnectVisibleNeighbors(
                rows[yIndex]!,
                adjacency,
                obstacleIndex,
                countEdges);
        }

        return new VisibilityGraphConstruction(
            orderedXCoordinates,
            orderedYCoordinates,
            nodes,
            adjacency,
            columns,
            rows,
            rowEncounterOrder,
            edgeCount);
    }

    private static int ConnectVisibleNeighbors(
        IReadOnlyList<DocumentPoint> ordered,
        IDictionary<DocumentPoint, List<DocumentPoint>> adjacency,
        VisibilityObstacleIndex obstacleIndex,
        bool countEdges)
    {
        int edgeCount = 0;
        for (int index = 1; index < ordered.Count; index++)
        {
            DocumentPoint previous = ordered[index - 1];
            DocumentPoint current = ordered[index];
            var segment = new OrthogonalRouteSegment(previous, current, 0);
            if (obstacleIndex.IntersectsInterior(segment))
            {
                continue;
            }

            adjacency[previous].Add(current);
            adjacency[current].Add(previous);
            if (countEdges)
            {
                edgeCount++;
            }
        }

        return edgeCount;
    }

    private static double ManhattanDistance(DocumentPoint first, DocumentPoint second) =>
        Math.Abs(first.XMillimeters - second.XMillimeters) +
        Math.Abs(first.YMillimeters - second.YMillimeters);

    internal Candidate[] MaterializeCandidates(
        ConnectionRouteRequest request,
        DocumentPoint start,
        DocumentPoint startStub,
        IEnumerable<IReadOnlyList<DocumentPoint>> rawCandidates,
        DocumentPoint endStub,
        DocumentPoint end,
        TerminalAnchorDirection startDirection,
        TerminalAnchorDirection endOutwardDirection,
        out int rawCandidateCount,
        ICollection<CandidateMaterializationTrace>? trace = null)
    {
        var candidates = new List<Candidate>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        rawCandidateCount = 0;

        foreach (IReadOnlyList<DocumentPoint> core in rawCandidates)
        {
            int priority = rawCandidateCount++;
            Candidate candidate = CreateCandidate(
                request,
                start,
                startStub,
                core,
                endStub,
                end,
                priority);
            bool terminalStubValid = HasTerminalStubs(
                candidate.Route,
                start,
                startDirection,
                request.Start.MinimumStubLength,
                end,
                endOutwardDirection,
                request.End.MinimumStubLength);
            if (!terminalStubValid)
            {
                trace?.Add(new CandidateMaterializationTrace(
                    core,
                    candidate,
                    terminalStubValid,
                    null,
                    CandidateMaterializationOutcome.StubRejected));
                continue;
            }

            bool backtrackingValid = !request.DisallowBacktracking ||
                !HasBacktracking(candidate.Route);
            if (!backtrackingValid)
            {
                trace?.Add(new CandidateMaterializationTrace(
                    core,
                    candidate,
                    terminalStubValid,
                    backtrackingValid,
                    CandidateMaterializationOutcome.BacktrackingRejected));
                continue;
            }

            if (!keys.Add(candidate.Key))
            {
                trace?.Add(new CandidateMaterializationTrace(
                    core,
                    candidate,
                    terminalStubValid,
                    backtrackingValid,
                    CandidateMaterializationOutcome.DuplicateKeyRejected));
                continue;
            }

            candidates.Add(candidate);
            trace?.Add(new CandidateMaterializationTrace(
                core,
                candidate,
                terminalStubValid,
                backtrackingValid,
                CandidateMaterializationOutcome.Accepted));
        }

        return candidates.ToArray();
    }

    private static Candidate CreateCandidate(
        ConnectionRouteRequest request,
        DocumentPoint start,
        DocumentPoint startStub,
        IReadOnlyList<DocumentPoint> core,
        DocumentPoint endStub,
        DocumentPoint end,
        int priority)
    {
        var points = new List<DocumentPoint> { start, startStub };
        for (int index = 1; index < core.Count - 1; index++)
        {
            points.Add(core[index]);
        }
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
        return new Candidate(route, priority, key, default);
    }

    internal static CandidateScoringContext BuildScoringContext(
        OrthogonalRoute route,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> priorRoutes)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(priorRoutes);

        DocumentPoint source = route.Points[0];
        DocumentPoint target = route.Points[^1];
        var obstacleViews = new ScoringObstacleView[obstacles.Count];
        for (int index = 0; index < obstacles.Count; index++)
        {
            RoutingObstacle obstacle = obstacles[index];
            bool stableOwner = RequiresStableOwnerExclusion(obstacle);
            obstacleViews[index] = new ScoringObstacleView(
                obstacle,
                obstacle.Bounds,
                obstacle.Bounds.XMillimeters + obstacle.Bounds.WidthMillimeters,
                obstacle.Bounds.YMillimeters + obstacle.Bounds.HeightMillimeters,
                stableOwner,
                !stableOwner && obstacle.Contains(source),
                !stableOwner && obstacle.Contains(target));
        }

        var priorRouteViews = new PriorRouteScoringView[priorRoutes.Count];
        for (int index = 0; index < priorRoutes.Count; index++)
        {
            priorRouteViews[index] = new PriorRouteScoringView(
                priorRoutes[index],
                BuildSegmentViews(priorRoutes[index].Segments));
        }

        return new CandidateScoringContext(obstacleViews, priorRouteViews);
    }

    internal static RouteCandidateScore ScoreCandidate(
        OrthogonalRoute route,
        int priority,
        CandidateScoringContext context,
        double? preferredHorizontalY,
        string coordinateKey)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coordinateKey);

        ScoringSegmentView[] candidateSegments = BuildSegmentViews(route.Segments);
        int obstacleIntersections = 0;
        foreach (ScoringSegmentView segment in candidateSegments)
        {
            foreach (ScoringObstacleView obstacle in context.Obstacles)
            {
                if (obstacle.ContainsSourceEndpoint &&
                    obstacle.Obstacle.Contains(segment.Segment.Start) ||
                    obstacle.ContainsTargetEndpoint &&
                    obstacle.Obstacle.Contains(segment.Segment.End))
                {
                    continue;
                }

                if (IntersectsInterior(segment, obstacle))
                {
                    obstacleIntersections++;
                }
            }
        }

        double overlap = 0;
        int crossings = 0;
        foreach (PriorRouteScoringView prior in context.PriorRoutes)
        {
            foreach (ScoringSegmentView current in candidateSegments)
            {
                foreach (ScoringSegmentView existing in prior.Segments)
                {
                    overlap += CollinearOverlap(current, existing);
                    if (HasInteriorCrossing(current, existing))
                    {
                        crossings++;
                    }
                }
            }
        }

        return new RouteCandidateScore(
            obstacleIntersections,
            preferredHorizontalY is double guideY
                ? candidateSegments
                    .Where(segment => segment.IsHorizontal)
                    .Select(segment => Math.Abs(segment.FixedY - guideY))
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

    private static ScoringSegmentView[] BuildSegmentViews(
        IReadOnlyList<OrthogonalRouteSegment> segments)
    {
        var views = new ScoringSegmentView[segments.Count];
        for (int index = 0; index < segments.Count; index++)
        {
            OrthogonalRouteSegment segment = segments[index];
            views[index] = new ScoringSegmentView(
                segment,
                segment.IsHorizontal,
                segment.IsVertical,
                Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters),
                Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters),
                Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters),
                Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters),
                segment.Start.XMillimeters,
                segment.Start.YMillimeters);
        }

        return views;
    }

    private static bool HasInteriorCrossing(
        ScoringSegmentView first,
        ScoringSegmentView second)
    {
        if (first.IsHorizontal == second.IsHorizontal)
        {
            return false;
        }

        ScoringSegmentView horizontal = first.IsHorizontal ? first : second;
        ScoringSegmentView vertical = first.IsVertical ? first : second;
        double x = vertical.FixedX;
        double y = horizontal.FixedY;
        return x > horizontal.MinX &&
               x < horizontal.MaxX &&
               y > vertical.MinY &&
               y < vertical.MaxY;
    }

    private static double CollinearOverlap(
        ScoringSegmentView first,
        ScoringSegmentView second)
    {
        if (first.IsHorizontal && second.IsHorizontal &&
            first.FixedY == second.FixedY)
        {
            return Math.Max(0, Math.Min(first.MaxX, second.MaxX) -
                               Math.Max(first.MinX, second.MinX));
        }

        if (first.IsVertical && second.IsVertical &&
            first.FixedX == second.FixedX)
        {
            return Math.Max(0, Math.Min(first.MaxY, second.MaxY) -
                               Math.Max(first.MinY, second.MinY));
        }

        return 0;
    }

    private static bool IntersectsInterior(
        ScoringSegmentView segment,
        ScoringObstacleView obstacle)
    {
        if (segment.IsHorizontal)
        {
            double y = segment.FixedY;
            return y > obstacle.Bounds.YMillimeters &&
                   y < obstacle.Bottom &&
                   Math.Max(segment.MinX, obstacle.Bounds.XMillimeters) <
                   Math.Min(segment.MaxX, obstacle.Right);
        }

        double x = segment.FixedX;
        return x > obstacle.Bounds.XMillimeters &&
               x < obstacle.Right &&
               Math.Max(segment.MinY, obstacle.Bounds.YMillimeters) <
               Math.Min(segment.MaxY, obstacle.Bottom);
    }

    private static string CoordinateKey(OrthogonalRoute route) => string.Join(
        ";",
        route.Points.Select(point => $"{point.XMillimeters:R},{point.YMillimeters:R}"));

    internal static bool HasInteriorCrossing(
        OrthogonalRouteSegment first,
        OrthogonalRouteSegment second)
    {
        if (first.IsHorizontal == second.IsHorizontal)
        {
            return false;
        }

        OrthogonalRouteSegment horizontal = first.IsHorizontal ? first : second;
        OrthogonalRouteSegment vertical = first.IsVertical ? first : second;
        double x = vertical.Start.XMillimeters;
        double y = horizontal.Start.YMillimeters;
        return x > Math.Min(horizontal.Start.XMillimeters, horizontal.End.XMillimeters) &&
               x < Math.Max(horizontal.Start.XMillimeters, horizontal.End.XMillimeters) &&
               y > Math.Min(vertical.Start.YMillimeters, vertical.End.YMillimeters) &&
               y < Math.Max(vertical.Start.YMillimeters, vertical.End.YMillimeters);
    }

    internal static double CollinearOverlap(
        OrthogonalRouteSegment first,
        OrthogonalRouteSegment second)
    {
        if (first.IsHorizontal && second.IsHorizontal &&
            first.Start.YMillimeters == second.Start.YMillimeters)
        {
            return OverlapLength(
                first.Start.XMillimeters,
                first.End.XMillimeters,
                second.Start.XMillimeters,
                second.End.XMillimeters);
        }

        if (first.IsVertical && second.IsVertical &&
            first.Start.XMillimeters == second.Start.XMillimeters)
        {
            return OverlapLength(
                first.Start.YMillimeters,
                first.End.YMillimeters,
                second.Start.YMillimeters,
                second.End.YMillimeters);
        }

        return 0;
    }

    private static double OverlapLength(double a1, double a2, double b1, double b2)
    {
        return Math.Max(0, Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) -
                           Math.Max(Math.Min(a1, a2), Math.Min(b1, b2)));
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

    private static bool RequiresStableOwnerExclusion(RoutingObstacle obstacle) =>
        obstacle.Kind is RoutingObstacleKind.Transformer or
            RoutingObstacleKind.CustomerStation;

    private static TerminalAnchorDirection ResolveDirection(
        TerminalAnchorDirection direction,
        DocumentPoint from,
        DocumentPoint toward)
    {
        if (direction != TerminalAnchorDirection.Auto)
        {
            return direction;
        }

        double dx = toward.XMillimeters - from.XMillimeters;
        double dy = toward.YMillimeters - from.YMillimeters;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? TerminalAnchorDirection.Right : TerminalAnchorDirection.Left
            : dy >= 0 ? TerminalAnchorDirection.Down : TerminalAnchorDirection.Up;
    }

    private static DocumentPoint Move(
        DocumentPoint point,
        TerminalAnchorDirection direction,
        double distance)
    {
        return direction switch
        {
            TerminalAnchorDirection.Left => new DocumentPoint(point.XMillimeters - distance, point.YMillimeters),
            TerminalAnchorDirection.Right => new DocumentPoint(point.XMillimeters + distance, point.YMillimeters),
            TerminalAnchorDirection.Up => new DocumentPoint(point.XMillimeters, point.YMillimeters - distance),
            TerminalAnchorDirection.Down => new DocumentPoint(point.XMillimeters, point.YMillimeters + distance),
            _ => point
        };
    }

    private static IReadOnlyList<DocumentPoint> NormalizePreview(
        IEnumerable<DocumentPoint> points)
    {
        var values = new List<DocumentPoint>();
        foreach (DocumentPoint point in points)
        {
            if (values.Count == 0 || values[^1] != point)
            {
                values.Add(point);
            }
        }

        var result = new List<DocumentPoint>();
        foreach (DocumentPoint point in values)
        {
            result.Add(point);
            while (result.Count >= 3 &&
                   (result[^3].XMillimeters == result[^2].XMillimeters &&
                    result[^2].XMillimeters == result[^1].XMillimeters ||
                    result[^3].YMillimeters == result[^2].YMillimeters &&
                    result[^2].YMillimeters == result[^1].YMillimeters))
            {
                result.RemoveAt(result.Count - 2);
            }
        }

        return result;
    }

    internal sealed record Candidate(
        OrthogonalRoute Route,
        int Priority,
        string Key,
        RouteCandidateScore Score)
    {
        public RouteFamilyKey? Family { get; init; }
    }
}

internal sealed class CandidateScoringContext(
    ScoringObstacleView[] obstacles,
    PriorRouteScoringView[] priorRoutes)
{
    internal IReadOnlyList<ScoringObstacleView> Obstacles { get; } = obstacles;

    internal IReadOnlyList<PriorRouteScoringView> PriorRoutes { get; } = priorRoutes;
}

internal readonly record struct ScoringObstacleView(
    RoutingObstacle Obstacle,
    DocumentRect Bounds,
    double Right,
    double Bottom,
    bool RequiresStableOwnerExclusion,
    bool ContainsSourceEndpoint,
    bool ContainsTargetEndpoint);

internal readonly record struct PriorRouteScoringView(
    OrthogonalRoute Route,
    ScoringSegmentView[] Segments);

internal readonly record struct ScoringSegmentView(
    OrthogonalRouteSegment Segment,
    bool IsHorizontal,
    bool IsVertical,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    double FixedX,
    double FixedY);

internal enum CandidateMaterializationOutcome
{
    StubRejected,
    BacktrackingRejected,
    DuplicateKeyRejected,
    Accepted
}

internal readonly record struct CandidateMaterializationTrace(
    IReadOnlyList<DocumentPoint> RawCandidate,
    OrthogonalRouter.Candidate Candidate,
    bool TerminalStubValid,
    bool? BacktrackingValid,
    CandidateMaterializationOutcome Outcome);

internal enum CandidateFamilyEvaluationMode
{
    Lazy,
    EagerReference
}

internal readonly record struct VisibilityGraphConstruction(
    double[] XCoordinates,
    double[] YCoordinates,
    DocumentPoint[] Nodes,
    Dictionary<DocumentPoint, List<DocumentPoint>> Adjacency,
    List<List<DocumentPoint>> Columns,
    List<DocumentPoint>?[] Rows,
    List<int> RowEncounterOrder,
    int EdgeCount);

internal sealed class RoutingEvaluationStatistics
{
    public int FamilyClassificationCount { get; private set; }

    public void RecordFamilyClassification() => FamilyClassificationCount++;
}
