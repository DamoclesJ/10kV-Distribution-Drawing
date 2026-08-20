using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class LayoutSnapService
{
    private readonly DrawingMetrics _metrics;

    public LayoutSnapService(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public DocumentPoint Snap(
        SelectionReference target,
        DocumentPoint candidatePosition,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);

        (DocumentPoint center, DocumentPoint centerOffset) = GetCandidateCenter(
            target,
            candidatePosition,
            layout);
        (double Delta, Guid Id)? xSnap = null;
        (double Delta, Guid Id)? ySnap = null;
        foreach ((Guid id, DocumentPoint otherCenter) in GetOtherCenters(target, layout))
        {
            double deltaX = otherCenter.XMillimeters - center.XMillimeters;
            double deltaY = otherCenter.YMillimeters - center.YMillimeters;
            if (Math.Abs(deltaX) <= _metrics.Alignment.SnapTolerance &&
                IsBetter(deltaX, id, xSnap))
            {
                xSnap = (deltaX, id);
            }

            if (Math.Abs(deltaY) <= _metrics.Alignment.SnapTolerance &&
                IsBetter(deltaY, id, ySnap))
            {
                ySnap = (deltaY, id);
            }
        }

        return new DocumentPoint(
            center.XMillimeters + (xSnap?.Delta ?? 0) - centerOffset.XMillimeters,
            center.YMillimeters + (ySnap?.Delta ?? 0) - centerOffset.YMillimeters);
    }

    private static bool IsBetter(
        double delta,
        Guid id,
        (double Delta, Guid Id)? current)
    {
        return current is null ||
               Math.Abs(delta) < Math.Abs(current.Value.Delta) ||
               Math.Abs(delta) == Math.Abs(current.Value.Delta) && id.CompareTo(current.Value.Id) < 0;
    }

    private (DocumentPoint Center, DocumentPoint Offset) GetCandidateCenter(
        SelectionReference target,
        DocumentPoint position,
        RuntimeLayoutDocument layout)
    {
        if (target.Kind == SelectionTargetKind.Device &&
            layout.DrawingLayout.Poles.TryGetValue(target.ObjectId, out PoleLayout? pole))
        {
            DocumentPoint offset = new(
                pole.WidthMillimeters / 2,
                pole.HeightMillimeters / 2);
            return (Add(position, offset), offset);
        }

        if (target.Kind == SelectionTargetKind.RingCabinet &&
            layout.RingCabinetLayouts.TryGetValue(target.ObjectId, out RingCabinetLayout? cabinet))
        {
            DocumentPoint offset = new(
                cabinet.WidthMillimeters / 2,
                cabinet.HeightMillimeters / 2);
            return (Add(position, offset), offset);
        }

        return (position, new DocumentPoint(0, 0));
    }

    private static IEnumerable<(Guid Id, DocumentPoint Center)> GetOtherCenters(
        SelectionReference target,
        RuntimeLayoutDocument layout)
    {
        foreach (PoleLayout pole in layout.DrawingLayout.Poles.Values
                     .Where(pole => pole.PoleId != target.ObjectId)
                     .OrderBy(pole => pole.PoleId))
        {
            yield return (
                pole.PoleId,
                new DocumentPoint(
                    pole.Position.XMillimeters + pole.WidthMillimeters / 2,
                    pole.Position.YMillimeters + pole.HeightMillimeters / 2));
        }

        foreach (RingCabinetLayout cabinet in layout.RingCabinetLayouts.Values
                     .Where(cabinet => cabinet.CabinetId != target.ObjectId)
                     .OrderBy(cabinet => cabinet.CabinetId))
        {
            yield return (
                cabinet.CabinetId,
                new DocumentPoint(
                    cabinet.Position.XMillimeters + cabinet.WidthMillimeters / 2,
                    cabinet.Position.YMillimeters + cabinet.HeightMillimeters / 2));
        }
    }

    private static DocumentPoint Add(DocumentPoint first, DocumentPoint second)
    {
        return new DocumentPoint(
            first.XMillimeters + second.XMillimeters,
            first.YMillimeters + second.YMillimeters);
    }
}
