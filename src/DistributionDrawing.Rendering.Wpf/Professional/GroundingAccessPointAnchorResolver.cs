using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Devices;
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
        out GroundingPresentationAnchor anchor) => TryResolve(
            point,
            document,
            layout,
            routes,
            null,
            out anchor);

    public bool TryResolve(
        GroundingAccessPoint point,
        DrawingDocument document,
        DrawingLayout layout,
        IReadOnlyDictionary<Guid, OrthogonalRoute> routes,
        IReadOnlyDictionary<Guid, TransformerLayout>? transformerLayouts,
        out GroundingPresentationAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(routes);

        OverheadLine? line = document.OverheadLines.SingleOrDefault(candidate =>
            candidate.ConnectionId == point.ConnectionId);
        Connection? connection = document.Connections.SingleOrDefault(candidate =>
            candidate.Id == point.ConnectionId);
        if (line is null ||
            connection is null ||
            !routes.TryGetValue(point.ConnectionId, out OrthogonalRoute? route) ||
            !SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                layout,
                point.PoleId,
                point.AdjacentEndpoint,
                connection,
                point.PlacementSide,
                out GroundingAccessHalfEdge halfEdge))
        {
            anchor = default;
            return false;
        }

        DocumentRect envelope;
        if (point.PlacementSide == GroundingAccessPlacementSide.AdjacentEndpointSide)
        {
            Transformer? transformer = document.Transformers.SingleOrDefault(candidate =>
                candidate.HvTerminalId == point.AdjacentEndpoint.TargetId);
            if (transformer is null || transformerLayouts is null ||
                !transformerLayouts.TryGetValue(transformer.Id, out TransformerLayout? transformerLayout))
            {
                anchor = default;
                return false;
            }
            envelope = TransformerProfessionalGeometry.Create(
                transformer,
                transformerLayout,
                _metrics.Transformer).Bounds;
        }
        else
        {
            envelope = PoleProfessionalGeometry.GetOccupiedEnvelope(
                point.PoleId, document, layout, _metrics);
        }
        double requiredCenterSeparation = _metrics.Line.GroundingAccessClearance +
            (_metrics.Line.GroundingAccessMarkerDiameter + _metrics.Line.ConnectionThickness) / 2;
        DocumentRect forbiddenEnvelope = new(
            envelope.XMillimeters - requiredCenterSeparation,
            envelope.YMillimeters - requiredCenterSeparation,
            envelope.WidthMillimeters + requiredCenterSeparation * 2,
            envelope.HeightMillimeters + requiredCenterSeparation * 2);
        if (!TryFindPathExit(
                halfEdge.OrientedPath,
                forbiddenEnvelope,
                out DocumentPoint position,
                out TerminalAnchorDirection direction))
        {
            anchor = default;
            return false;
        }
        anchor = new GroundingPresentationAnchor(
            position,
            direction,
            Policy: GroundingPresentationPolicy.GroundingAccessPoint);
        return true;
    }

    private static bool TryFindPathExit(
        IReadOnlyList<DocumentPoint> path,
        DocumentRect forbiddenEnvelope,
        out DocumentPoint position,
        out TerminalAnchorDirection direction)
    {
        for (var index = 0; index + 1 < path.Count; index++)
        {
            DocumentPoint start = path[index];
            DocumentPoint end = path[index + 1];
            direction = ResolveDirection(start, end);
            if (!Contains(forbiddenEnvelope, start))
            {
                position = start;
                return true;
            }
            if (Contains(forbiddenEnvelope, end))
            {
                continue;
            }

            position = direction switch
            {
                TerminalAnchorDirection.Left => new DocumentPoint(
                    forbiddenEnvelope.XMillimeters, start.YMillimeters),
                TerminalAnchorDirection.Right => new DocumentPoint(
                    forbiddenEnvelope.XMillimeters + forbiddenEnvelope.WidthMillimeters,
                    start.YMillimeters),
                TerminalAnchorDirection.Up => new DocumentPoint(
                    start.XMillimeters, forbiddenEnvelope.YMillimeters),
                _ => new DocumentPoint(
                    start.XMillimeters,
                    forbiddenEnvelope.YMillimeters + forbiddenEnvelope.HeightMillimeters)
            };
            return true;
        }
        position = default;
        direction = default;
        return false;
    }

    private static bool Contains(DocumentRect rectangle, DocumentPoint point) =>
        point.XMillimeters > rectangle.XMillimeters &&
        point.XMillimeters < rectangle.XMillimeters + rectangle.WidthMillimeters &&
        point.YMillimeters > rectangle.YMillimeters &&
        point.YMillimeters < rectangle.YMillimeters + rectangle.HeightMillimeters;

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

}
