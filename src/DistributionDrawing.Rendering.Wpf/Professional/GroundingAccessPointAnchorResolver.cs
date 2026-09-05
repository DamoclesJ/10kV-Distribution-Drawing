using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed class GroundingAccessPointAnchorResolver
{
    private readonly DrawingMetrics _metrics;

    public GroundingAccessPointAnchorResolver(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public bool TryResolve(
        GroundingAccessPoint point,
        DrawingDocument document,
        DrawingLayout layout,
        IReadOnlyDictionary<Guid, OrthogonalRoute> routes,
        out GroundingPresentationAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(routes);

        OverheadLine? line = document.OverheadLines.SingleOrDefault(candidate =>
            candidate.ConnectionId == point.ConnectionId);
        if (line is null ||
            !routes.TryGetValue(point.ConnectionId, out OrthogonalRoute? route) ||
            !SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                layout,
                point.PoleId,
                point.AdjacentPoleId,
                out GroundingAccessHalfEdge halfEdge))
        {
            anchor = default;
            return false;
        }

        TerminalAnchorDirection direction = ResolveDirection(
            halfEdge.PoleCenter,
            halfEdge.DirectionPoint);
        double distance = _metrics.Pole.PoleRadius + _metrics.Line.GroundingAccessClearance;
        anchor = new GroundingPresentationAnchor(
            Move(halfEdge.PoleCenter, direction, distance),
            direction);
        return true;
    }

    private static TerminalAnchorDirection ResolveDirection(DocumentPoint from, DocumentPoint to)
    {
        if (from.XMillimeters != to.XMillimeters)
        {
            return to.XMillimeters > from.XMillimeters
                ? TerminalAnchorDirection.Right
                : TerminalAnchorDirection.Left;
        }
        return to.YMillimeters > from.YMillimeters
            ? TerminalAnchorDirection.Down
            : TerminalAnchorDirection.Up;
    }

    private static DocumentPoint Move(
        DocumentPoint point,
        TerminalAnchorDirection direction,
        double distance) => direction switch
        {
            TerminalAnchorDirection.Left => new(point.XMillimeters - distance, point.YMillimeters),
            TerminalAnchorDirection.Right => new(point.XMillimeters + distance, point.YMillimeters),
            TerminalAnchorDirection.Up => new(point.XMillimeters, point.YMillimeters - distance),
            TerminalAnchorDirection.Down => new(point.XMillimeters, point.YMillimeters + distance),
            _ => point
        };
}
