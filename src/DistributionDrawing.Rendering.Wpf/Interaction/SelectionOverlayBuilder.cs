using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public static class SelectionOverlayBuilder
{
    public static IReadOnlyList<SceneElement> CreateElements(
        SelectionHitTestIndex hitTestIndex,
        SelectionReference? selected)
    {
        return CreateElements(
            hitTestIndex,
            selected is null ? SelectionSet.Empty : SelectionSet.Create([selected]));
    }

    public static IReadOnlyList<SceneElement> CreateElements(
        SelectionHitTestIndex hitTestIndex,
        SelectionSet selectionSet)
    {
        ArgumentNullException.ThrowIfNull(hitTestIndex);
        ArgumentNullException.ThrowIfNull(selectionSet);

        if (selectionSet.Count == 0)
        {
            return [];
        }

        const double marginMillimeters = 1.5;
        return selectionSet.SelectedReferences
            .SelectMany(selected => hitTestIndex.FindAll(selected).Select(entry =>
            {
                bool isPrimary = selected == selectionSet.PrimarySelection;
                DocumentRect bounds = entry.Bounds;
                return (SceneElement)new SceneRectangle(
                    new DocumentRect(
                        bounds.XMillimeters - marginMillimeters,
                        bounds.YMillimeters - marginMillimeters,
                        bounds.WidthMillimeters + marginMillimeters * 2,
                        bounds.HeightMillimeters + marginMillimeters * 2),
                    isPrimary ? Colors.DeepSkyBlue : Colors.CornflowerBlue,
                    isPrimary ? 1.2 : 0.8);
            }))
            .ToArray();
    }
}
