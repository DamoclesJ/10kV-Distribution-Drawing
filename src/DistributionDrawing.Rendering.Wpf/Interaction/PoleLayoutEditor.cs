using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class PoleLayoutEditor
{
    private DragState? _drag;

    public bool IsArmed => _drag is { Phase: DragPhase.Armed };

    public bool IsDragging => _drag is { Phase: DragPhase.Dragging };

    public bool IsActive => _drag is not null;

    public void BeginDrag(
        SelectionReference target,
        DocumentPoint pointer,
        PoleLayout layout,
        DrawingLayout documentLayout)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(documentLayout);

        if (target.Kind != SelectionTargetKind.Device ||
            target.ObjectId != layout.PoleId)
        {
            throw new ArgumentException(
                "Only a selected pole can begin a layout drag.",
                nameof(target));
        }

        _drag = new DragState(
            target,
            pointer,
            layout,
            layout,
            documentLayout,
            DragPhase.Armed);
    }

    public PoleLayout UpdatePreview(DocumentPoint pointer)
    {
        if (_drag is not { } drag)
        {
            throw new InvalidOperationException("No pole drag is active.");
        }

        DocumentPoint position = new(
            drag.StartLayout.Position.XMillimeters +
                pointer.XMillimeters - drag.StartPointer.XMillimeters,
            drag.StartLayout.Position.YMillimeters +
                pointer.YMillimeters - drag.StartPointer.YMillimeters);
        PoleLayout preview = drag.StartLayout.MoveTo(position);
        _drag = drag with { CurrentLayout = preview, Phase = DragPhase.Dragging };
        return preview;
    }

    public MoveCommand? Commit()
    {
        if (_drag is not { } drag)
        {
            return null;
        }

        _drag = null;
        return new MoveCommand(
            drag.DocumentLayout,
            drag.StartLayout,
            drag.CurrentLayout);
    }

    public PoleLayout? Cancel()
    {
        if (_drag is not { } drag)
        {
            return null;
        }

        _drag = null;
        return drag.StartLayout;
    }

    private sealed record DragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        PoleLayout StartLayout,
        PoleLayout CurrentLayout,
        DrawingLayout DocumentLayout,
        DragPhase Phase);

    private enum DragPhase
    {
        Armed,
        Dragging
    }
}
