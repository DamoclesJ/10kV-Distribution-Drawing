using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class GroundingPointOffsetConstraint
{
    private readonly GroundingPresentationPolicy _policy;
    private readonly TerminalAnchorDirection _direction;
    private readonly double _snapTolerance;

    public GroundingPointOffsetConstraint(
        GroundingPresentationAnchor anchor,
        DrawingMetrics? metrics = null)
    {
        _policy = anchor.Policy;
        _direction = anchor.Direction;
        _snapTolerance = (metrics ?? DrawingMetrics.Default).Grounding.ManualSnapTolerance;
    }

    public DocumentPoint Normalize(DocumentPoint offset)
    {
        if (_policy != GroundingPresentationPolicy.GroundingAccessPoint)
        {
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
}
