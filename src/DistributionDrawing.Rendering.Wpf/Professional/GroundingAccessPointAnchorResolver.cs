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
            halfEdge.ConductorOrigin,
            halfEdge.DirectionPoint);
        DocumentRect envelope = PoleProfessionalGeometry.GetOccupiedEnvelope(
            point.PoleId, document, layout, _metrics);
        double requiredCenterSeparation = _metrics.Line.GroundingAccessClearance +
            (_metrics.Line.GroundingAccessMarkerDiameter + _metrics.Line.ConnectionThickness) / 2;
        DocumentRect forbiddenEnvelope = new(
            envelope.XMillimeters - requiredCenterSeparation,
            envelope.YMillimeters - requiredCenterSeparation,
            envelope.WidthMillimeters + requiredCenterSeparation * 2,
            envelope.HeightMillimeters + requiredCenterSeparation * 2);
        double distance = DirectionalExitDistance(
            halfEdge.ConductorOrigin,
            direction,
            forbiddenEnvelope);
        DocumentPoint position = Move(halfEdge.ConductorOrigin, direction, distance);
        if (!route.Segments.Any(segment => Contains(segment, position)))
        {
            anchor = default;
            return false;
        }
        anchor = new GroundingPresentationAnchor(
            position,
            direction);
        return true;
    }

    private static double DirectionalExitDistance(
        DocumentPoint origin,
        TerminalAnchorDirection direction,
        DocumentRect forbiddenEnvelope)
    {
        bool withinHorizontalSpan = origin.XMillimeters > forbiddenEnvelope.XMillimeters &&
            origin.XMillimeters < forbiddenEnvelope.XMillimeters + forbiddenEnvelope.WidthMillimeters;
        bool withinVerticalSpan = origin.YMillimeters > forbiddenEnvelope.YMillimeters &&
            origin.YMillimeters < forbiddenEnvelope.YMillimeters + forbiddenEnvelope.HeightMillimeters;
        return direction switch
        {
            TerminalAnchorDirection.Left when withinVerticalSpan =>
                Math.Max(0, origin.XMillimeters - forbiddenEnvelope.XMillimeters),
            TerminalAnchorDirection.Right when withinVerticalSpan => Math.Max(0,
                forbiddenEnvelope.XMillimeters + forbiddenEnvelope.WidthMillimeters - origin.XMillimeters),
            TerminalAnchorDirection.Up when withinHorizontalSpan =>
                Math.Max(0, origin.YMillimeters - forbiddenEnvelope.YMillimeters),
            TerminalAnchorDirection.Down when withinHorizontalSpan => Math.Max(0,
                forbiddenEnvelope.YMillimeters + forbiddenEnvelope.HeightMillimeters - origin.YMillimeters),
            _ => 0
        };
    }

    private static bool Contains(OrthogonalRouteSegment segment, DocumentPoint point) =>
        segment.IsHorizontal
            ? point.YMillimeters == segment.Start.YMillimeters &&
              point.XMillimeters >= Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) &&
              point.XMillimeters <= Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters)
            : point.XMillimeters == segment.Start.XMillimeters &&
              point.YMillimeters >= Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) &&
              point.YMillimeters <= Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters);

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
