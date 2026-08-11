using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public static class SelectionOverlayBuilder
{
    public static IReadOnlyList<SceneElement> CreateElements(
        SelectionHitTestIndex hitTestIndex,
        SelectionReference? selected)
    {
        ArgumentNullException.ThrowIfNull(hitTestIndex);

        if (selected is null || hitTestIndex.Find(selected) is not SelectionHitTestEntry entry)
        {
            return [];
        }

        const double marginMillimeters = 1.5;
        DocumentRect bounds = entry.Bounds;
        return
        [
            new SceneRectangle(
                new DocumentRect(
                    bounds.XMillimeters - marginMillimeters,
                    bounds.YMillimeters - marginMillimeters,
                    bounds.WidthMillimeters + marginMillimeters * 2,
                    bounds.HeightMillimeters + marginMillimeters * 2),
                Colors.DeepSkyBlue,
                1.2)
        ];
    }
}
