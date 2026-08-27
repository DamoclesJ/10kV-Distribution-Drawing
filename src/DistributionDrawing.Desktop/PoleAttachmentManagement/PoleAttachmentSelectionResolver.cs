using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.PoleAttachmentManagement;

public sealed class PoleAttachmentSelectionResolver
{
    public Guid? Resolve(
        ProjectRuntimeSession session,
        SelectionReference? selection,
        Guid? fallbackAttachmentId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (selection?.Kind == SelectionTargetKind.PoleAttachment &&
            IsAttachment(session, selection.ObjectId, selection.ParentId))
        {
            return selection.ObjectId;
        }

        if (selection?.Kind == SelectionTargetKind.Device)
        {
            PoleAttachment? attachment = selection.ParentId is Guid parentId
                ? session.PersistenceSession.Domain.PoleAttachments.SingleOrDefault(candidate =>
                    candidate.AttachmentId == parentId &&
                    candidate.AttachedDeviceId == selection.ObjectId)
                : session.PersistenceSession.Domain.PoleAttachments.SingleOrDefault(candidate =>
                    candidate.AttachedDeviceId == selection.ObjectId);
            if (attachment is not null &&
                session.PersistenceSession.Domain.Devices.SingleOrDefault(device =>
                    device.Id == attachment.AttachedDeviceId) is SwitchDevice)
            {
                return attachment.AttachmentId;
            }
        }

        return fallbackAttachmentId is Guid fallback &&
            IsAttachment(session, fallback, parentId: null)
            ? fallback
            : null;
    }

    private static bool IsAttachment(
        ProjectRuntimeSession session,
        Guid attachmentId,
        Guid? parentId)
    {
        PoleAttachment? attachment = session.PersistenceSession.Domain.PoleAttachments
            .SingleOrDefault(candidate => candidate.AttachmentId == attachmentId);
        return attachment is not null &&
            (parentId is null || parentId == attachment.PoleId);
    }
}
