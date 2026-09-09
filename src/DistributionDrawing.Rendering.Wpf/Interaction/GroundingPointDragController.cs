using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Coordinates presentation-only dragging of a GroundingPoint body.
/// </summary>
public sealed class GroundingPointDragController
{
    private DragState? _drag;

    public bool IsActive => _drag is not null;

    public bool TryBeginDrag(
        SelectionHitTestEntry hit,
        DocumentPoint pointer,
        DrawingDocument document,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        if (_drag is not null)
        {
            throw new InvalidOperationException("A grounding-point drag is already active.");
        }
        if (hit.Target.Kind != SelectionTargetKind.GroundingPoint || !hit.CanStartDrag)
        {
            return false;
        }

        _ = document.GetGroundingPoint(hit.Target.ObjectId);
        layout.GroundingPointLayouts.TryGetValue(
            hit.Target.ObjectId,
            out GroundingPointLayout? before);
        DocumentPoint startOffset = before?.SymbolOffset ?? new DocumentPoint(0, 0);
        GroundingPointOffsetConstraint? constraint = hit.GroundingAnchor is GroundingPresentationAnchor anchor
            ? new GroundingPointOffsetConstraint(anchor)
            : null;
        _drag = new DragState(
            document,
            layout,
            hit.Target.ObjectId,
            pointer,
            startOffset,
            before,
            startOffset,
            constraint);
        return true;
    }

    public bool UpdatePreview(DocumentPoint pointer)
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No grounding-point drag is active.");
        DocumentPoint current = new(
            drag.StartOffset.XMillimeters + pointer.XMillimeters - drag.StartPointer.XMillimeters,
            drag.StartOffset.YMillimeters + pointer.YMillimeters - drag.StartPointer.YMillimeters);
        current = drag.Constraint?.Normalize(current) ?? current;
        if (current == drag.CurrentOffset)
        {
            return false;
        }
        drag.Layout.SetGroundingPointLayout(new GroundingPointLayout(
            drag.GroundingPointId,
            current));
        _drag = drag with { CurrentOffset = current };
        return true;
    }

    public ICommand? Commit()
    {
        if (_drag is not DragState drag)
        {
            return null;
        }
        _drag = null;
        if (drag.StartOffset == drag.CurrentOffset)
        {
            Restore(drag);
            return null;
        }
        return new MoveGroundingPointLayoutCommand(
            drag.Document,
            drag.Layout,
            drag.GroundingPointId,
            drag.Before,
            new GroundingPointLayout(drag.GroundingPointId, drag.CurrentOffset));
    }

    public bool Cancel()
    {
        if (_drag is not DragState drag)
        {
            return false;
        }
        _drag = null;
        Restore(drag);
        return drag.StartOffset != drag.CurrentOffset;
    }

    private static void Restore(DragState drag)
    {
        if (drag.Before is null)
        {
            drag.Layout.RemoveGroundingPointLayout(drag.GroundingPointId);
        }
        else
        {
            drag.Layout.SetGroundingPointLayout(drag.Before);
        }
    }

    private sealed record DragState(
        DrawingDocument Document,
        RuntimeLayoutDocument Layout,
        Guid GroundingPointId,
        DocumentPoint StartPointer,
        DocumentPoint StartOffset,
        GroundingPointLayout? Before,
        DocumentPoint CurrentOffset,
        GroundingPointOffsetConstraint? Constraint);
}
