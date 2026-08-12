using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.Selection;

public sealed record SelectionTransition(
    SelectionReference? SelectionBefore,
    SelectionReference? SelectionAfter)
{
    public static SelectionTransition ForAdd(
        SelectionReference? selectionBefore,
        SelectionReference selectionAfter)
    {
        ArgumentNullException.ThrowIfNull(selectionAfter);
        return new SelectionTransition(selectionBefore, selectionAfter);
    }

    public static SelectionTransition ForRemove(SelectionReference selectionBefore)
    {
        ArgumentNullException.ThrowIfNull(selectionBefore);
        return new SelectionTransition(selectionBefore, null);
    }

    public static SelectionTransition Preserve(SelectionReference? selection)
    {
        return new SelectionTransition(selection, selection);
    }
}
