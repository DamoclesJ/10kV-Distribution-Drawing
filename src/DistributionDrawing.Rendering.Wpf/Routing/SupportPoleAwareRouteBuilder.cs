using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public readonly record struct GroundingAccessHalfEdge(
    DocumentPoint PoleCenter,
    DocumentPoint ConductorOrigin,
    DocumentPoint DirectionPoint);

public static class SupportPoleAwareRouteBuilder
{
    public static bool TryResolveHalfEdge(
        OrthogonalRoute route,
        OverheadLine line,
        DrawingLayout layout,
        Guid poleId,
        Guid adjacentPoleId,
        out GroundingAccessHalfEdge halfEdge)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(layout);

        int poleIndex = line.SupportPoleIds.ToList().IndexOf(poleId);
        if (poleIndex < 0 ||
            !layout.Poles.TryGetValue(poleId, out PoleLayout? poleLayout))
        {
            halfEdge = default;
            return false;
        }

        bool predecessor = poleIndex > 0 &&
                           line.SupportPoleIds[poleIndex - 1] == adjacentPoleId;
        bool successor = poleIndex + 1 < line.SupportPoleIds.Count &&
                         line.SupportPoleIds[poleIndex + 1] == adjacentPoleId;
        if (!predecessor && !successor)
        {
            halfEdge = default;
            return false;
        }

        DocumentPoint pole = PoleProfessionalGeometry.GetPoleCenter(poleLayout);
        bool endpointPole = poleIndex == 0 || poleIndex == line.SupportPoleIds.Count - 1;
        if (endpointPole &&
            layout.Poles.TryGetValue(adjacentPoleId, out PoleLayout? adjacentLayout) &&
            TryFindEndpointDirection(
                route,
                pole,
                PoleProfessionalGeometry.GetPoleCenter(adjacentLayout),
                poleIndex == 0,
                out DocumentPoint endpoint,
                out DocumentPoint endpointDirection))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, endpoint, endpointDirection);
            return true;
        }
        if (successor && TryFindForwardDirection(route, pole, out DocumentPoint forward))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, pole, forward);
            return true;
        }
        if (predecessor && TryFindBackwardDirection(route, pole, out DocumentPoint backward))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, pole, backward);
            return true;
        }

        halfEdge = default;
        return false;
    }

    private static bool TryFindEndpointDirection(
        OrthogonalRoute route,
        DocumentPoint pole,
        DocumentPoint adjacent,
        bool useStart,
        out DocumentPoint endpoint,
        out DocumentPoint direction)
    {
        if (route.Segments.Count == 0)
        {
            endpoint = default;
            direction = default;
            return false;
        }
        bool horizontal = Math.Abs(adjacent.XMillimeters - pole.XMillimeters) >=
            Math.Abs(adjacent.YMillimeters - pole.YMillimeters);
        double sign = horizontal
            ? Math.Sign(adjacent.XMillimeters - pole.XMillimeters)
            : Math.Sign(adjacent.YMillimeters - pole.YMillimeters);
        foreach (OrthogonalRouteSegment segment in useStart
                     ? route.Segments
                     : route.Segments.Reverse())
        {
            DocumentPoint from = useStart ? segment.Start : segment.End;
            DocumentPoint to = useStart ? segment.End : segment.Start;
            double delta = horizontal
                ? to.XMillimeters - from.XMillimeters
                : to.YMillimeters - from.YMillimeters;
            if (delta != 0 && Math.Sign(delta) == sign &&
                (horizontal ? segment.IsHorizontal : segment.IsVertical))
            {
                endpoint = from;
                direction = to;
                return true;
            }
        }

        OrthogonalRouteSegment fallback = useStart ? route.Segments[0] : route.Segments[^1];
        endpoint = useStart ? fallback.Start : fallback.End;
        direction = useStart ? fallback.End : fallback.Start;
        return endpoint != direction;
    }

    private static bool TryFindForwardDirection(
        OrthogonalRoute route,
        DocumentPoint pole,
        out DocumentPoint direction)
    {
        foreach (OrthogonalRouteSegment segment in route.Segments)
        {
            if (!Contains(segment, pole) || segment.End == pole)
            {
                continue;
            }
            direction = segment.End;
            return true;
        }
        direction = default;
        return false;
    }

    private static bool TryFindBackwardDirection(
        OrthogonalRoute route,
        DocumentPoint pole,
        out DocumentPoint direction)
    {
        foreach (OrthogonalRouteSegment segment in route.Segments.Reverse())
        {
            if (!Contains(segment, pole) || segment.Start == pole)
            {
                continue;
            }
            direction = segment.Start;
            return true;
        }
        direction = default;
        return false;
    }

    private static bool Contains(OrthogonalRouteSegment segment, DocumentPoint point)
    {
        return segment.IsHorizontal
            ? point.YMillimeters == segment.Start.YMillimeters &&
              point.XMillimeters >= Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) &&
              point.XMillimeters <= Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters)
            : point.XMillimeters == segment.Start.XMillimeters &&
              point.YMillimeters >= Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) &&
              point.YMillimeters <= Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters);
    }
}
