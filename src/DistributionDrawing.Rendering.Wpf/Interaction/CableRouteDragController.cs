using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class CableRouteDragController : ITransactionalDragPreview
{
    private DragState? _drag;

    public bool IsActive => _drag is not null;

    public bool TryBeginDrag(
        SelectionHitTestEntry hit,
        IReadOnlyList<SelectionHitTestEntry> cableSegments,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(cableSegments);
        ArgumentNullException.ThrowIfNull(layout);

        if (_drag is not null)
        {
            throw new InvalidOperationException("A cable route drag is already active.");
        }

        if (hit.Target.Kind != SelectionTargetKind.CableSegment ||
            hit.SegmentStart is not DocumentPoint start ||
            hit.SegmentEnd is not DocumentPoint end ||
            start.YMillimeters != end.YMillimeters)
        {
            return false;
        }

        int segmentIndex = IndexOf(cableSegments, hit);
        if (segmentIndex <= 0 || segmentIndex >= cableSegments.Count - 1)
        {
            return false;
        }

        CableRouteGuide? before = layout.CableRouteGuides.GetValueOrDefault(hit.Target.ObjectId);
        double initialY = before?.HorizontalYMillimeters ?? start.YMillimeters;
        _drag = new DragState(
            hit.Target.ObjectId,
            initialY,
            initialY,
            before,
            before,
            layout);
        return true;
    }

    public bool UpdatePreview(DocumentPoint pointer)
    {
        if (_drag is not { } drag)
        {
            throw new InvalidOperationException("No cable route drag is active.");
        }
        DragCandidateGuard.EnsureFinite(pointer.YMillimeters);

        if (pointer.YMillimeters == drag.CurrentY)
        {
            return false;
        }

        drag.Layout.SetCableRouteGuide(
            new CableRouteGuide(drag.CableSegmentId, pointer.YMillimeters));
        _drag = drag with { CurrentY = pointer.YMillimeters };
        return true;
    }

    public ICommand? Commit()
    {
        if (_drag is not { } drag)
        {
            return null;
        }

        _drag = null;
        double lastValidY = drag.LastValid?.HorizontalYMillimeters ?? drag.InitialY;
        if (drag.InitialY == lastValidY)
        {
            RestoreBefore(drag);
            return null;
        }

        var command = new SetCableRouteGuideCommand(
            drag.Layout,
            drag.CableSegmentId,
            drag.Before,
            new CableRouteGuide(drag.CableSegmentId, lastValidY));
        RestoreBefore(drag);
        return command;
    }

    public void AcceptCurrentPreview()
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No cable-route drag is active.");
        _drag = drag with
        {
            LastValid = new CableRouteGuide(drag.CableSegmentId, drag.CurrentY)
        };
    }

    public bool RollbackToLastValid()
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No cable-route drag is active.");
        double lastValidY = drag.LastValid?.HorizontalYMillimeters ?? drag.InitialY;
        bool changed = drag.CurrentY != lastValidY;
        Restore(drag.Layout, drag.CableSegmentId, drag.LastValid);
        _drag = drag with { CurrentY = lastValidY };
        return changed;
    }

    public bool Cancel()
    {
        if (_drag is not { } drag)
        {
            return false;
        }

        _drag = null;
        RestoreBefore(drag);
        return true;
    }

    private static void RestoreBefore(DragState drag)
    {
        Restore(drag.Layout, drag.CableSegmentId, drag.Before);
    }

    private static void Restore(
        RuntimeLayoutDocument layout,
        Guid cableSegmentId,
        CableRouteGuide? value)
    {
        if (value is null)
        {
            layout.RemoveCableRouteGuide(cableSegmentId);
        }
        else
        {
            layout.SetCableRouteGuide(value);
        }
    }

    private static int IndexOf(
        IReadOnlyList<SelectionHitTestEntry> entries,
        SelectionHitTestEntry target)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (ReferenceEquals(entries[index], target) || entries[index] == target)
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record DragState(
        Guid CableSegmentId,
        double InitialY,
        double CurrentY,
        CableRouteGuide? Before,
        CableRouteGuide? LastValid,
        RuntimeLayoutDocument Layout);
}

public sealed class SetCableRouteGuideCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;
    private readonly Guid _cableSegmentId;
    private readonly CableRouteGuide? _before;
    private readonly CableRouteGuide _after;

    public SetCableRouteGuideCommand(
        RuntimeLayoutDocument layout,
        Guid cableSegmentId,
        CableRouteGuide? before,
        CableRouteGuide after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _cableSegmentId = cableSegmentId;
        _before = before;
        _after = after ?? throw new ArgumentNullException(nameof(after));
    }

    public void Execute() => _layout.SetCableRouteGuide(_after);

    public void Undo()
    {
        if (_before is null)
        {
            _layout.RemoveCableRouteGuide(_cableSegmentId);
        }
        else
        {
            _layout.SetCableRouteGuide(_before);
        }
    }

    public void Redo() => Execute();
}
