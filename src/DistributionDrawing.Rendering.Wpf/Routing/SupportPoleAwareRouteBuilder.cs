using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public readonly record struct GroundingAccessHalfEdge(
    DocumentPoint PoleCenter,
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
        if (successor && TryFindForwardDirection(route, pole, out DocumentPoint forward))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, forward);
            return true;
        }
        if (predecessor && TryFindBackwardDirection(route, pole, out DocumentPoint backward))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, backward);
            return true;
        }
        bool endpointPole = poleIndex == 0 || poleIndex == line.SupportPoleIds.Count - 1;
        if (endpointPole &&
            layout.Poles.TryGetValue(adjacentPoleId, out PoleLayout? adjacentLayout) &&
            TryFindEndpointDirection(
                route,
                pole,
                PoleProfessionalGeometry.GetPoleCenter(adjacentLayout),
                poleIndex == 0,
                out DocumentPoint endpointDirection))
        {
            halfEdge = new GroundingAccessHalfEdge(pole, endpointDirection);
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
        out DocumentPoint direction)
    {
        OrthogonalRouteSegment segment = useStart ? route.Segments[0] : route.Segments[^1];
        DocumentPoint endpoint = useStart ? segment.Start : segment.End;
        DocumentPoint other = useStart ? segment.End : segment.Start;
        if (adjacent.XMillimeters != pole.XMillimeters &&
            endpoint.YMillimeters == pole.YMillimeters &&
            other.YMillimeters == pole.YMillimeters &&
            (adjacent.XMillimeters > pole.XMillimeters
                ? endpoint.XMillimeters > pole.XMillimeters && other.XMillimeters > endpoint.XMillimeters
                : endpoint.XMillimeters < pole.XMillimeters && other.XMillimeters < endpoint.XMillimeters))
        {
            direction = other;
            return true;
        }
        if (adjacent.YMillimeters != pole.YMillimeters &&
            endpoint.XMillimeters == pole.XMillimeters &&
            other.XMillimeters == pole.XMillimeters &&
            (adjacent.YMillimeters > pole.YMillimeters
                ? endpoint.YMillimeters > pole.YMillimeters && other.YMillimeters > endpoint.YMillimeters
                : endpoint.YMillimeters < pole.YMillimeters && other.YMillimeters < endpoint.YMillimeters))
        {
            direction = other;
            return true;
        }

        direction = default;
        return false;
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
