using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class CableLabel
{
    public IReadOnlyList<SceneElement> CreateElements(
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

        return
        [
            new SceneText(
                layout.LabelPosition,
                $"{cableSegment.CableType} {cableSegment.Length:0.###}m",
                Colors.Black,
                3.5)
        ];
    }
}
