using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public enum RouteIntersectionKind
{
    PerpendicularInterior,
    EndpointTouch,
    CollinearOverlap
}

public sealed record RouteIntersection(
    Guid FirstConnectionId,
    int FirstSegmentIndex,
    Guid SecondConnectionId,
    int SecondSegmentIndex,
    RouteIntersectionKind Kind,
    DocumentPoint? Position,
    bool SharesTopologyTerminal);

public sealed class RouteCrossingDetector
{
    private readonly double _tolerance;

    public RouteCrossingDetector(DrawingMetrics? metrics = null)
    {
        _tolerance = (metrics ?? DrawingMetrics.Default).Routing.CrossingTolerance;
    }

    public IReadOnlyList<RouteIntersection> Detect(IEnumerable<OrthogonalRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        OrthogonalRoute[] values = routes.OrderBy(route => route.ConnectionId).ToArray();
        var intersections = new List<RouteIntersection>();
        for (var firstIndex = 0; firstIndex < values.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < values.Length; secondIndex++)
            {
                DetectPair(values[firstIndex], values[secondIndex], intersections);
            }
        }

        return intersections;
    }

    private void DetectPair(
        OrthogonalRoute first,
        OrthogonalRoute second,
        ICollection<RouteIntersection> output)
    {
        bool sharesTerminal = first.SharesTerminalWith(second);
        foreach (OrthogonalRouteSegment firstSegment in first.Segments)
        {
            foreach (OrthogonalRouteSegment secondSegment in second.Segments)
            {
                if (TryPerpendicularIntersection(
                        firstSegment,
                        secondSegment,
                        out DocumentPoint position,
                        out bool firstEndpoint,
                        out bool secondEndpoint))
                {
                    output.Add(new RouteIntersection(
                        first.ConnectionId,
                        firstSegment.Index,
                        second.ConnectionId,
                        secondSegment.Index,
                        firstEndpoint || secondEndpoint
                            ? RouteIntersectionKind.EndpointTouch
                            : RouteIntersectionKind.PerpendicularInterior,
                        position,
                        sharesTerminal));
                    continue;
                }

                if (OrthogonalRouter.CollinearOverlap(firstSegment, secondSegment) > _tolerance)
                {
                    output.Add(new RouteIntersection(
                        first.ConnectionId,
                        firstSegment.Index,
                        second.ConnectionId,
                        secondSegment.Index,
                        RouteIntersectionKind.CollinearOverlap,
                        null,
                        sharesTerminal));
                }
            }
        }
    }

    private bool TryPerpendicularIntersection(
        OrthogonalRouteSegment first,
        OrthogonalRouteSegment second,
        out DocumentPoint position,
        out bool firstEndpoint,
        out bool secondEndpoint)
    {
        if (first.IsHorizontal == second.IsHorizontal)
        {
            position = default;
            firstEndpoint = false;
            secondEndpoint = false;
            return false;
        }

        OrthogonalRouteSegment horizontal = first.IsHorizontal ? first : second;
        OrthogonalRouteSegment vertical = first.IsVertical ? first : second;
        double x = vertical.Start.XMillimeters;
        double y = horizontal.Start.YMillimeters;
        if (!Between(x, horizontal.Start.XMillimeters, horizontal.End.XMillimeters) ||
            !Between(y, vertical.Start.YMillimeters, vertical.End.YMillimeters))
        {
            position = default;
            firstEndpoint = false;
            secondEndpoint = false;
            return false;
        }

        position = new DocumentPoint(x, y);
        firstEndpoint = IsEndpoint(position, first);
        secondEndpoint = IsEndpoint(position, second);
        return true;
    }

    private bool Between(double value, double first, double second)
    {
        return value >= Math.Min(first, second) - _tolerance &&
               value <= Math.Max(first, second) + _tolerance;
    }

    private bool IsEndpoint(DocumentPoint point, OrthogonalRouteSegment segment)
    {
        return Distance(point, segment.Start) <= _tolerance ||
               Distance(point, segment.End) <= _tolerance;
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double dx = first.XMillimeters - second.XMillimeters;
        double dy = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
