using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class GroundingPointOffsetConstraint
{
    private readonly GroundingPresentationPolicy _policy;
    private readonly TerminalAnchorDirection _direction;
    private readonly double _snapTolerance;
    private readonly DocumentPoint? _defaultSymbolTop;
    private readonly DocumentPoint _anchorPosition;
    private readonly DocumentRect? _ringCabinetInternalLeadBounds;
    private readonly double _ringCabinetClearance;

    public GroundingPointOffsetConstraint(
        GroundingPresentationAnchor anchor,
        DocumentPoint? defaultSymbolTop = null,
        DrawingMetrics? metrics = null)
    {
        DrawingMetrics drawingMetrics = metrics ?? DrawingMetrics.Default;
        _policy = anchor.Policy;
        _direction = anchor.Direction;
        _snapTolerance = drawingMetrics.Grounding.ManualSnapTolerance;
        _defaultSymbolTop = defaultSymbolTop;
        _anchorPosition = anchor.Position;
        _ringCabinetInternalLeadBounds = anchor.RingCabinetInternalLeadBounds;
        _ringCabinetClearance = Math.Max(
            drawingMetrics.General.StandardStrokeThickness,
            drawingMetrics.Routing.ObstacleClearance / 2);
    }

    public DocumentPoint Normalize(DocumentPoint offset)
    {
        if (_policy != GroundingPresentationPolicy.GroundingAccessPoint)
        {
            if (_policy == GroundingPresentationPolicy.RingCabinetCableTerminal)
            {
                return NormalizeRingCabinetOffset(offset);
            }
            return offset;
        }

        double dx = offset.XMillimeters;
        double dy = offset.YMillimeters;
        double outward = _direction switch
        {
            TerminalAnchorDirection.Left => -dx,
            TerminalAnchorDirection.Right => dx,
            TerminalAnchorDirection.Up => -dy,
            TerminalAnchorDirection.Down => dy,
            _ => 0
        };
        if (outward <= _snapTolerance)
        {
            if (_direction is TerminalAnchorDirection.Left or TerminalAnchorDirection.Right)
            {
                dx = 0;
            }
            else if (_direction is TerminalAnchorDirection.Up or TerminalAnchorDirection.Down)
            {
                dy = 0;
            }
        }
        return new DocumentPoint(dx, dy);
    }

    private DocumentPoint NormalizeRingCabinetOffset(DocumentPoint offset)
    {
        if (_defaultSymbolTop is not DocumentPoint defaultTop ||
            _ringCabinetInternalLeadBounds is not DocumentRect lead)
        {
            return offset;
        }

        double symbolY = defaultTop.YMillimeters + offset.YMillimeters;
        if (symbolY >= _anchorPosition.YMillimeters)
        {
            return offset;
        }

        double forbiddenTop = lead.YMillimeters - _ringCabinetClearance;
        double forbiddenBottom =
            lead.YMillimeters + lead.HeightMillimeters + _ringCabinetClearance;
        if (symbolY <= forbiddenTop || symbolY >= forbiddenBottom)
        {
            return offset;
        }

        double forbiddenLeft = lead.XMillimeters - _ringCabinetClearance;
        double forbiddenRight =
            lead.XMillimeters + lead.WidthMillimeters + _ringCabinetClearance;
        double symbolX = defaultTop.XMillimeters + offset.XMillimeters;
        if (symbolX <= forbiddenLeft || symbolX >= forbiddenRight)
        {
            return offset;
        }

        double fallbackSide = _direction == TerminalAnchorDirection.Left ? -1 : 1;
        double clampedX = fallbackSide < 0 ? forbiddenLeft : forbiddenRight;
        return new DocumentPoint(
            clampedX - defaultTop.XMillimeters,
            offset.YMillimeters);
    }
}
