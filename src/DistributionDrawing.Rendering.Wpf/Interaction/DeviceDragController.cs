using DistributionDrawing.Domain.Documents;
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
    private readonly SelectionMovePlanner _movePlanner;
    private DragState? _drag;

    public DeviceDragController(
        LayoutSnapService? snapService = null,
        SelectionMovePlanner? movePlanner = null)
    {
        _snapService = snapService ?? new LayoutSnapService();
        _movePlanner = movePlanner ?? new SelectionMovePlanner();
    }

    public bool IsActive => _drag is not null;

    public SelectionReference? Target => _drag?.Target;

    public bool IsGroupDrag => _drag is GroupDragState;

    public bool TryBeginGroupDrag(
        SelectionSet selection,
        SelectionReference dragTarget,
        DocumentPoint pointer,
        DrawingDocument document,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(dragTarget);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureInactive();

        SelectionMovePlan plan = _movePlanner.Create(
            selection,
            dragTarget,
            document,
            layout);
        if (!plan.CanMove)
        {
            return false;
        }

        GroupMoveLayoutState before = CaptureState(plan.Roots, layout);
        SelectionMoveRoot anchor = plan.DragAnchorRoot!;
        _drag = new GroupDragState(
            dragTarget,
            pointer,
            layout,
            before,
            before,
            anchor,
            GetRootPosition(anchor, before),
            plan.Roots
                .Where(item => item.Kind is SelectionMoveRootKind.Pole or
                    SelectionMoveRootKind.RingCabinet)
                .Select(item => item.ObjectId)
                .ToHashSet());
        return true;
    }

    public bool TryBeginDrag(
        SelectionReference target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout,
        Guid? orbitParentPoleId = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureInactive();

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
                attachment,
                true);
            return true;
        }

        return false;
    }

    public bool TryBeginAttachmentDrag(
        SelectionReference target,
        Guid attachmentId,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout,
        Guid parentPoleId,
        bool orbitAroundPole)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureInactive();
        if (!layout.DrawingLayout.Attachments.TryGetValue(
                attachmentId,
                out AttachmentLayout? attachment) ||
            !layout.DrawingLayout.Poles.TryGetValue(
                parentPoleId,
                out PoleLayout? parentPole))
        {
            return false;
        }

        _drag = new AttachmentDragState(
            target,
            pointer,
            layout,
            parentPole,
            attachment,
            attachment,
            orbitAroundPole);
        return true;
    }

    public bool UpdatePreview(DocumentPoint pointer)
    {
        if (_drag is not { } drag)
        {
            throw new InvalidOperationException("No device drag is active.");
        }

        if (drag is GroupDragState group)
        {
            return UpdateGroup(group, pointer);
        }

        if (drag is AttachmentDragState attachment)
        {
            DocumentPoint offset = attachment.OrbitAroundPole
                ? PoleProfessionalGeometry.GetCableTerminationOffset(
                    attachment.ParentPole,
                    attachment.Before,
                    pointer)
                : Translate(
                    attachment.Before.Offset,
                    Delta(pointer, attachment.StartPointer));
            AttachmentLayout current = attachment.Before.MoveTo(offset);
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
            GroupDragState group => new GroupMoveCommand(
                group.Layout,
                group.Before,
                group.Current),
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
            case GroupDragState group:
                GroupMoveCommand.Apply(group.Layout, group.Before);
                break;
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

    private bool UpdateGroup(GroupDragState drag, DocumentPoint pointer)
    {
        DocumentPoint rawDelta = Delta(pointer, drag.StartPointer);
        DocumentPoint anchorCandidate = Translate(drag.AnchorStartPosition, rawDelta);
        DocumentPoint snappedAnchor = _snapService.Snap(
            drag.AnchorRoot.SelectionReference,
            anchorCandidate,
            drag.Layout,
            drag.ExcludedSnapObjectIds);
        DocumentPoint finalDelta = Delta(snappedAnchor, drag.AnchorStartPosition);
        GroupMoveLayoutState current = Translate(drag.Before, finalDelta);
        if (current.HasSamePositions(drag.Current))
        {
            return false;
        }

        GroupMoveCommand.Apply(drag.Layout, current);
        _drag = drag with { Current = current };
        return true;
    }

    private static GroupMoveLayoutState CaptureState(
        IEnumerable<SelectionMoveRoot> roots,
        RuntimeLayoutDocument layout)
    {
        SelectionMoveRoot[] values = roots.ToArray();
        return new GroupMoveLayoutState(
            Array.AsReadOnly(values
                .Where(item => item.Kind == SelectionMoveRootKind.Pole)
                .Select(item => layout.DrawingLayout.Poles[item.ObjectId])
                .ToArray()),
            Array.AsReadOnly(values
                .Where(item => item.Kind == SelectionMoveRootKind.RingCabinet)
                .Select(item => layout.RingCabinetLayouts[item.ObjectId])
                .ToArray()),
            Array.AsReadOnly(values
                .Where(item => item.Kind == SelectionMoveRootKind.PoleAttachment)
                .Select(item => layout.DrawingLayout.Attachments[item.ObjectId])
                .ToArray()));
    }

    private static DocumentPoint GetRootPosition(
        SelectionMoveRoot root,
        GroupMoveLayoutState state) => root.Kind switch
        {
            SelectionMoveRootKind.Pole => state.Poles.Single(item =>
                item.PoleId == root.ObjectId).Position,
            SelectionMoveRootKind.RingCabinet => state.RingCabinets.Single(item =>
                item.CabinetId == root.ObjectId).Position,
            SelectionMoveRootKind.PoleAttachment => state.Attachments.Single(item =>
                item.AttachmentId == root.ObjectId).Offset,
            _ => throw new InvalidOperationException("Unsupported selection move root.")
        };

    private static GroupMoveLayoutState Translate(
        GroupMoveLayoutState state,
        DocumentPoint delta) => new(
        Array.AsReadOnly(state.Poles.Select(item =>
            item.MoveTo(Translate(item.Position, delta))).ToArray()),
        Array.AsReadOnly(state.RingCabinets.Select(item =>
            item.MoveTo(Translate(item.Position, delta))).ToArray()),
        Array.AsReadOnly(state.Attachments.Select(item =>
            item.MoveTo(Translate(item.Offset, delta))).ToArray()));

    private static DocumentPoint Delta(DocumentPoint value, DocumentPoint origin) => new(
        value.XMillimeters - origin.XMillimeters,
        value.YMillimeters - origin.YMillimeters);

    private static DocumentPoint Translate(DocumentPoint value, DocumentPoint delta) => new(
        value.XMillimeters + delta.XMillimeters,
        value.YMillimeters + delta.YMillimeters);

    private void EnsureInactive()
    {
        if (_drag is not null)
        {
            throw new InvalidOperationException("A device drag is already active.");
        }
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
        AttachmentLayout Current,
        bool OrbitAroundPole)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Offset;

        public override DocumentPoint CurrentPosition => Current.Offset;
    }

    private sealed record GroupDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        GroupMoveLayoutState Before,
        GroupMoveLayoutState Current,
        SelectionMoveRoot AnchorRoot,
        DocumentPoint AnchorStartPosition,
        IReadOnlySet<Guid> ExcludedSnapObjectIds)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => AnchorStartPosition;

        public override DocumentPoint CurrentPosition => GetRootPosition(
            AnchorRoot,
            Current);
    }
}
