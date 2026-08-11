using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed record SelectionHitTestEntry(
    SelectionReference Target,
    DocumentRect Bounds,
    int Priority);

public sealed class SelectionHitTestIndex
{
    private readonly IReadOnlyList<SelectionHitTestEntry> _entries;

    public SelectionHitTestIndex(IEnumerable<SelectionHitTestEntry>? entries = null)
    {
        SelectionHitTestEntry[] values = entries?.ToArray() ?? [];
        if (values.Any(entry => entry.Bounds.WidthMillimeters <= 0 ||
                               entry.Bounds.HeightMillimeters <= 0))
        {
            throw new ArgumentException(
                "Selection hit-test bounds must have positive dimensions.",
                nameof(entries));
        }

        _entries = Array.AsReadOnly(values);
    }

    public IReadOnlyList<SelectionHitTestEntry> Entries => _entries;

    public SelectionReference? HitTest(DocumentPoint point)
    {
        return _entries
            .Where(entry => Contains(entry.Bounds, point))
            .OrderByDescending(entry => entry.Priority)
            .Select(entry => entry.Target)
            .FirstOrDefault();
    }

    public SelectionHitTestEntry? Find(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return _entries.FirstOrDefault(entry => entry.Target == target);
    }

    private static bool Contains(DocumentRect bounds, DocumentPoint point)
    {
        return point.XMillimeters >= bounds.XMillimeters &&
               point.XMillimeters <= bounds.XMillimeters + bounds.WidthMillimeters &&
               point.YMillimeters >= bounds.YMillimeters &&
               point.YMillimeters <= bounds.YMillimeters + bounds.HeightMillimeters;
    }
}
