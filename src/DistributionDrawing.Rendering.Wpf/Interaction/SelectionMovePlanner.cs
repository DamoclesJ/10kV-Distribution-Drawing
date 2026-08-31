using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public enum SelectionMoveRootKind
{
    Pole,
    RingCabinet,
    PoleAttachment
}

public sealed record SelectionMoveRoot(
    SelectionMoveRootKind Kind,
    Guid ObjectId,
    Guid? ParentPoleId = null)
{
    public SelectionReference SelectionReference => Kind switch
    {
        SelectionMoveRootKind.Pole => new SelectionReference(
            SelectionTargetKind.Device,
            ObjectId),
        SelectionMoveRootKind.RingCabinet => new SelectionReference(
            SelectionTargetKind.RingCabinet,
            ObjectId),
        SelectionMoveRootKind.PoleAttachment => new SelectionReference(
            SelectionTargetKind.PoleAttachment,
            ObjectId,
            ParentPoleId),
        _ => throw new InvalidOperationException("Unsupported selection move root.")
    };
}

public sealed record SelectionMovePlan(
    IReadOnlyList<SelectionMoveRoot> Roots,
    SelectionMoveRoot? DragAnchorRoot)
{
    public bool CanMove => Roots.Count > 0 && DragAnchorRoot is not null;
}

/// <summary>
/// Converts a selection projection into unique runtime-layout roots. Parent
/// aggregates win over their children so one physical object receives one delta.
/// </summary>
public sealed class SelectionMovePlanner
{
    public SelectionMovePlan Create(
        SelectionSet selection,
        SelectionReference dragTarget,
        DrawingDocument document,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(dragTarget);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);

        var poleIds = new HashSet<Guid>();
        var cabinetIds = new HashSet<Guid>();
        var attachmentIds = new HashSet<Guid>();

        foreach (SelectionReference reference in selection.SelectedReferences)
        {
            AddCandidate(
                reference,
                document,
                layout,
                poleIds,
                cabinetIds,
                attachmentIds);
        }

        PoleAttachment[] attachments = document.PoleAttachments
            .Where(item => attachmentIds.Contains(item.AttachmentId))
            .ToArray();
        attachmentIds.ExceptWith(attachments
            .Where(item => poleIds.Contains(item.PoleId))
            .Select(item => item.AttachmentId));

        SelectionMoveRoot[] roots = poleIds.OrderBy(id => id)
            .Select(id => new SelectionMoveRoot(SelectionMoveRootKind.Pole, id))
            .Concat(cabinetIds.OrderBy(id => id)
                .Select(id => new SelectionMoveRoot(
                    SelectionMoveRootKind.RingCabinet,
                    id)))
            .Concat(attachments
                .Where(item => attachmentIds.Contains(item.AttachmentId))
                .OrderBy(item => item.AttachmentId)
                .Select(item => new SelectionMoveRoot(
                    SelectionMoveRootKind.PoleAttachment,
                    item.AttachmentId,
                    item.PoleId)))
            .ToArray();

        SelectionMoveRoot? anchor = ResolveRoot(
            dragTarget,
            roots,
            document);
        if (anchor is null && selection.PrimarySelection is not null)
        {
            anchor = ResolveRoot(selection.PrimarySelection, roots, document);
        }

        anchor ??= roots.FirstOrDefault();
        return new SelectionMovePlan(Array.AsReadOnly(roots), anchor);
    }

    public PoleAttachment? ResolvePoleAttachment(
        SelectionReference reference,
        DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(document);

        if (reference.Kind == SelectionTargetKind.PoleAttachment)
        {
            return document.PoleAttachments.SingleOrDefault(item =>
                item.AttachmentId == reference.ObjectId);
        }

        if (reference.Kind != SelectionTargetKind.Device)
        {
            return null;
        }

        return document.PoleAttachments.SingleOrDefault(item =>
            item.AttachedDeviceId == reference.ObjectId);
    }

    private void AddCandidate(
        SelectionReference reference,
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        ISet<Guid> poleIds,
        ISet<Guid> cabinetIds,
        ISet<Guid> attachmentIds)
    {
        switch (reference.Kind)
        {
            case SelectionTargetKind.Device:
                if (layout.DrawingLayout.Poles.ContainsKey(reference.ObjectId))
                {
                    poleIds.Add(reference.ObjectId);
                    return;
                }

                if (document.Devices.SingleOrDefault(item =>
                        item.Id == reference.ObjectId) is RingCabinet cabinet &&
                    layout.RingCabinetLayouts.ContainsKey(cabinet.Id))
                {
                    cabinetIds.Add(cabinet.Id);
                    return;
                }

                PoleAttachment? attachment = ResolvePoleAttachment(reference, document);
                if (attachment is not null &&
                    document.Devices.SingleOrDefault(item =>
                        item.Id == attachment.AttachedDeviceId) is not SwitchDevice &&
                    layout.DrawingLayout.Attachments.ContainsKey(attachment.AttachmentId))
                {
                    attachmentIds.Add(attachment.AttachmentId);
                }
                return;

            case SelectionTargetKind.RingCabinet:
                if (layout.RingCabinetLayouts.ContainsKey(reference.ObjectId))
                {
                    cabinetIds.Add(reference.ObjectId);
                }
                return;

            case SelectionTargetKind.PoleAttachment:
                PoleAttachment? selectedAttachment = ResolvePoleAttachment(reference, document);
                if (selectedAttachment is not null &&
                    document.Devices.SingleOrDefault(item =>
                        item.Id == selectedAttachment.AttachedDeviceId) is not SwitchDevice &&
                    layout.DrawingLayout.Attachments.ContainsKey(reference.ObjectId))
                {
                    attachmentIds.Add(reference.ObjectId);
                }
                return;

            case SelectionTargetKind.RingCabinetInterval:
                // Intervals and internal switches have no independent absolute layout.
                return;

            case SelectionTargetKind.Connection:
            case SelectionTargetKind.CableSegment:
            case SelectionTargetKind.Terminal:
            case SelectionTargetKind.IntermediateTerminal:
            case SelectionTargetKind.GroundingPoint:
            case SelectionTargetKind.WorkScope:
                return;

            default:
                return;
        }
    }

    private SelectionMoveRoot? ResolveRoot(
        SelectionReference reference,
        IReadOnlyList<SelectionMoveRoot> roots,
        DrawingDocument document)
    {
        if (reference.Kind == SelectionTargetKind.Device &&
            roots.Any(item => item.Kind == SelectionMoveRootKind.Pole &&
                              item.ObjectId == reference.ObjectId))
        {
            return roots.Single(item => item.Kind == SelectionMoveRootKind.Pole &&
                item.ObjectId == reference.ObjectId);
        }

        if (reference.Kind == SelectionTargetKind.RingCabinet)
        {
            return roots.SingleOrDefault(item =>
                item.Kind == SelectionMoveRootKind.RingCabinet &&
                item.ObjectId == reference.ObjectId);
        }

        PoleAttachment? attachment = ResolvePoleAttachment(reference, document);
        if (attachment is null)
        {
            return null;
        }

        return roots.SingleOrDefault(item =>
                   item.Kind == SelectionMoveRootKind.PoleAttachment &&
                   item.ObjectId == attachment.AttachmentId)
               ?? roots.SingleOrDefault(item =>
                   item.Kind == SelectionMoveRootKind.Pole &&
                   item.ObjectId == attachment.PoleId);
    }
}
