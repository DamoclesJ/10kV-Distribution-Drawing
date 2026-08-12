using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed record SelectionHitTestEntry(
    SelectionReference Target,
    DocumentRect Bounds,
    int Priority,
    DocumentPoint? SegmentStart = null,
    DocumentPoint? SegmentEnd = null);

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

    public SelectionReference? HitTest(
        DocumentPoint point,
        double toleranceMillimeters = 0)
    {
        if (toleranceMillimeters < 0 ||
            double.IsNaN(toleranceMillimeters) ||
            double.IsInfinity(toleranceMillimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMillimeters));
        }

        return _entries
            .Where(entry => IsHit(entry, point, toleranceMillimeters))
            .OrderByDescending(entry => entry.Priority)
            .Select(entry => entry.Target)
            .FirstOrDefault();
    }

    public SelectionHitTestEntry? Find(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return _entries.FirstOrDefault(entry => entry.Target == target);
    }

    public IReadOnlyList<SelectionHitTestEntry> FindAll(SelectionReference target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return _entries
            .Where(entry => entry.Target == target)
            .ToArray();
    }

    private static bool IsHit(
        SelectionHitTestEntry entry,
        DocumentPoint point,
        double toleranceMillimeters)
    {
        if (entry.SegmentStart is DocumentPoint start &&
            entry.SegmentEnd is DocumentPoint end)
        {
            return DistanceToSegment(point, start, end) <= toleranceMillimeters;
        }

        return Contains(entry.Bounds, point, toleranceMillimeters);
    }

    private static bool Contains(
        DocumentRect bounds,
        DocumentPoint point,
        double toleranceMillimeters)
    {
        return point.XMillimeters >= bounds.XMillimeters - toleranceMillimeters &&
               point.XMillimeters <= bounds.XMillimeters + bounds.WidthMillimeters + toleranceMillimeters &&
               point.YMillimeters >= bounds.YMillimeters - toleranceMillimeters &&
               point.YMillimeters <= bounds.YMillimeters + bounds.HeightMillimeters + toleranceMillimeters;
    }

    private static double DistanceToSegment(
        DocumentPoint point,
        DocumentPoint start,
        DocumentPoint end)
    {
        double deltaX = end.XMillimeters - start.XMillimeters;
        double deltaY = end.YMillimeters - start.YMillimeters;
        double lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared == 0)
        {
            return Math.Sqrt(
                Math.Pow(point.XMillimeters - start.XMillimeters, 2) +
                Math.Pow(point.YMillimeters - start.YMillimeters, 2));
        }

        double projection =
            ((point.XMillimeters - start.XMillimeters) * deltaX +
             (point.YMillimeters - start.YMillimeters) * deltaY) /
            lengthSquared;
        double ratio = Math.Clamp(projection, 0, 1);
        double nearestX = start.XMillimeters + ratio * deltaX;
        double nearestY = start.YMillimeters + ratio * deltaY;
        return Math.Sqrt(
            Math.Pow(point.XMillimeters - nearestX, 2) +
            Math.Pow(point.YMillimeters - nearestY, 2));
    }
}
