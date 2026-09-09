using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record GroundingPointResolvedLayout(
    GroundingPresentationAnchor TargetAnchor,
    DocumentPoint DefaultSymbolTop,
    DocumentPoint SymbolTop,
    IReadOnlyList<OrthogonalRouteSegment> LeaderSegments,
    OrthogonalRouteSegment Stem,
    IReadOnlyList<OrthogonalRouteSegment> Bars,
    DocumentPoint? NumberOrigin,
    DocumentRect BodyBounds,
    DocumentRect? NumberBounds);

/// <summary>
/// Resolves GroundingPoint-only presentation geometry. Persisted state is
/// limited to an offset from the currently derived default symbol position.
/// </summary>
public sealed class GroundingPointLayoutResolver
{
    private readonly DrawingMetrics _metrics;

    public GroundingPointLayoutResolver(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public GroundingPointResolvedLayout Resolve(
        GroundingPoint groundingPoint,
        GroundingPresentationAnchor anchor,
        GroundingPointLayout? manualLayout)
    {
        ArgumentNullException.ThrowIfNull(groundingPoint);
        if (manualLayout is not null &&
            manualLayout.GroundingPointId != groundingPoint.GroundingPointId)
        {
            throw new ArgumentException(
                "Grounding layout does not belong to the grounding point.",
                nameof(manualLayout));
        }

        DocumentPoint defaultTop = ResolveDefaultSymbolTop(groundingPoint, anchor);
        DocumentPoint offset = manualLayout?.SymbolOffset ?? new DocumentPoint(0, 0);
        DocumentPoint symbolTop = Translate(defaultTop, offset);
        IReadOnlyList<OrthogonalRouteSegment> leader = ResolveLeader(
            groundingPoint.Target.Kind == GroundingTargetKind.GroundingAccessPoint,
            anchor,
            symbolTop);

        GroundingDrawingMetrics grounding = _metrics.Grounding;
        DocumentPoint stemBottom = new(
            symbolTop.XMillimeters,
            symbolTop.YMillimeters + grounding.StemLength);
        var stem = new OrthogonalRouteSegment(symbolTop, stemBottom, 0);
        double[] widths =
        [
            grounding.TopBarWidth,
            grounding.MiddleBarWidth,
            grounding.BottomBarWidth
        ];
        OrthogonalRouteSegment[] bars = widths.Select((width, index) =>
        {
            double y = stemBottom.YMillimeters + index * grounding.BarSpacing;
            return new OrthogonalRouteSegment(
                new DocumentPoint(symbolTop.XMillimeters - width / 2, y),
                new DocumentPoint(symbolTop.XMillimeters + width / 2, y),
                index);
        }).ToArray();
        double bottomBarY = bars[^1].Start.YMillimeters;
        DocumentPoint? numberOrigin = string.IsNullOrWhiteSpace(groundingPoint.Number)
            ? null
            : new DocumentPoint(
                symbolTop.XMillimeters,
                bottomBarY + grounding.BarSpacing);
        double left = symbolTop.XMillimeters - grounding.TopBarWidth / 2;
        var bodyBounds = new DocumentRect(
            left,
            symbolTop.YMillimeters,
            grounding.TopBarWidth,
            bottomBarY - symbolTop.YMillimeters);
        DocumentRect? numberBounds = numberOrigin is DocumentPoint number
            ? EstimateNumberBounds(number, groundingPoint.Number!)
            : null;

        return new GroundingPointResolvedLayout(
            anchor,
            defaultTop,
            symbolTop,
            leader,
            stem,
            Array.AsReadOnly(bars),
            numberOrigin,
            bodyBounds,
            numberBounds);
    }

    private DocumentPoint ResolveDefaultSymbolTop(
        GroundingPoint groundingPoint,
        GroundingPresentationAnchor anchor)
    {
        double leader = Math.Max(
            _metrics.Grounding.LeaderLength,
            anchor.MinimumStubLength);
        if (groundingPoint.Target.Kind == GroundingTargetKind.GroundingAccessPoint)
        {
            return anchor.Direction switch
            {
                TerminalAnchorDirection.Up => new DocumentPoint(
                    anchor.Position.XMillimeters + leader,
                    anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength),
                TerminalAnchorDirection.Down => new DocumentPoint(
                    anchor.Position.XMillimeters - leader,
                    anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength),
                _ => new DocumentPoint(
                    anchor.Position.XMillimeters,
                    anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength)
            };
        }

        return anchor.Direction switch
        {
            TerminalAnchorDirection.Left => new DocumentPoint(
                anchor.Position.XMillimeters - leader,
                anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength),
            TerminalAnchorDirection.Up => new DocumentPoint(
                anchor.Position.XMillimeters + _metrics.Grounding.LeaderLength,
                anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength),
            TerminalAnchorDirection.Down => new DocumentPoint(
                anchor.Position.XMillimeters + _metrics.Grounding.LeaderLength,
                anchor.Position.YMillimeters + leader + _metrics.Grounding.LeaderLength),
            _ => new DocumentPoint(
                anchor.Position.XMillimeters + leader,
                anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength)
        };
    }

    private IReadOnlyList<OrthogonalRouteSegment> ResolveLeader(
        bool allowDirectVertical,
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop)
    {
        if (allowDirectVertical && anchor.Position == symbolTop)
        {
            return [];
        }

        var points = new List<DocumentPoint>();
        AddPoint(points, anchor.Position);
        if (allowDirectVertical &&
            anchor.Position.XMillimeters == symbolTop.XMillimeters &&
            symbolTop.YMillimeters > anchor.Position.YMillimeters)
        {
            AddPoint(points, symbolTop);
            return Segments(points);
        }

        TerminalAnchorDirection direction = anchor.Direction == TerminalAnchorDirection.Auto
            ? TerminalAnchorDirection.Right
            : anchor.Direction;
        double stub = Math.Max(
            _metrics.Routing.PortStubLength,
            anchor.MinimumStubLength);
        DocumentPoint first = Move(anchor.Position, direction, stub);
        AddPoint(points, first);
        double entryY = Math.Min(
            first.YMillimeters,
            symbolTop.YMillimeters - _metrics.Grounding.BarSpacing);

        if (direction is TerminalAnchorDirection.Left or TerminalAnchorDirection.Right)
        {
            bool returnsTowardAnchor = direction == TerminalAnchorDirection.Right
                ? symbolTop.XMillimeters < first.XMillimeters
                : symbolTop.XMillimeters > first.XMillimeters;
            if (returnsTowardAnchor && entryY == first.YMillimeters)
            {
                entryY = Math.Min(
                    anchor.Position.YMillimeters + _metrics.Grounding.LeaderLength,
                    symbolTop.YMillimeters - _metrics.Grounding.BarSpacing);
            }
            AddPoint(points, new DocumentPoint(first.XMillimeters, entryY));
            AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, entryY));
        }
        else
        {
            double side = Math.Sign(symbolTop.XMillimeters - anchor.Position.XMillimeters);
            if (side == 0)
            {
                side = direction == TerminalAnchorDirection.Up ? 1 : -1;
            }
            double corridorX = symbolTop.XMillimeters;
            if (corridorX == first.XMillimeters)
            {
                corridorX += side * _metrics.Grounding.LeaderLength;
                double corridorEntryY = direction == TerminalAnchorDirection.Up
                    ? anchor.Position.YMillimeters
                    : first.YMillimeters + _metrics.Grounding.LeaderLength;
                entryY = Math.Min(
                    corridorEntryY,
                    symbolTop.YMillimeters - _metrics.Grounding.BarSpacing);
            }
            AddPoint(points, new DocumentPoint(corridorX, first.YMillimeters));
            AddPoint(points, new DocumentPoint(corridorX, entryY));
            AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, entryY));
        }

        AddPoint(points, symbolTop);
        return Segments(points);
    }

    private DocumentRect EstimateNumberBounds(DocumentPoint origin, string number)
    {
        double fontSize = _metrics.Typography.GroundingPointNumberFontSize;
        double width = Math.Max(fontSize, number.Length * fontSize * 0.65);
        return new DocumentRect(
            origin.XMillimeters - width / 2,
            origin.YMillimeters,
            width,
            fontSize * 1.25);
    }

    private static IReadOnlyList<OrthogonalRouteSegment> Segments(
        IReadOnlyList<DocumentPoint> points) => Array.AsReadOnly(points
        .Zip(points.Skip(1), (start, end) => (start, end))
        .Where(pair => pair.start != pair.end)
        .Select((pair, index) => new OrthogonalRouteSegment(pair.start, pair.end, index))
        .ToArray());

    private static void AddPoint(IList<DocumentPoint> points, DocumentPoint point)
    {
        if (points.Count == 0 || points[^1] != point)
        {
            points.Add(point);
        }
    }

    private static DocumentPoint Translate(DocumentPoint point, DocumentPoint offset) => new(
        point.XMillimeters + offset.XMillimeters,
        point.YMillimeters + offset.YMillimeters);

    private static DocumentPoint Move(
        DocumentPoint point,
        TerminalAnchorDirection direction,
        double distance) => direction switch
        {
            TerminalAnchorDirection.Left => new(point.XMillimeters - distance, point.YMillimeters),
            TerminalAnchorDirection.Up => new(point.XMillimeters, point.YMillimeters - distance),
            TerminalAnchorDirection.Down => new(point.XMillimeters, point.YMillimeters + distance),
            _ => new(point.XMillimeters + distance, point.YMillimeters)
        };
}
