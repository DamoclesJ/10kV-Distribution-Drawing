using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Coordinates transient document-space dragging for device layouts.
/// Domain objects and topology are never changed here.
/// </summary>
public sealed class DeviceDragController : ITransactionalDragPreview
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

        GroupMoveLayoutState before = CaptureState(plan.Roots, document, layout);
        SelectionMoveRoot anchor = plan.DragAnchorRoot!;
        _drag = new GroupDragState(
            dragTarget,
            pointer,
            layout,
            before,
            before,
            before,
            anchor,
            GetRootPosition(anchor, before),
            plan.Roots
                .Where(item => item.Kind is SelectionMoveRootKind.Pole or
                    SelectionMoveRootKind.RingCabinet or
                    SelectionMoveRootKind.Transformer or
                    SelectionMoveRootKind.CustomerStation)
                .Select(item => item.ObjectId)
                .ToHashSet());
        return true;
    }

    public bool TryBeginDrag(
        SelectionReference target,
        DocumentPoint pointer,
        RuntimeLayoutDocument layout,
        Guid? orbitParentPoleId = null,
        DrawingDocument? document = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureInactive();

        if (target.Kind == SelectionTargetKind.Device &&
            layout.DrawingLayout.Poles.TryGetValue(target.ObjectId, out PoleLayout? pole))
        {
            _drag = new PoleDragState(target, pointer, layout, pole, pole, pole);
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
                cabinet,
                cabinet);
            return true;
        }

        if (target.Kind == SelectionTargetKind.Device &&
            document?.Transformers.SingleOrDefault(item => item.Id == target.ObjectId)
                is Transformer transformer &&
            layout.TransformerLayouts.TryGetValue(
                target.ObjectId,
                out TransformerLayout? transformerLayout))
        {
            _drag = new TransformerDragState(
                target,
                pointer,
                layout,
                transformer.TransformerKind,
                transformerLayout,
                transformerLayout,
                transformerLayout);
            return true;
        }

        if (target.Kind == SelectionTargetKind.Device &&
            document?.CustomerStations.SingleOrDefault(item => item.Id == target.ObjectId)
                is CustomerStation station &&
            layout.CustomerStationLayouts.TryGetValue(
                target.ObjectId,
                out CustomerStationLayout? stationLayout))
        {
            _drag = new CustomerStationDragState(
                target,
                pointer,
                layout,
                station,
                stationLayout,
                stationLayout,
                stationLayout);
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
        DragCandidateGuard.EnsureFinite(pointer);

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
            DragCandidateGuard.EnsureFinite(offset);
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
        DragCandidateGuard.EnsureFinite(position);
        position = _snapService.Snap(drag.Target, position, drag.Layout);
        DragCandidateGuard.EnsureFinite(position);
        if (position == drag.CurrentPosition)
        {
            return false;
        }

        _drag = drag switch
        {
            PoleDragState pole => UpdatePole(pole, position),
            RingCabinetDragState cabinet => UpdateRingCabinet(cabinet, position),
            TransformerDragState transformer => UpdateTransformer(transformer, position),
            CustomerStationDragState station => UpdateCustomerStation(station, position),
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
        if (drag.StartPosition == drag.LastValidPosition)
        {
            RestoreBefore(drag);
            return null;
        }

        ICommand command = drag switch
        {
            GroupDragState group => new GroupMoveCommand(
                group.Layout,
                group.Before,
                group.LastValid),
            PoleDragState pole => new MoveCommand(
                pole.Layout.DrawingLayout,
                pole.Before,
                pole.LastValid),
            RingCabinetDragState cabinet => new MoveRingCabinetCommand(
                cabinet.Layout,
                cabinet.Before.CabinetId,
                cabinet.Before.Position,
                cabinet.LastValid.Position),
            TransformerDragState transformer => new MoveTransformerCommand(
                transformer.Layout,
                transformer.Before.TransformerId,
                transformer.TransformerKind,
                transformer.Before.Position,
                transformer.LastValid.Position),
            CustomerStationDragState station => new MoveCustomerStationCommand(
                station.Layout,
                station.Station,
                station.Before.Position,
                station.LastValid.Position),
            AttachmentDragState attachment => new MoveAttachmentCommand(
                attachment.Layout.DrawingLayout,
                attachment.Before.AttachmentId,
                attachment.Before.Offset,
                attachment.LastValid.Offset),
            _ => throw new InvalidOperationException("Unsupported device drag state.")
        };
        RestoreBefore(drag);
        return command;
    }

    public void AcceptCurrentPreview()
    {
        _drag = _drag switch
        {
            GroupDragState group => group with { LastValid = group.Current },
            PoleDragState pole => pole with { LastValid = pole.Current },
            RingCabinetDragState cabinet => cabinet with { LastValid = cabinet.Current },
            TransformerDragState transformer => transformer with
            {
                LastValid = transformer.Current
            },
            CustomerStationDragState station => station with { LastValid = station.Current },
            AttachmentDragState attachment => attachment with { LastValid = attachment.Current },
            null => throw new InvalidOperationException("No device drag is active."),
            _ => throw new InvalidOperationException("Unsupported device drag state.")
        };
    }

    public bool RollbackToLastValid()
    {
        DragState drag = _drag ?? throw new InvalidOperationException(
            "No device drag is active.");
        bool changed = drag.CurrentPosition != drag.LastValidPosition;
        ApplyLastValid(drag);
        _drag = drag switch
        {
            GroupDragState group => group with { Current = group.LastValid },
            PoleDragState pole => pole with { Current = pole.LastValid },
            RingCabinetDragState cabinet => cabinet with { Current = cabinet.LastValid },
            TransformerDragState transformer => transformer with
            {
                Current = transformer.LastValid
            },
            CustomerStationDragState station => station with { Current = station.LastValid },
            AttachmentDragState attachment => attachment with { Current = attachment.LastValid },
            _ => throw new InvalidOperationException("Unsupported device drag state.")
        };
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
            case TransformerDragState transformer:
                transformer.Layout.ReplaceTransformer(
                    transformer.Before,
                    transformer.TransformerKind);
                break;
            case CustomerStationDragState station:
                station.Layout.ReplaceCustomerStation(station.Before, station.Station);
                break;
            case AttachmentDragState attachment:
                attachment.Layout.DrawingLayout.Replace(attachment.Before);
                break;
            default:
                throw new InvalidOperationException("Unsupported device drag state.");
        }
    }

    private static void ApplyLastValid(DragState drag)
    {
        switch (drag)
        {
            case GroupDragState group:
                GroupMoveCommand.Apply(group.Layout, group.LastValid);
                break;
            case PoleDragState pole:
                pole.Layout.DrawingLayout.Replace(pole.LastValid);
                break;
            case RingCabinetDragState cabinet:
                cabinet.Layout.ReplaceRingCabinet(cabinet.LastValid);
                break;
            case TransformerDragState transformer:
                transformer.Layout.ReplaceTransformer(
                    transformer.LastValid,
                    transformer.TransformerKind);
                break;
            case CustomerStationDragState station:
                station.Layout.ReplaceCustomerStation(station.LastValid, station.Station);
                break;
            case AttachmentDragState attachment:
                attachment.Layout.DrawingLayout.Replace(attachment.LastValid);
                break;
            default:
                throw new InvalidOperationException("Unsupported device drag state.");
        }
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

    private static TransformerDragState UpdateTransformer(
        TransformerDragState drag,
        DocumentPoint position)
    {
        TransformerLayout preview = drag.Before.MoveTo(
            position,
            drag.TransformerKind);
        drag.Layout.ReplaceTransformer(preview, drag.TransformerKind);
        return drag with { Current = preview };
    }

    private static CustomerStationDragState UpdateCustomerStation(
        CustomerStationDragState drag,
        DocumentPoint position)
    {
        CustomerStationLayout preview = drag.Before.MoveTo(
            position,
            drag.Station);
        drag.Layout.ReplaceCustomerStation(preview, drag.Station);
        return drag with { Current = preview };
    }

    private bool UpdateGroup(GroupDragState drag, DocumentPoint pointer)
    {
        DocumentPoint rawDelta = Delta(pointer, drag.StartPointer);
        DocumentPoint anchorCandidate = Translate(drag.AnchorStartPosition, rawDelta);
        DragCandidateGuard.EnsureFinite(anchorCandidate);
        DocumentPoint snappedAnchor = _snapService.Snap(
            drag.AnchorRoot.SelectionReference,
            anchorCandidate,
            drag.Layout,
            drag.ExcludedSnapObjectIds);
        DragCandidateGuard.EnsureFinite(snappedAnchor);
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
        DrawingDocument document,
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
                .ToArray()),
            Array.AsReadOnly(values
                .Where(item => item.Kind == SelectionMoveRootKind.Transformer)
                .Select(item =>
                {
                    Transformer transformer = document.Transformers.Single(value =>
                        value.Id == item.ObjectId);
                    return new TransformerGroupMoveLayout(
                        layout.TransformerLayouts[item.ObjectId],
                        transformer.TransformerKind);
                })
                .ToArray()),
            Array.AsReadOnly(values
                .Where(item => item.Kind == SelectionMoveRootKind.CustomerStation)
                .Select(item =>
                {
                    CustomerStation station = document.CustomerStations.Single(value =>
                        value.Id == item.ObjectId);
                    return new CustomerStationGroupMoveLayout(
                        layout.CustomerStationLayouts[item.ObjectId],
                        station);
                })
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
            SelectionMoveRootKind.Transformer => state.Transformers.Single(item =>
                item.Layout.TransformerId == root.ObjectId).Layout.Position,
            SelectionMoveRootKind.CustomerStation => state.CustomerStations.Single(item =>
                item.Layout.CustomerStationId == root.ObjectId).Layout.Position,
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
            item.MoveTo(Translate(item.Offset, delta))).ToArray()),
        Array.AsReadOnly(state.Transformers.Select(item =>
            new TransformerGroupMoveLayout(
                item.Layout.MoveTo(
                    Translate(item.Layout.Position, delta),
                    item.TransformerKind),
                item.TransformerKind)).ToArray()),
        Array.AsReadOnly(state.CustomerStations.Select(item =>
            new CustomerStationGroupMoveLayout(
                item.Layout.MoveTo(
                    Translate(item.Layout.Position, delta),
                    item.Station),
                item.Station)).ToArray()));

    private static DocumentPoint Delta(DocumentPoint value, DocumentPoint origin) => new(
        value.XMillimeters - origin.XMillimeters,
        value.YMillimeters - origin.YMillimeters);

    private static DocumentPoint Translate(DocumentPoint value, DocumentPoint delta)
    {
        DocumentPoint result = new(
            value.XMillimeters + delta.XMillimeters,
            value.YMillimeters + delta.YMillimeters);
        DragCandidateGuard.EnsureFinite(result);
        return result;
    }

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

        public abstract DocumentPoint LastValidPosition { get; }
    }

    private sealed record PoleDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        PoleLayout Before,
        PoleLayout Current,
        PoleLayout LastValid)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DocumentPoint LastValidPosition => LastValid.Position;
    }

    private sealed record RingCabinetDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        RingCabinetLayout Before,
        RingCabinetLayout Current,
        RingCabinetLayout LastValid)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DocumentPoint LastValidPosition => LastValid.Position;
    }

    private sealed record TransformerDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        TransformerKind TransformerKind,
        TransformerLayout Before,
        TransformerLayout Current,
        TransformerLayout LastValid)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DocumentPoint LastValidPosition => LastValid.Position;
    }

    private sealed record CustomerStationDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        CustomerStation Station,
        CustomerStationLayout Before,
        CustomerStationLayout Current,
        CustomerStationLayout LastValid)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Position;

        public override DocumentPoint CurrentPosition => Current.Position;

        public override DocumentPoint LastValidPosition => LastValid.Position;
    }

    private sealed record AttachmentDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        PoleLayout ParentPole,
        AttachmentLayout Before,
        AttachmentLayout Current,
        AttachmentLayout LastValid,
        bool OrbitAroundPole)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => Before.Offset;

        public override DocumentPoint CurrentPosition => Current.Offset;

        public override DocumentPoint LastValidPosition => LastValid.Offset;
    }

    private sealed record GroupDragState(
        SelectionReference Target,
        DocumentPoint StartPointer,
        RuntimeLayoutDocument Layout,
        GroupMoveLayoutState Before,
        GroupMoveLayoutState Current,
        GroupMoveLayoutState LastValid,
        SelectionMoveRoot AnchorRoot,
        DocumentPoint AnchorStartPosition,
        IReadOnlySet<Guid> ExcludedSnapObjectIds)
        : DragState(Target, StartPointer, Layout)
    {
        public override DocumentPoint StartPosition => AnchorStartPosition;

        public override DocumentPoint CurrentPosition => GetRootPosition(
            AnchorRoot,
            Current);

        public override DocumentPoint LastValidPosition => GetRootPosition(
            AnchorRoot,
            LastValid);
    }
}
