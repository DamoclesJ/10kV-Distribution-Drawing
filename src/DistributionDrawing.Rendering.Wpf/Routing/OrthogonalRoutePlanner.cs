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
                ? request.Start
                : new TerminalAnchor(request.StartTerminalId, passagePoints[index]);
            TerminalAnchor end = index == passagePoints.Length - 2
                ? request.End
                : new TerminalAnchor(request.EndTerminalId, passagePoints[index + 1]);
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

        return new OrthogonalRoute(
            request.ConnectionId,
            request.ConnectionType,
            request.StartTerminalId,
            request.EndTerminalId,
            points);
    }
}
