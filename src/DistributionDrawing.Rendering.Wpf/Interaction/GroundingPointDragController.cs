using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Coordinates presentation-only dragging of a GroundingPoint body.
/// </summary>
public sealed class GroundingPointDragController : ITransactionalDragPreview
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

        GroundingPoint groundingPoint = document.GetGroundingPoint(hit.Target.ObjectId);
        layout.GroundingPointLayouts.TryGetValue(
            hit.Target.ObjectId,
            out GroundingPointLayout? before);
        DocumentPoint startOffset = before?.SymbolOffset ?? new DocumentPoint(0, 0);
        GroundingPointOffsetConstraint? constraint = hit.GroundingAnchor is GroundingPresentationAnchor anchor
            ? new GroundingPointOffsetConstraint(
                anchor,
                new GroundingPointLayoutResolver().Resolve(
                    groundingPoint,
                    anchor,
                    null).DefaultSymbolTop)
            : null;
        _drag = new DragState(
            document,
            layout,
            hit.Target.ObjectId,
            pointer,
            startOffset,
            before,
            startOffset,
            startOffset,
            before,
            constraint);
        return true;
    }

    public bool UpdatePreview(DocumentPoint pointer)
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No grounding-point drag is active.");
        DragCandidateGuard.EnsureFinite(pointer);
        DocumentPoint current = new(
            drag.StartOffset.XMillimeters + pointer.XMillimeters - drag.StartPointer.XMillimeters,
            drag.StartOffset.YMillimeters + pointer.YMillimeters - drag.StartPointer.YMillimeters);
        DragCandidateGuard.EnsureFinite(current);
        current = drag.Constraint?.Normalize(current) ?? current;
        DragCandidateGuard.EnsureFinite(current);
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
        if (drag.StartOffset == drag.LastValidOffset)
        {
            Restore(drag);
            return null;
        }
        var command = new MoveGroundingPointLayoutCommand(
            drag.Document,
            drag.Layout,
            drag.GroundingPointId,
            drag.Before,
            new GroundingPointLayout(drag.GroundingPointId, drag.LastValidOffset));
        Restore(drag);
        return command;
    }

    public void AcceptCurrentPreview()
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No grounding-point drag is active.");
        _drag = drag with
        {
            LastValidOffset = drag.CurrentOffset,
            LastValid = new GroundingPointLayout(drag.GroundingPointId, drag.CurrentOffset)
        };
    }

    public bool RollbackToLastValid()
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No grounding-point drag is active.");
        bool changed = drag.CurrentOffset != drag.LastValidOffset;
        Restore(drag.Layout, drag.GroundingPointId, drag.LastValid);
        _drag = drag with { CurrentOffset = drag.LastValidOffset };
        return changed;
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
        Restore(drag.Layout, drag.GroundingPointId, drag.Before);
    }

    private static void Restore(
        RuntimeLayoutDocument layout,
        Guid groundingPointId,
        GroundingPointLayout? value)
    {
        if (value is null)
        {
            layout.RemoveGroundingPointLayout(groundingPointId);
        }
        else
        {
            layout.SetGroundingPointLayout(value);
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
        DocumentPoint LastValidOffset,
        GroundingPointLayout? LastValid,
        GroundingPointOffsetConstraint? Constraint);
}
