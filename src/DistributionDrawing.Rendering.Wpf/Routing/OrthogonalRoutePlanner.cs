using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class OrthogonalRoutePlanner
{
    private readonly OrthogonalRouter _router;

    public OrthogonalRoutePlanner(OrthogonalRouter? router = null)
    {
        _router = router ?? new OrthogonalRouter();
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
            planned.Add(RouteRequest(request, obstacleArray, planned));
        }

        return planned;
    }

    private OrthogonalRoute RouteRequest(
        ConnectionRouteRequest request,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> planned)
    {
        RequiredRouteWaypoint[] waypoints = request.RequiredWaypoints?.ToArray() ?? [];
        if (waypoints.Length == 0)
        {
            return _router.Route(request, obstacles, planned);
        }

        HashSet<Guid> requiredSourceIds = waypoints.Select(item => item.SourceId).ToHashSet();
        requiredSourceIds.UnionWith(waypoints.SelectMany(item => item.CompositeSourceIds ?? []));
        RoutingObstacle[] routeObstacles = obstacles
            .Where(obstacle => !requiredSourceIds.Contains(obstacle.SourceId))
            .ToArray();
        DocumentPoint[] passagePoints =
            [request.Start.Position, .. waypoints.Select(item => item.Position), request.End.Position];
        passagePoints = passagePoints
            .Where((point, index) => index == 0 || point != passagePoints[index - 1])
            .ToArray();
        var points = new List<DocumentPoint>();
        for (int index = 0; index < passagePoints.Length - 1; index++)
        {
            TerminalAnchor start = index == 0
                ? request.Start with { MinimumStubLength = Math.Max(request.Start.MinimumStubLength,
                    MinimumStub(request.Start.Position, outgoing: true)) }
                : new TerminalAnchor(request.StartTerminalId, passagePoints[index],
                    MinimumStubLength: MinimumStub(passagePoints[index], outgoing: true));
            TerminalAnchor end = index == passagePoints.Length - 2
                ? request.End with { MinimumStubLength = Math.Max(request.End.MinimumStubLength,
                    MinimumStub(request.End.Position, outgoing: false)) }
                : new TerminalAnchor(request.EndTerminalId, passagePoints[index + 1],
                    MinimumStubLength: MinimumStub(passagePoints[index + 1], outgoing: false));
            ValidateCollinearCapacity(start, end);
            var legRequest = request with
            {
                Start = start,
                End = end,
                PreferredHorizontalY = null,
                RequiredWaypoints = null
            };
            OrthogonalRoute leg = _router.Route(legRequest, routeObstacles, planned);
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
            points);

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
            double required = Math.Max(_router.PortStubLength, startAnchor.MinimumStubLength) +
                Math.Max(_router.PortStubLength, endAnchor.MinimumStubLength);
            if (span < required)
            {
                throw new InvalidOperationException("杆间距不足，无法容纳所需导线段和接地环间隙。");
            }
        }
    }
}
