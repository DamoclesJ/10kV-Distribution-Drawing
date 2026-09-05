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

        halfEdge = default;
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
