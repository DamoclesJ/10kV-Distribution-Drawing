using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class OrthogonalRouter
{
    private readonly DrawingMetrics _metrics;

    public OrthogonalRouter(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public OrthogonalRoute Route(
        ConnectionRouteRequest request,
        IEnumerable<RoutingObstacle> obstacles,
        IEnumerable<OrthogonalRoute>? plannedRoutes = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(obstacles);

        RoutingObstacle[] expandedObstacles = obstacles
            .OrderBy(obstacle => obstacle.SourceId)
            .Select(obstacle => obstacle.Expand(_metrics.Routing.ObstacleClearance))
            .ToArray();
        OrthogonalRoute[] priorRoutes = plannedRoutes?
            .OrderBy(route => route.ConnectionId)
            .ToArray() ?? [];

        TerminalAnchorDirection startDirection = ResolveDirection(
            request.Start.Direction,
            request.Start.Position,
            request.End.Position);
        TerminalAnchorDirection endDirection = ResolveDirection(
            request.End.Direction,
            request.End.Position,
            request.Start.Position);
        DocumentPoint startStub = Move(
            request.Start.Position,
            startDirection,
            _metrics.Routing.PortStubLength);
        DocumentPoint endStub = Move(
            request.End.Position,
            endDirection,
            _metrics.Routing.PortStubLength);

        Candidate[] candidates = CreateCandidates(startStub, endStub, expandedObstacles)
            .Select((core, priority) => CreateCandidate(
                request,
                request.Start.Position,
                startStub,
                core,
                endStub,
                request.End.Position,
                priority))
            .Where(candidate => request.Start.Direction == TerminalAnchorDirection.Auto ||
                StartsInDirection(candidate.Route, startDirection))
            .GroupBy(candidate => string.Join(
                ";",
                candidate.Route.Points.Select(point =>
                    $"{point.XMillimeters:R},{point.YMillimeters:R}")),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"No orthogonal route candidates exist for connection '{request.ConnectionId}'.");
        }

        return candidates
            .Select(candidate => candidate with
            {
                Score = Score(
                    candidate.Route,
                    candidate.Priority,
                    expandedObstacles,
                    priorRoutes)
            })
            .OrderBy(candidate => candidate.Score.ObstacleIntersections)
            .ThenBy(candidate => candidate.Score.OverlapLength)
            .ThenBy(candidate => candidate.Score.Crossings)
            .ThenBy(candidate => candidate.Score.Bends)
            .ThenBy(candidate => candidate.Score.Length)
            .ThenBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
            .Select(candidate => candidate.Route)
            .First();
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
            _metrics.Routing.PortStubLength);
        DocumentPoint corner = Math.Abs(end.XMillimeters - stub.XMillimeters) >=
                               Math.Abs(end.YMillimeters - stub.YMillimeters)
            ? new DocumentPoint(end.XMillimeters, stub.YMillimeters)
            : new DocumentPoint(stub.XMillimeters, end.YMillimeters);
        return NormalizePreview([start.Position, stub, corner, end]);
    }

    private IEnumerable<IReadOnlyList<DocumentPoint>> CreateCandidates(
        DocumentPoint start,
        DocumentPoint end,
        IReadOnlyList<RoutingObstacle> obstacles)
    {
        if (start.XMillimeters == end.XMillimeters ||
            start.YMillimeters == end.YMillimeters)
        {
            yield return [start, end];
        }

        yield return [start, new DocumentPoint(end.XMillimeters, start.YMillimeters), end];
        yield return [start, new DocumentPoint(start.XMillimeters, end.YMillimeters), end];

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
        return new Candidate(route, priority, key, default);
    }

    private RouteScore Score(
        OrthogonalRoute route,
        int priority,
        IReadOnlyList<RoutingObstacle> obstacles,
        IReadOnlyList<OrthogonalRoute> priorRoutes)
    {
        int obstacleIntersections = 0;
        foreach (OrthogonalRouteSegment segment in route.Segments)
        {
            foreach (RoutingObstacle obstacle in obstacles)
            {
                bool sourceObstacle = obstacle.Contains(route.Points[0]);
                bool targetObstacle = obstacle.Contains(route.Points[^1]);
                if (sourceObstacle && segment.Index <= 1 ||
                    targetObstacle && segment.Index >= route.Segments.Count - 2)
                {
                    continue;
                }

                if (IntersectsInterior(segment, obstacle.Bounds))
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
                    overlap += CollinearOverlap(current, existing);
                    if (HasInteriorCrossing(current, existing))
                    {
                        crossings++;
                    }
                }
            }
        }

        return new RouteScore(
            obstacleIntersections,
            overlap,
            crossings,
            Math.Max(0, route.Points.Count - 2),
            route.Length,
            priority);
    }

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

    private sealed record Candidate(
        OrthogonalRoute Route,
        int Priority,
        string Key,
        RouteScore Score);

    private readonly record struct RouteScore(
        int ObstacleIntersections,
        double OverlapLength,
        int Crossings,
        int Bends,
        double Length,
        int Priority);
}
