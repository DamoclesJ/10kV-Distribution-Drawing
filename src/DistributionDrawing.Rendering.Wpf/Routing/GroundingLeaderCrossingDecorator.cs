using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

/// <summary>
/// Derives bridge crossings for a presentation-only grounding leader while
/// leaving every electrical route unchanged.
/// </summary>
public sealed class GroundingLeaderCrossingDecorator
{
    private readonly DrawingMetrics _metrics;
    private readonly LineJumpDecorator _lineJumpDecorator;

    public GroundingLeaderCrossingDecorator(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
        _lineJumpDecorator = new LineJumpDecorator(_metrics);
    }

    public IReadOnlyList<SceneElement> Project(
        IReadOnlyList<OrthogonalRouteSegment> leaderSegments,
        IEnumerable<OrthogonalRoute> electricalRoutes,
        Color stroke,
        double thicknessMillimeters)
    {
        ArgumentNullException.ThrowIfNull(leaderSegments);
        ArgumentNullException.ThrowIfNull(electricalRoutes);
        var crossings = new List<(int SegmentIndex, DocumentPoint Position)>();
        foreach (OrthogonalRouteSegment leader in leaderSegments)
        {
            foreach (OrthogonalRouteSegment electrical in electricalRoutes.SelectMany(
                         route => route.Segments))
            {
                if (TryInteriorPerpendicularCrossing(leader, electrical, out DocumentPoint point) &&
                    HasBridgeClearance(leader, point))
                {
                    crossings.Add((leader.Index, point));
                }
            }
        }

        return _lineJumpDecorator.ProjectPresentationSegments(
            leaderSegments,
            crossings.Distinct(),
            stroke,
            thicknessMillimeters);
    }

    private bool TryInteriorPerpendicularCrossing(
        OrthogonalRouteSegment first,
        OrthogonalRouteSegment second,
        out DocumentPoint point)
    {
        if (first.IsHorizontal == second.IsHorizontal)
        {
            point = default;
            return false;
        }
        OrthogonalRouteSegment horizontal = first.IsHorizontal ? first : second;
        OrthogonalRouteSegment vertical = first.IsVertical ? first : second;
        point = new DocumentPoint(
            vertical.Start.XMillimeters,
            horizontal.Start.YMillimeters);
        return StrictlyBetween(
                   point.XMillimeters,
                   horizontal.Start.XMillimeters,
                   horizontal.End.XMillimeters) &&
               StrictlyBetween(
                   point.YMillimeters,
                   vertical.Start.YMillimeters,
                   vertical.End.YMillimeters);
    }

    private bool StrictlyBetween(double value, double first, double second)
    {
        double tolerance = _metrics.Routing.CrossingTolerance;
        return value > Math.Min(first, second) + tolerance &&
               value < Math.Max(first, second) - tolerance;
    }

    private bool HasBridgeClearance(
        OrthogonalRouteSegment segment,
        DocumentPoint point)
    {
        double required = _metrics.LineJump.Radius + _metrics.LineJump.EndpointClearance;
        return Distance(point, segment.Start) >= required &&
               Distance(point, segment.End) >= required;
    }

    private static double Distance(DocumentPoint first, DocumentPoint second)
    {
        double dx = first.XMillimeters - second.XMillimeters;
        double dy = first.YMillimeters - second.YMillimeters;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
