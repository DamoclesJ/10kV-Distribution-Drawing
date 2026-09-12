using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public readonly record struct GroundingAccessHalfEdge(
    DocumentPoint PoleCenter,
    DocumentPoint ConductorOrigin,
    DocumentPoint DirectionPoint,
    IReadOnlyList<DocumentPoint> OrientedPath);

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
        return TryResolveHalfEdge(
            route,
            line,
            layout,
            poleId,
            GroundingAdjacentEndpoint.ForPole(adjacentPoleId),
            null,
            GroundingAccessPlacementSide.PoleSide,
            out halfEdge);
    }

    public static bool TryResolveHalfEdge(
        OrthogonalRoute route,
        OverheadLine line,
        DrawingLayout layout,
        Guid poleId,
        GroundingAdjacentEndpoint adjacentEndpoint,
        Connection? connection,
        out GroundingAccessHalfEdge halfEdge) => TryResolveHalfEdge(
            route,
            line,
            layout,
            poleId,
            adjacentEndpoint,
            connection,
            GroundingAccessPlacementSide.PoleSide,
            out halfEdge);

    public static bool TryResolveHalfEdge(
        OrthogonalRoute route,
        OverheadLine line,
        DrawingLayout layout,
        Guid poleId,
        GroundingAdjacentEndpoint adjacentEndpoint,
        Connection? connection,
        GroundingAccessPlacementSide placementSide,
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

        if (adjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Terminal)
        {
            if (connection is null || connection.Id != line.ConnectionId ||
                line.SupportPoleIds.Count != 1 ||
                !connection.UsesTerminal(adjacentEndpoint.TargetId) ||
                route.Segments.Count == 0)
            {
                halfEdge = default;
                return false;
            }

            DocumentPoint terminalEndpointPole = PoleProfessionalGeometry.GetPoleCenter(poleLayout);
            bool terminalIsStart = connection.StartTerminalId == adjacentEndpoint.TargetId;
            bool fromStart = placementSide == GroundingAccessPlacementSide.AdjacentEndpointSide
                ? terminalIsStart
                : !terminalIsStart;
            DocumentPoint[] path = fromStart
                ? route.Points.ToArray()
                : route.Points.Reverse().ToArray();
            DocumentPoint origin = path[0];
            DocumentPoint direction = path[1];
            if (origin == direction)
            {
                halfEdge = default;
                return false;
            }
            halfEdge = new GroundingAccessHalfEdge(
                terminalEndpointPole,
                origin,
                direction,
                Array.AsReadOnly(path));
            return true;
        }

        Guid adjacentPoleId = adjacentEndpoint.TargetId;
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
            halfEdge = CreateHalfEdge(
                route,
                pole,
                endpoint,
                endpointDirection,
                PoleProfessionalGeometry.GetPoleCenter(adjacentLayout));
            return true;
        }
        DocumentPoint? adjacentCenter = layout.Poles.TryGetValue(
            adjacentPoleId,
            out PoleLayout? adjacentPoleLayout)
            ? PoleProfessionalGeometry.GetPoleCenter(adjacentPoleLayout)
            : null;
        if (successor && TryFindForwardDirection(route, pole, out DocumentPoint forward))
        {
            halfEdge = CreateHalfEdge(route, pole, pole, forward, adjacentCenter);
            return true;
        }
        if (predecessor && TryFindBackwardDirection(route, pole, out DocumentPoint backward))
        {
            halfEdge = CreateHalfEdge(route, pole, pole, backward, adjacentCenter);
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

    private static GroundingAccessHalfEdge CreateHalfEdge(
        OrthogonalRoute route,
        DocumentPoint pole,
        DocumentPoint origin,
        DocumentPoint direction,
        DocumentPoint? stop = null)
    {
        for (var index = 0; index < route.Segments.Count; index++)
        {
            OrthogonalRouteSegment segment = route.Segments[index];
            if (!Contains(segment, origin) || !Contains(segment, direction))
            {
                continue;
            }
            bool forward = direction == segment.End || direction != segment.Start;
            var points = new List<DocumentPoint> { origin };
            if (forward)
            {
                points.Add(segment.End);
                points.AddRange(route.Points.Skip(index + 2));
            }
            else
            {
                points.Add(segment.Start);
                points.AddRange(route.Points.Take(index).Reverse());
            }
            DocumentPoint[] normalized = TrimAt(
                points.DistinctConsecutive().ToArray(),
                stop);
            return new GroundingAccessHalfEdge(
                pole,
                origin,
                direction,
                Array.AsReadOnly(normalized));
        }

        return new GroundingAccessHalfEdge(
            pole,
            origin,
            direction,
            Array.AsReadOnly(new[] { origin, direction }));
    }

    private static DocumentPoint[] TrimAt(
        IReadOnlyList<DocumentPoint> path,
        DocumentPoint? stop)
    {
        if (stop is not DocumentPoint stopPoint)
        {
            return path.ToArray();
        }
        var result = new List<DocumentPoint> { path[0] };
        for (var index = 0; index + 1 < path.Count; index++)
        {
            var segment = new OrthogonalRouteSegment(path[index], path[index + 1], index);
            if (Contains(segment, stopPoint))
            {
                if (result[^1] != stopPoint)
                {
                    result.Add(stopPoint);
                }
                return result.ToArray();
            }
            result.Add(path[index + 1]);
        }
        return path.ToArray();
    }

    private static IEnumerable<DocumentPoint> DistinctConsecutive(
        this IEnumerable<DocumentPoint> points)
    {
        DocumentPoint? previous = null;
        foreach (DocumentPoint point in points)
        {
            if (previous is null || previous.Value != point)
            {
                yield return point;
                previous = point;
            }
        }
    }
}
