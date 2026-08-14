using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class CableLabel
{
    public LabelRequest CreateRequest(
        CableSegment cableSegment,
        CableLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cableSegment);
        ArgumentNullException.ThrowIfNull(layout);

        if (cableSegment.Id != layout.CableSegmentId)
        {
            throw new InvalidOperationException(
                "Cable segment and cable layout IDs must match.");
        }

        return new LabelRequest(
            LabelTargetKind.CableSegment,
            cableSegment.Id,
            $"{cableSegment.CableType} {cableSegment.Length:0.###}m",
            layout.LabelPosition,
            new DocumentPoint(0, 0),
            fontSizeMillimeters: 3.5);
    }

    public SceneText CreateElement(LabelLayoutResult layoutResult)
    {
        ArgumentNullException.ThrowIfNull(layoutResult);

        return new SceneText(
            layoutResult.Position,
            layoutResult.Text,
            Colors.Black,
            layoutResult.Request.FontSizeMillimeters);
    }
}
