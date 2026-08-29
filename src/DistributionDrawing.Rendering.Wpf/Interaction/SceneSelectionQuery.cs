using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SceneSelectionQuery
{
    public IReadOnlyList<SelectionReference> QueryRectangle(
        SelectionHitTestIndex hitTestIndex,
        DocumentRect rectangle)
    {
        ArgumentNullException.ThrowIfNull(hitTestIndex);
        DocumentRect normalized = Normalize(rectangle);
        return DistinctTargets(hitTestIndex.Entries.Where(entry =>
            IsDirectlySelectable(entry.Target) && Intersects(entry, normalized)));
    }

    public IReadOnlyList<SelectionReference> SelectAll(
        SelectionHitTestIndex hitTestIndex)
    {
        ArgumentNullException.ThrowIfNull(hitTestIndex);
        return DistinctTargets(hitTestIndex.Entries.Where(entry =>
            IsDirectlySelectable(entry.Target)));
    }

    private static bool IsDirectlySelectable(SelectionReference target)
    {
        return target.Kind is not (
            SelectionTargetKind.Terminal or
            SelectionTargetKind.IntermediateTerminal);
    }

    private static IReadOnlyList<SelectionReference> DistinctTargets(
        IEnumerable<SelectionHitTestEntry> entries)
    {
        var identities = new HashSet<(SelectionTargetKind Kind, Guid ObjectId)>();
        var targets = new List<SelectionReference>();
        foreach (SelectionHitTestEntry entry in entries)
        {
            if (identities.Add((entry.Target.Kind, entry.Target.ObjectId)))
            {
                targets.Add(entry.Target);
            }
        }

        return targets;
    }

    private static bool Intersects(
        SelectionHitTestEntry entry,
        DocumentRect rectangle)
    {
        if (entry.SegmentStart is DocumentPoint start &&
            entry.SegmentEnd is DocumentPoint end)
        {
            return SegmentIntersectsRectangle(start, end, rectangle);
        }

        return RectanglesIntersect(entry.Bounds, rectangle);
    }

    private static bool RectanglesIntersect(DocumentRect first, DocumentRect second)
    {
        return first.XMillimeters <= Right(second) &&
               Right(first) >= second.XMillimeters &&
               first.YMillimeters <= Bottom(second) &&
               Bottom(first) >= second.YMillimeters;
    }

    private static bool SegmentIntersectsRectangle(
        DocumentPoint start,
        DocumentPoint end,
        DocumentRect rectangle)
    {
        double deltaX = end.XMillimeters - start.XMillimeters;
        double deltaY = end.YMillimeters - start.YMillimeters;
        double near = 0;
        double far = 1;

        return Clip(-deltaX, start.XMillimeters - rectangle.XMillimeters, ref near, ref far) &&
               Clip(deltaX, Right(rectangle) - start.XMillimeters, ref near, ref far) &&
               Clip(-deltaY, start.YMillimeters - rectangle.YMillimeters, ref near, ref far) &&
               Clip(deltaY, Bottom(rectangle) - start.YMillimeters, ref near, ref far);
    }

    private static bool Clip(double direction, double distance, ref double near, ref double far)
    {
        if (direction == 0)
        {
            return distance >= 0;
        }

        double ratio = distance / direction;
        if (direction < 0)
        {
            if (ratio > far)
            {
                return false;
            }

            near = Math.Max(near, ratio);
        }
        else
        {
            if (ratio < near)
            {
                return false;
            }

            far = Math.Min(far, ratio);
        }

        return true;
    }

    private static DocumentRect Normalize(DocumentRect rectangle)
    {
        double left = Math.Min(rectangle.XMillimeters, Right(rectangle));
        double top = Math.Min(rectangle.YMillimeters, Bottom(rectangle));
        double right = Math.Max(rectangle.XMillimeters, Right(rectangle));
        double bottom = Math.Max(rectangle.YMillimeters, Bottom(rectangle));
        return new DocumentRect(left, top, right - left, bottom - top);
    }

    private static double Right(DocumentRect rectangle) =>
        rectangle.XMillimeters + rectangle.WidthMillimeters;

    private static double Bottom(DocumentRect rectangle) =>
        rectangle.YMillimeters + rectangle.HeightMillimeters;
}
