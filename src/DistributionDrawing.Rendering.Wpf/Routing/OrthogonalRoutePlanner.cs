using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Metrics;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class OrthogonalRoutePlanner
{
    private readonly OrthogonalRouter _router;
    private readonly RouteContinuityContext? _continuity;
    private readonly DrawingMetrics _metrics;

    public OrthogonalRoutePlanner(
        OrthogonalRouter? router = null,
        RouteContinuityContext? continuity = null,
        DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
        _continuity = continuity;
        _router = router ?? new OrthogonalRouter(_metrics, continuity);
    }

    public IReadOnlyList<OrthogonalRoute> Plan(
        IEnumerable<ConnectionRouteRequest> requests,
        IEnumerable<RoutingObstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(obstacles);
        RoutingObstacle[] obstacleArray = obstacles
            .OrderBy(obstacle => obstacle.SourceId)
            .ToArray();
        var planned = new List<OrthogonalRoute>();
        foreach (ConnectionRouteRequest request in requests.OrderBy(request => request.ConnectionId))
        {
            OrthogonalRoute route = RouteRequest(request, obstacleArray, planned);
            if (route.ContinuityFamily is null || route.ContinuityScore is null)
            {
                HashSet<Guid> excluded = request.ExcludedObstacleSourceIds?.ToHashSet() ?? [];
                RoutingObstacle[] activeObstacles = obstacleArray
                    .Where(obstacle => !excluded.Contains(obstacle.SourceId))
                    .Select(obstacle => obstacle.Expand(_metrics.Routing.ObstacleClearance))
                    .ToArray();
                RouteFamilyKey family = RouteFamilyClassifier.Classify(
                    request,
                    route,
                    activeObstacles,
                    _metrics);
                var score = new RouteCandidateScore(
                    0,
                    0,
                    0,
                    0,
                    Math.Max(0, route.Points.Count - 2),
                    route.Length,
                    int.MaxValue,
                    string.Join(";", route.Points.Select(point =>
                        $"{point.XMillimeters:R},{point.YMillimeters:R}")));
                route.ContinuityFamily = family;
                route.ContinuityScore = score;
                _continuity?.Stage(request.ConnectionId, family, score);
            }
            planned.Add(route);
        }

        return planned;
    }

    private OrthogonalRoute RouteRequest(
        ConnectionRouteRequest request,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> planned)
    {
        RequiredRouteWaypoint[] allWaypoints = request.RequiredWaypoints?.ToArray() ?? [];
        if (allWaypoints.Length == 0)
        {
            return _router.Route(request, obstacles, planned);
        }

        HashSet<Guid> requiredSourceIds = allWaypoints.Select(item => item.SourceId).ToHashSet();
        requiredSourceIds.UnionWith(allWaypoints.SelectMany(item => item.CompositeSourceIds ?? []));
        RoutingObstacle[] routeObstacles = obstacles
            .Where(obstacle => !requiredSourceIds.Contains(obstacle.SourceId))
            .ToArray();
        bool substituteStart = allWaypoints.Length > 0 &&
            allWaypoints[0].AllowStartEndpointSubstitution;
        bool substituteEnd = allWaypoints.Length > 0 &&
            allWaypoints[^1].AllowEndEndpointSubstitution;
        if (allWaypoints.Length == 1 && substituteStart && substituteEnd)
        {
            substituteStart = false;
            substituteEnd = false;
        }
        double startTransferredStub = substituteStart
            ? SuccessorMinimumStub(allWaypoints[0])
            : 0;
        double endTransferredStub = substituteEnd
            ? PredecessorMinimumStub(allWaypoints[^1])
            : 0;
        RequiredRouteWaypoint[] waypoints = allWaypoints
            .Skip(substituteStart ? 1 : 0)
            .Take(allWaypoints.Length - (substituteStart ? 1 : 0) - (substituteEnd ? 1 : 0))
            .ToArray();
        TerminalAnchor requestStart = request.Start with
        {
            MinimumStubLength = Math.Max(request.Start.MinimumStubLength, startTransferredStub)
        };
        TerminalAnchor requestEnd = request.End with
        {
            MinimumStubLength = Math.Max(request.End.MinimumStubLength, endTransferredStub)
        };
        if (waypoints.Length == 0)
        {
            return _router.Route(request with
            {
                Start = requestStart,
                End = requestEnd,
                RequiredWaypoints = null,
                EnforceRequiredStubConstraints = startTransferredStub > 0 || endTransferredStub > 0,
                DisallowBacktracking = request.DisallowBacktracking || substituteStart || substituteEnd
            }, routeObstacles, planned);
        }
        DocumentPoint[] passagePoints =
            [requestStart.Position, .. waypoints.Select(item => item.Position), requestEnd.Position];
        passagePoints = passagePoints
            .Where((point, index) => index == 0 || point != passagePoints[index - 1])
            .ToArray();
        var points = new List<DocumentPoint>();
        for (int index = 0; index < passagePoints.Length - 1; index++)
        {
            TerminalAnchor start = index == 0
                ? requestStart with { MinimumStubLength = Math.Max(requestStart.MinimumStubLength,
                    MinimumStub(requestStart.Position, outgoing: true)) }
                : new TerminalAnchor(request.StartTerminalId, passagePoints[index],
                    MinimumStubLength: MinimumStub(passagePoints[index], outgoing: true));
            TerminalAnchor end = index == passagePoints.Length - 2
                ? requestEnd with { MinimumStubLength = Math.Max(requestEnd.MinimumStubLength,
                    MinimumStub(requestEnd.Position, outgoing: false)) }
                : new TerminalAnchor(request.EndTerminalId, passagePoints[index + 1],
                    MinimumStubLength: MinimumStub(passagePoints[index + 1], outgoing: false));
            ValidateCollinearCapacity(start, end);
            var legRequest = request with
            {
                Start = start,
                End = end,
                PreferredHorizontalY = null,
                RequiredWaypoints = null,
                EnforceRequiredStubConstraints =
                    index == 0 && startTransferredStub > 0 ||
                    index == passagePoints.Length - 2 && endTransferredStub > 0 ||
                    MinimumStub(passagePoints[index], outgoing: true) > 0 ||
                    MinimumStub(passagePoints[index + 1], outgoing: false) > 0,
                DisallowBacktracking = request.DisallowBacktracking ||
                    (index == 0 && substituteStart) ||
                    (index == passagePoints.Length - 2 && substituteEnd)
            };
            OrthogonalRoute leg = _router.RouteWithoutContinuity(legRequest, routeObstacles, planned);
            points.AddRange(index == 0 ? leg.Points : leg.Points.Skip(1));
        }

        double MinimumStub(DocumentPoint point, bool outgoing) => waypoints
            .Where(item => item.Position == point)
            .Select(item => outgoing
                ? (item.SuccessorMinimumStubLength > 0
                    ? item.SuccessorMinimumStubLength : item.MinimumStubLength)
                : (item.PredecessorMinimumStubLength > 0
                    ? item.PredecessorMinimumStubLength : item.MinimumStubLength))
            .DefaultIfEmpty(0).Max();

        return new OrthogonalRoute(
            request.ConnectionId,
            request.ConnectionType,
            request.StartTerminalId,
            request.EndTerminalId,
            points,
            waypoints.Select(item => item.Position));

        void ValidateCollinearCapacity(TerminalAnchor startAnchor, TerminalAnchor endAnchor)
        {
            if (startAnchor.Position.XMillimeters != endAnchor.Position.XMillimeters &&
                startAnchor.Position.YMillimeters != endAnchor.Position.YMillimeters)
            {
                return;
            }
            double span = startAnchor.Position.XMillimeters == endAnchor.Position.XMillimeters
                ? Math.Abs(startAnchor.Position.YMillimeters - endAnchor.Position.YMillimeters)
                : Math.Abs(startAnchor.Position.XMillimeters - endAnchor.Position.XMillimeters);
            double required = startAnchor.MinimumStubLength + endAnchor.MinimumStubLength;
            if (span < required)
            {
                throw new RoutingConstraintException(
                    "杆间距不足，无法容纳所需导线段和接地环间隙。");
            }
        }

        static double PredecessorMinimumStub(RequiredRouteWaypoint waypoint) =>
            waypoint.PredecessorMinimumStubLength > 0
                ? waypoint.PredecessorMinimumStubLength
                : waypoint.MinimumStubLength;

        static double SuccessorMinimumStub(RequiredRouteWaypoint waypoint) =>
            waypoint.SuccessorMinimumStubLength > 0
                ? waypoint.SuccessorMinimumStubLength
                : waypoint.MinimumStubLength;

    }
}
