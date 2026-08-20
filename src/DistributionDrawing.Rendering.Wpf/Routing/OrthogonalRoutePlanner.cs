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
            planned.Add(_router.Route(request, obstacleArray, planned));
        }

        return planned;
    }
}
