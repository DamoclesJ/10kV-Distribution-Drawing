using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class LineJumpDecorator
{
    private readonly DrawingMetrics _metrics;

    public LineJumpDecorator(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public IReadOnlyList<SceneElement> Project(
        OrthogonalRoute route,
        IEnumerable<RouteIntersection> intersections,
        Color stroke,
        SceneStrokeStyle strokeStyle,
        double? thicknessMillimeters = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(intersections);
        double thickness = thicknessMillimeters ?? _metrics.Line.ConnectionThickness;
        var elements = new List<SceneElement>();
        foreach (OrthogonalRouteSegment segment in route.Segments)
        {
            DocumentPoint[] jumps = SelectSafeJumps(segment, intersections
                .Where(intersection => ShouldJump(route, segment, intersection))
                .Select(intersection => intersection.Position!.Value)
                .OrderBy(point => DistanceAlong(segment, point)))
                .ToArray();
            ProjectSegment(segment, jumps, stroke, strokeStyle, thickness, elements);
        }

        return elements;
    }

    private bool ShouldJump(
        OrthogonalRoute route,
        OrthogonalRouteSegment segment,
        RouteIntersection intersection)
    {
        if (intersection.Kind != RouteIntersectionKind.PerpendicularInterior ||
            intersection.SharesTopologyTerminal ||
            intersection.Position is not DocumentPoint position)
        {
            return false;
        }

        Guid otherId;
        int routeSegmentIndex;
        if (intersection.FirstConnectionId == route.ConnectionId)
        {
            otherId = intersection.SecondConnectionId;
            routeSegmentIndex = intersection.FirstSegmentIndex;
        }
        else if (intersection.SecondConnectionId == route.ConnectionId)
        {
            otherId = intersection.FirstConnectionId;
            routeSegmentIndex = intersection.SecondSegmentIndex;
        }
        else
        {
            return false;
        }

        if (route.ConnectionId.CompareTo(otherId) <= 0 || routeSegmentIndex != segment.Index)
        {
            return false;
        }

        double required = _metrics.LineJump.Radius + _metrics.LineJump.EndpointClearance;
        return Distance(position, segment.Start) >= required &&
               Distance(position, segment.End) >= required;
    }

    private IEnumerable<DocumentPoint> SelectSafeJumps(
        OrthogonalRouteSegment segment,
        IEnumerable<DocumentPoint> candidates)
    {
        double minimumSpacing = _metrics.LineJump.Radius * 2 +
                                _metrics.LineJump.EndpointClearance;
        double? previousDistance = null;
        foreach (DocumentPoint candidate in candidates)
        {
            double distance = DistanceAlong(segment, candidate);
            if (previousDistance is not null &&
                distance - previousDistance.Value < minimumSpacing)
            {
                continue;
            }

            previousDistance = distance;
            yield return candidate;
        }
    }

    private void ProjectSegment(
        OrthogonalRouteSegment segment,
        IReadOnlyList<DocumentPoint> jumps,
        Color stroke,
        SceneStrokeStyle strokeStyle,
        double thickness,
        ICollection<SceneElement> output)
    {
        if (jumps.Count == 0)
        {
            output.Add(new SceneLine(segment.Start, segment.End, stroke, thickness, strokeStyle));
            return;
        }

        double direction = segment.IsHorizontal
            ? Math.Sign(segment.End.XMillimeters - segment.Start.XMillimeters)
            : Math.Sign(segment.End.YMillimeters - segment.Start.YMillimeters);
        DocumentPoint cursor = segment.Start;
        foreach (DocumentPoint crossing in jumps)
        {
            DocumentPoint before = segment.IsHorizontal
                ? new DocumentPoint(
                    crossing.XMillimeters - direction * _metrics.LineJump.Radius,
                    crossing.YMillimeters)
                : new DocumentPoint(
                    crossing.XMillimeters,
                    crossing.YMillimeters - direction * _metrics.LineJump.Radius);
            DocumentPoint after = segment.IsHorizontal
                ? new DocumentPoint(
                    crossing.XMillimeters + direction * _metrics.LineJump.Radius,
                    crossing.YMillimeters)
                : new DocumentPoint(
                    crossing.XMillimeters,
                    crossing.YMillimeters + direction * _metrics.LineJump.Radius);
            if (cursor != before)
            {
                output.Add(new SceneLine(cursor, before, stroke, thickness, strokeStyle));
            }

            double startAngle = segment.IsHorizontal
                ? direction > 0 ? 180 : 0
                : direction > 0 ? 270 : 90;
            double sweep = direction > 0 ? 180 : -180;
            output.Add(new SceneArc(
                crossing,
                _metrics.LineJump.Radius,
                startAngle,
                sweep,
                stroke,
                thickness,
                strokeStyle));
            cursor = after;
        }

        if (cursor != segment.End)
        {
            output.Add(new SceneLine(cursor, segment.End, stroke, thickness, strokeStyle));
        }
    }

    private static double DistanceAlong(
        OrthogonalRouteSegment segment,
        DocumentPoint point)
    {
        return Math.Abs(point.XMillimeters - segment.Start.XMillimeters) +
               Math.Abs(point.YMillimeters - segment.Start.YMillimeters);
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double dx = first.XMillimeters - second.XMillimeters;
        double dy = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
