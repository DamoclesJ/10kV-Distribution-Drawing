using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

internal enum RouteTopologyKind
{
    Direct,
    SimpleElbow,
    MultiElbow
}

internal readonly record struct RouteFamilyKey(
    RouteTopologyKind Topology,
    string EndpointApproaches,
    string SegmentOrientations,
    string RequiredWaypointPasses,
    string ObstaclePasses,
    string ChannelFamilies);

internal readonly record struct RouteCandidateScore(
    int ObstacleIntersections,
    double HorizontalGuideDeviation,
    double OverlapLength,
    int Crossings,
    int Bends,
    double Length,
    int Priority,
    string CoordinateKey);

internal readonly record struct RouteContinuityPreference(
    RouteFamilyKey Family,
    RouteCandidateScore Score);

/// <summary>
/// Holds route-family preferences for one active drag transaction. This state is
/// deliberately transient and is published only after the complete scene build
/// has been accepted by the drag transaction coordinator.
/// </summary>
public sealed class RouteContinuityContext
{
    private Dictionary<Guid, RouteContinuityPreference> _accepted = [];
    private Dictionary<Guid, RouteContinuityPreference> _provisional = [];

    public bool IsActive { get; private set; }

    public int AcceptedEntryCount => _accepted.Count;

    public int ProvisionalEntryCount => _provisional.Count;

    public void BeginGesture(IEnumerable<OrthogonalRoute> currentRoutes)
    {
        ArgumentNullException.ThrowIfNull(currentRoutes);
        if (IsActive)
        {
            throw new InvalidOperationException("A route-continuity gesture is already active.");
        }

        _accepted = currentRoutes
            .Where(route => route.ContinuityFamily is not null && route.ContinuityScore is not null)
            .ToDictionary(
                route => route.ConnectionId,
                route => new RouteContinuityPreference(
                    route.ContinuityFamily!.Value,
                    route.ContinuityScore!.Value));
        _provisional = [];
        IsActive = true;
    }

    public void BeginProvisionalBuild()
    {
        if (IsActive)
        {
            _provisional = [];
        }
    }

    public void AcceptProvisional()
    {
        if (!IsActive)
        {
            return;
        }

        _accepted = new Dictionary<Guid, RouteContinuityPreference>(_provisional);
        _provisional = [];
    }

    public void DiscardProvisional()
    {
        _provisional = [];
    }

    public void EndGesture()
    {
        IsActive = false;
        _accepted = [];
        _provisional = [];
    }

    internal RouteContinuityPreference? GetAccepted(Guid connectionId) =>
        IsActive && _accepted.TryGetValue(connectionId, out RouteContinuityPreference value)
            ? value
            : null;

    internal RouteContinuityPreference? GetProvisional(Guid connectionId) =>
        IsActive && _provisional.TryGetValue(connectionId, out RouteContinuityPreference value)
            ? value
            : null;

    internal void Stage(
        Guid connectionId,
        RouteFamilyKey family,
        RouteCandidateScore score)
    {
        if (IsActive)
        {
            _provisional[connectionId] = new RouteContinuityPreference(family, score);
        }
    }
}

internal static class RouteFamilyClassifier
{
    private const double CoordinateTolerance = 1e-9;

    public static RouteFamilyKey Classify(
        ConnectionRouteRequest request,
        OrthogonalRoute route,
        IReadOnlyList<RoutingObstacle> obstacles,
        DrawingMetrics metrics) => ClassifyCore(
            request,
            route,
            obstacles.OrderBy(obstacle => obstacle.SourceId).ToArray(),
            metrics,
            null);

    internal static RouteFamilyKey ClassifySorted(
        ConnectionRouteRequest request,
        OrthogonalRoute route,
        IReadOnlyList<RoutingObstacle> obstacles,
        DrawingMetrics metrics,
        IReadOnlyDictionary<Guid, string>? sourceIdText) => ClassifyCore(
            request,
            route,
            obstacles,
            metrics,
            sourceIdText);

    private static RouteFamilyKey ClassifyCore(
        ConnectionRouteRequest request,
        OrthogonalRoute route,
        IReadOnlyList<RoutingObstacle> obstacles,
        DrawingMetrics metrics,
        IReadOnlyDictionary<Guid, string>? sourceIdText)
    {
        RouteTopologyKind topology = route.Segments.Count switch
        {
            1 => RouteTopologyKind.Direct,
            2 => RouteTopologyKind.SimpleElbow,
            _ => RouteTopologyKind.MultiElbow
        };
        string orientations = string.Concat(route.Segments.Select(segment =>
            segment.IsHorizontal ? 'H' : 'V'));
        string endpointApproaches = route.Segments.Count == 0
            ? string.Empty
            : $"{Direction(route.Segments[0])}>{Direction(route.Segments[^1])}";
        string waypointPasses = string.Join(",", (request.RequiredWaypoints ?? [])
            .Select(waypoint => WaypointPass(route, waypoint)));
        var obstacleClassifications = obstacles
            .Select(obstacle => (
                Obstacle: obstacle,
                SourceIdText: sourceIdText is not null &&
                    sourceIdText.TryGetValue(obstacle.SourceId, out string? formatted)
                    ? formatted
                    : obstacle.SourceId.ToString("N"),
                Pass: ObstaclePass(route, obstacle)))
            .ToArray();
        string obstaclePasses = string.Join(",", obstacleClassifications.Select(item =>
            $"{item.SourceIdText}:{item.Pass.Side}"));
        string channels = string.Join(",", obstacleClassifications
            .Where(item => item.Pass.Segment is not null)
            .Select(item =>
                $"{item.SourceIdText}:" + ChannelFamily(
                    request,
                    item.Pass.Segment!.Value,
                    item.Obstacle,
                    metrics)));
        return new RouteFamilyKey(
            topology,
            endpointApproaches,
            orientations,
            waypointPasses,
            obstaclePasses,
            channels);
    }

    private static string WaypointPass(
        OrthogonalRoute route,
        RequiredRouteWaypoint waypoint)
    {
        int index = Enumerable.Range(0, route.Points.Count)
            .FirstOrDefault(index => route.Points[index] == waypoint.Position, -1);
        if (index < 0)
        {
            return $"{waypoint.SourceId:N}:missing";
        }

        string incoming = index == 0 ? "S" : Direction(route.Segments[index - 1]);
        string outgoing = index >= route.Segments.Count ? "E" : Direction(route.Segments[index]);
        return $"{waypoint.SourceId:N}:{incoming}>{outgoing}";
    }

    private static ObstaclePassClassification ObstaclePass(
        OrthogonalRoute route,
        RoutingObstacle obstacle)
    {
        DocumentRect bounds = obstacle.Bounds;
        var passes = new List<(
            double Distance,
            double Coverage,
            string Side,
            OrthogonalRouteSegment Segment)>();
        foreach (OrthogonalRouteSegment segment in route.Segments)
        {
            if (segment.IsHorizontal && Overlaps(
                    segment.Start.XMillimeters,
                    segment.End.XMillimeters,
                    bounds.XMillimeters,
                    bounds.XMillimeters + bounds.WidthMillimeters))
            {
                double y = segment.Start.YMillimeters;
                if (y <= bounds.YMillimeters)
                {
                    passes.Add((
                        bounds.YMillimeters - y,
                        OverlapLength(
                            segment.Start.XMillimeters,
                            segment.End.XMillimeters,
                            bounds.XMillimeters,
                            bounds.XMillimeters + bounds.WidthMillimeters),
                        "U",
                        segment));
                }
                else if (y >= bounds.YMillimeters + bounds.HeightMillimeters)
                {
                    passes.Add((
                        y - bounds.YMillimeters - bounds.HeightMillimeters,
                        OverlapLength(
                            segment.Start.XMillimeters,
                            segment.End.XMillimeters,
                            bounds.XMillimeters,
                            bounds.XMillimeters + bounds.WidthMillimeters),
                        "D",
                        segment));
                }
            }
            else if (segment.IsVertical && Overlaps(
                         segment.Start.YMillimeters,
                         segment.End.YMillimeters,
                         bounds.YMillimeters,
                         bounds.YMillimeters + bounds.HeightMillimeters))
            {
                double x = segment.Start.XMillimeters;
                if (x <= bounds.XMillimeters)
                {
                    passes.Add((
                        bounds.XMillimeters - x,
                        OverlapLength(
                            segment.Start.YMillimeters,
                            segment.End.YMillimeters,
                            bounds.YMillimeters,
                            bounds.YMillimeters + bounds.HeightMillimeters),
                        "L",
                        segment));
                }
                else if (x >= bounds.XMillimeters + bounds.WidthMillimeters)
                {
                    passes.Add((
                        x - bounds.XMillimeters - bounds.WidthMillimeters,
                        OverlapLength(
                            segment.Start.YMillimeters,
                            segment.End.YMillimeters,
                            bounds.YMillimeters,
                            bounds.YMillimeters + bounds.HeightMillimeters),
                        "R",
                        segment));
                }
            }
        }

        if (passes.Count == 0)
        {
            return new ObstaclePassClassification("N", null);
        }

        var selected = passes
            .OrderBy(item => item.Distance)
            .ThenByDescending(item => item.Coverage)
            .ThenBy(item => item.Side, StringComparer.Ordinal)
            .FirstOrDefault();
        return new ObstaclePassClassification(selected.Side, selected.Segment);
    }

    private static string ChannelFamily(
        ConnectionRouteRequest request,
        OrthogonalRouteSegment segment,
        RoutingObstacle obstacle,
        DrawingMetrics metrics)
    {
        double coordinate = segment.IsHorizontal
            ? segment.Start.YMillimeters
            : segment.Start.XMillimeters;
        if (segment.IsHorizontal && request.PreferredHorizontalY is double guide &&
            Equal(coordinate, guide))
        {
            return "H:guide";
        }

        double minimum = segment.IsHorizontal
            ? obstacle.Bounds.YMillimeters
            : obstacle.Bounds.XMillimeters;
        double maximum = minimum + (segment.IsHorizontal
            ? obstacle.Bounds.HeightMillimeters
            : obstacle.Bounds.WidthMillimeters);
        if (Equal(coordinate, minimum))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:boundary:min";
        }
        if (Equal(coordinate, maximum))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:boundary:max";
        }
        if (Equal(coordinate, minimum - metrics.Routing.ParallelSpacing))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:parallel:min";
        }
        if (Equal(coordinate, maximum + metrics.Routing.ParallelSpacing))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:parallel:max";
        }

        double start = segment.IsHorizontal
            ? request.Start.Position.YMillimeters
            : request.Start.Position.XMillimeters;
        double end = segment.IsHorizontal
            ? request.End.Position.YMillimeters
            : request.End.Position.XMillimeters;
        if (Equal(coordinate, start - metrics.Routing.MinimumDoglegLength) ||
            Equal(coordinate, start + metrics.Routing.MinimumDoglegLength))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:start-dogleg";
        }
        if (Equal(coordinate, end - metrics.Routing.MinimumDoglegLength) ||
            Equal(coordinate, end + metrics.Routing.MinimumDoglegLength))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:end-dogleg";
        }
        if (Equal(coordinate, (start + end) / 2))
        {
            return $"{(segment.IsHorizontal ? 'H' : 'V')}:midpoint";
        }

        return $"{(segment.IsHorizontal ? 'H' : 'V')}:free";
    }

    private static string Direction(OrthogonalRouteSegment segment)
    {
        if (segment.IsHorizontal)
        {
            return segment.End.XMillimeters >= segment.Start.XMillimeters ? "R" : "L";
        }
        return segment.End.YMillimeters >= segment.Start.YMillimeters ? "D" : "U";
    }

    private static bool Overlaps(double firstStart, double firstEnd, double secondStart, double secondEnd) =>
        Math.Max(Math.Min(firstStart, firstEnd), Math.Min(secondStart, secondEnd)) <=
        Math.Min(Math.Max(firstStart, firstEnd), Math.Max(secondStart, secondEnd));

    private static double OverlapLength(
        double firstStart,
        double firstEnd,
        double secondStart,
        double secondEnd) =>
        Math.Max(0,
            Math.Min(Math.Max(firstStart, firstEnd), Math.Max(secondStart, secondEnd)) -
            Math.Max(Math.Min(firstStart, firstEnd), Math.Min(secondStart, secondEnd)));

    private static bool Equal(double first, double second) =>
        Math.Abs(first - second) <= CoordinateTolerance;

    private readonly record struct ObstaclePassClassification(
        string Side,
        OrthogonalRouteSegment? Segment);
}
