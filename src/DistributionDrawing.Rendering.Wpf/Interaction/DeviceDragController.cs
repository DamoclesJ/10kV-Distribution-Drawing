using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Coordinates transient document-space dragging for device layouts.
/// Domain objects and topology are never changed here.
/// </summary>
public sealed class DeviceDragController
{
    private readonly LayoutSnapService _snapService;
    private DragState? _drag;

    public DeviceDragController(LayoutSnapService? snapService = null)
    {
        _snapService = snapService ?? new LayoutSnapService();
    }

    public bool IsActive => _drag is not null;

    public SelectionReference? Target => _drag?.Target;

    public bool TryBeginDrag(
        SelectionReference target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout,
        Guid? orbitParentPoleId = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        if (_drag is not null)
        {
            throw new InvalidOperationException("A device drag is already active.");
        }

        if (target.Kind == SelectionTargetKind.Device &&
            layout.DrawingLayout.Poles.TryGetValue(target.ObjectId, out PoleLayout? pole))
        {
            _drag = new PoleDragState(target, pointer, layout, pole, pole);
            return true;
        }

        if (target.Kind == SelectionTargetKind.RingCabinet &&
            layout.RingCabinetLayouts.TryGetValue(
                target.ObjectId,
                out RingCabinetLayout? cabinet))
        {
            _drag = new RingCabinetDragState(
                target,
                pointer,
                layout,
                cabinet,
                cabinet);
            return true;
        }

        if (target.Kind == SelectionTargetKind.PoleAttachment &&
            orbitParentPoleId is Guid poleId &&
            layout.DrawingLayout.Attachments.TryGetValue(
                target.ObjectId,
                out AttachmentLayout? attachment) &&
            layout.DrawingLayout.Poles.TryGetValue(poleId, out PoleLayout? parentPole))
        {
            _drag = new AttachmentDragState(
                target,
                pointer,
                layout,
                parentPole,
                attachment,
                attachment);
            return true;
        }

        return false;
    }

    public bool UpdatePreview(DocumentPoint pointer)
    {
        if (_drag is not { } drag)
        {
            throw new InvalidOperationException("No device drag is active.");
        }

        if (drag is AttachmentDragState attachment)
        {
            AttachmentLayout current = attachment.Before.MoveTo(
                PoleProfessionalGeometry.GetCableTerminationOffset(
                    attachment.ParentPole,
                    attachment.Before,
                    pointer));
            if (current.Offset == attachment.Current.Offset)
            {
                return false;
            }

            attachment.Layout.DrawingLayout.Replace(current);
            _drag = attachment with { Current = current };
            return true;
        }

        DocumentPoint position = new(
            drag.StartPosition.XMillimeters +
                pointer.XMillimeters - drag.StartPointer.XMillimeters,
            drag.StartPosition.YMillimeters +
                pointer.YMillimeters - drag.StartPointer.YMillimeters);
        position = _snapService.Snap(drag.Target, position, drag.Layout);
        if (position == drag.CurrentPosition)
        {
            return false;
        }

        _drag = drag switch
        {
            PoleDragState pole => UpdatePole(pole, position),
            RingCabinetDragState cabinet => UpdateRingCabinet(cabinet, position),
            _ => throw new InvalidOperationException("Unsupported device drag state.")
        };
        return true;
    }

    public ICommand? Commit()
    {
        if (_drag is not { } drag)
        {
            return null;
        }

        _drag = null;
        if (drag.StartPosition == drag.CurrentPosition)
        {
            return null;
        }

        return drag switch
        {
            PoleDragState pole => new MoveCommand(
                pole.Layout.DrawingLayout,
                pole.Before,
                pole.Current),
            RingCabinetDragState cabinet => new MoveRingCabinetCommand(
                cabinet.Layout,
                cabinet.Before.CabinetId,
                cabinet.Before.Position,
                cabinet.Current.Position),
            AttachmentDragState attachment => new MoveAttachmentCommand(
                attachment.Layout.DrawingLayout,
                attachment.Before.AttachmentId,
                attachment.Before.Offset,
                attachment.Current.Offset),
            _ => throw new InvalidOperationException("Unsupported device drag state.")
        };
    }

    public bool Cancel()
    {
        if (_drag is not { } drag)
        {
            return false;
        }

        _drag = null;
        switch (drag)
        {
            case PoleDragState pole:
                pole.Layout.DrawingLayout.Replace(pole.Before);
                break;
            case RingCabinetDragState cabinet:
                cabinet.Layout.ReplaceRingCabinet(cabinet.Before);
                break;
            case AttachmentDragState attachment:
                attachment.Layout.DrawingLayout.Replace(attachment.Before);
                break;
            default:
                throw new InvalidOperationException("Unsupported device drag state.");
        }

        return true;
    }

    private static PoleDragState UpdatePole(
        PoleDragState drag,
        DocumentPoint position)
    {
        PoleLayout preview = drag.Before.MoveTo(position);
        drag.Layout.DrawingLayout.Replace(preview);
        return drag with { Current = preview };
    }

    private static RingCabinetDragState UpdateRingCabinet(
        RingCabinetDragState drag,
        DocumentPoint position)
    {
        RingCabinetLayout preview = drag.Before.MoveTo(position);
        drag.Layout.ReplaceRingCabinet(preview);
        return drag with { Current = preview };
    }

    private abstract record DragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout)
    {
        public abstract DocumentPoint StartPosition { get; }

        public abstract DocumentPoint CurrentPosition { get; }
    }

    private sealed record PoleDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        PoleLayout Before,
        PoleLayout Current)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;
    }

    private sealed record RingCabinetDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        RingCabinetLayout Before,
        RingCabinetLayout Current)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;
    }

    private sealed record AttachmentDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        PoleLayout ParentPole,
        AttachmentLayout Before,
        AttachmentLayout Current)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Offset;

        public override DocumentPoint CurrentPosition => Current.Offset;
    }
}
