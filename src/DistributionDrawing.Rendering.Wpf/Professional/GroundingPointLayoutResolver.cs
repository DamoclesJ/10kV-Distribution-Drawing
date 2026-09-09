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
        GroundingPresentationPolicy policy = groundingPoint.Target.Kind ==
            GroundingTargetKind.GroundingAccessPoint
            ? GroundingPresentationPolicy.GroundingAccessPoint
            : anchor.Policy;
        DocumentPoint offset = manualLayout?.SymbolOffset ?? new DocumentPoint(0, 0);
        if (policy == GroundingPresentationPolicy.GroundingAccessPoint &&
            manualLayout is not null)
        {
            offset = NormalizeGapOffset(offset, anchor.Direction);
        }
        bool effectiveManual = manualLayout is not null &&
            (offset.XMillimeters != 0 || offset.YMillimeters != 0);
        DocumentPoint symbolTop = Translate(defaultTop, offset);
        IReadOnlyList<OrthogonalRouteSegment> leader = ResolveLeader(
            policy,
            anchor,
            symbolTop,
            effectiveManual);

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
        GroundingPresentationPolicy policy,
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop,
        bool isManual)
    {
        if (policy == GroundingPresentationPolicy.RingCabinetCableTerminal)
        {
            return ResolveRingCabinetLeader(anchor, symbolTop);
        }

        if (policy == GroundingPresentationPolicy.GroundingAccessPoint &&
            isManual)
        {
            return ResolveGapLeader(anchor, symbolTop);
        }

        bool allowDirectVertical = policy is
            GroundingPresentationPolicy.GroundingAccessPoint or
            GroundingPresentationPolicy.PoleCableTermination;
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
        double stub = isManual && policy is
            (GroundingPresentationPolicy.GroundingAccessPoint or
             GroundingPresentationPolicy.PoleCableTermination)
            ? 0
            : Math.Max(
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

    private IReadOnlyList<OrthogonalRouteSegment> ResolveRingCabinetLeader(
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop)
    {
        var points = new List<DocumentPoint>();
        AddPoint(points, anchor.Position);
        double deltaX = symbolTop.XMillimeters - anchor.Position.XMillimeters;
        double centerTolerance = Math.Max(
            _metrics.General.StandardStrokeThickness,
            _metrics.Routing.ObstacleClearance / 2);
        double side = Math.Abs(deltaX) <= centerTolerance
            ? ResolveRingCabinetCenterSide(anchor)
            : Math.Sign(deltaX);

        if (symbolTop.YMillimeters >= anchor.Position.YMillimeters)
        {
            AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, anchor.Position.YMillimeters));
            AddPoint(points, symbolTop);
            return Segments(points);
        }

        double corridor = Math.Max(
            _metrics.Grounding.LeaderLength,
            Math.Abs(symbolTop.XMillimeters - anchor.Position.XMillimeters) +
            _metrics.Grounding.BarSpacing);
        double corridorX = anchor.Position.XMillimeters + side * corridor;
        corridorX = MoveOutsideRingCabinetInternalLead(
            corridorX,
            side,
            anchor.RingCabinetInternalLeadBounds);
        if (Math.Sign(symbolTop.XMillimeters - corridorX) == side)
        {
            corridorX = symbolTop.XMillimeters - side * _metrics.Grounding.BarSpacing;
            corridorX = MoveOutsideRingCabinetInternalLead(
                corridorX,
                side,
                anchor.RingCabinetInternalLeadBounds);
        }
        double upperY = symbolTop.YMillimeters - _metrics.Grounding.BarSpacing;
        if (ShouldUseRingCabinetHorizontalFallback(
                anchor,
                symbolTop,
                upperY))
        {
            AddPoint(points, new DocumentPoint(
                corridorX,
                anchor.Position.YMillimeters));
            AddPoint(points, new DocumentPoint(corridorX, symbolTop.YMillimeters));
            AddPoint(points, symbolTop);
            return Segments(points);
        }

        if (NeedsRingCabinetSafeEntry(
                anchor,
                symbolTop,
                corridorX,
                upperY))
        {
            upperY = anchor.RingCabinetInternalLeadBounds!.Value.YMillimeters -
                GetRingCabinetLeadClearance();
        }
        AddPoint(points, new DocumentPoint(corridorX, anchor.Position.YMillimeters));
        AddPoint(points, new DocumentPoint(corridorX, upperY));
        AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, upperY));
        AddPoint(points, symbolTop);
        return Segments(points);
    }

    private bool ShouldUseRingCabinetHorizontalFallback(
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop,
        double upperY)
    {
        if (anchor.RingCabinetInternalLeadBounds is not DocumentRect lead)
        {
            return false;
        }

        bool symbolInsideLead = symbolTop.XMillimeters > lead.XMillimeters &&
            symbolTop.XMillimeters < lead.XMillimeters + lead.WidthMillimeters;
        return symbolInsideLead &&
            PositiveOverlap(
                upperY,
                symbolTop.YMillimeters,
                lead.YMillimeters,
                lead.YMillimeters + lead.HeightMillimeters) > 0;
    }

    private bool NeedsRingCabinetSafeEntry(
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop,
        double corridorX,
        double upperY)
    {
        if (anchor.RingCabinetInternalLeadBounds is not DocumentRect lead)
        {
            return false;
        }

        bool horizontalTransferCrossesLead =
            PositiveOverlap(
                corridorX,
                symbolTop.XMillimeters,
                lead.XMillimeters,
                lead.XMillimeters + lead.WidthMillimeters) > 0;
        return horizontalTransferCrossesLead &&
            upperY >= lead.YMillimeters &&
            upperY <= lead.YMillimeters + lead.HeightMillimeters;
    }

    private double GetRingCabinetLeadClearance() => Math.Max(
        _metrics.General.StandardStrokeThickness,
        _metrics.Routing.ObstacleClearance / 2);

    private static double PositiveOverlap(
        double firstStart,
        double firstEnd,
        double secondStart,
        double secondEnd) => Math.Max(
            0,
            Math.Min(Math.Max(firstStart, firstEnd), Math.Max(secondStart, secondEnd)) -
            Math.Max(Math.Min(firstStart, firstEnd), Math.Min(secondStart, secondEnd)));

    private double MoveOutsideRingCabinetInternalLead(
        double candidateX,
        double side,
        DocumentRect? internalLeadBounds)
    {
        if (internalLeadBounds is not DocumentRect lead)
        {
            return candidateX;
        }

        double clearance = GetRingCabinetLeadClearance();
        double leadLeft = lead.XMillimeters - clearance;
        double leadRight = lead.XMillimeters + lead.WidthMillimeters + clearance;
        if (candidateX < leadLeft || candidateX > leadRight)
        {
            return candidateX;
        }

        return side >= 0 ? leadRight : leadLeft;
    }

    private static double ResolveRingCabinetCenterSide(
        GroundingPresentationAnchor anchor) =>
        anchor.Direction == TerminalAnchorDirection.Left ? -1 : 1;

    private IReadOnlyList<OrthogonalRouteSegment> ResolveGapLeader(
        GroundingPresentationAnchor anchor,
        DocumentPoint symbolTop)
    {
        if (anchor.Position == symbolTop)
        {
            return [];
        }

        TerminalAnchorDirection direction = anchor.Direction == TerminalAnchorDirection.Auto
            ? TerminalAnchorDirection.Right
            : anchor.Direction;
        var points = new List<DocumentPoint>();
        AddPoint(points, anchor.Position);
        if (anchor.Position.XMillimeters == symbolTop.XMillimeters &&
            symbolTop.YMillimeters > anchor.Position.YMillimeters)
        {
            AddPoint(points, symbolTop);
            return Segments(points);
        }

        bool symbolBelow = symbolTop.YMillimeters > anchor.Position.YMillimeters;
        bool symbolSameLevel = symbolTop.YMillimeters == anchor.Position.YMillimeters;
        if (symbolBelow || symbolSameLevel)
        {
            AddPoint(points, new DocumentPoint(
                symbolTop.XMillimeters,
                anchor.Position.YMillimeters));
            AddPoint(points, symbolTop);
            return Segments(points);
        }

        double entryY = symbolTop.YMillimeters -
            _metrics.Grounding.BarSpacing;
        if (direction is TerminalAnchorDirection.Left or TerminalAnchorDirection.Right)
        {
            double side = direction == TerminalAnchorDirection.Left ? -1 : 1;
            double corridorX = anchor.Position.XMillimeters +
                side * Math.Max(_metrics.Grounding.BarSpacing, 0.5);
            if (corridorX == symbolTop.XMillimeters)
            {
                corridorX +=
                    side * Math.Max(_metrics.Grounding.BarSpacing, 0.5);
            }
            AddPoint(points, new DocumentPoint(corridorX, anchor.Position.YMillimeters));
            AddPoint(points, new DocumentPoint(corridorX, entryY));
            AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, entryY));
        }
        else
        {
            double side = Math.Sign(symbolTop.XMillimeters - anchor.Position.XMillimeters);
            if (side == 0)
            {
                side = direction == TerminalAnchorDirection.Up ? 1 : -1;
            }
            double corridorX = anchor.Position.XMillimeters +
                side * Math.Max(_metrics.Grounding.BarSpacing, 0.5);
            if (corridorX == symbolTop.XMillimeters)
            {
                corridorX +=
                    side * Math.Max(_metrics.Grounding.BarSpacing, 0.5);
            }
            AddPoint(points, new DocumentPoint(corridorX, anchor.Position.YMillimeters));
            AddPoint(points, new DocumentPoint(corridorX, entryY));
            AddPoint(points, new DocumentPoint(symbolTop.XMillimeters, entryY));
        }

        AddPoint(points, symbolTop);
        return Segments(points);
    }

    private DocumentPoint NormalizeGapOffset(
        DocumentPoint offset,
        TerminalAnchorDirection direction)
    {
        double dx = offset.XMillimeters;
        double dy = offset.YMillimeters;
        double outward = direction switch
        {
            TerminalAnchorDirection.Left => -dx,
            TerminalAnchorDirection.Right => dx,
            TerminalAnchorDirection.Up => -dy,
            TerminalAnchorDirection.Down => dy,
            _ => dy
        };
        if (outward <= _metrics.Grounding.ManualSnapTolerance)
        {
            if (direction is TerminalAnchorDirection.Left or TerminalAnchorDirection.Right)
            {
                dx = 0;
            }
            else if (direction is TerminalAnchorDirection.Up or TerminalAnchorDirection.Down)
            {
                dy = 0;
            }
        }
        return new DocumentPoint(dx, dy);
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
