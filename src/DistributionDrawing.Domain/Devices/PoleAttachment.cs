namespace DistributionDrawing.Domain.Devices;

public sealed class PoleAttachment
{
    public PoleAttachment(Guid attachmentId, Guid poleId, Guid attachedDeviceId)
    {
        if (attachmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attachment ID cannot be empty.",
                nameof(attachmentId));
        }

        if (poleId == Guid.Empty)
        {
            throw new ArgumentException("Pole ID cannot be empty.", nameof(poleId));
        }

        if (attachedDeviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attached device ID cannot be empty.",
                nameof(attachedDeviceId));
        }

        if (poleId == attachedDeviceId)
        {
            throw new ArgumentException(
                "A pole cannot be attached to itself.",
                nameof(attachedDeviceId));
        }

        AttachmentId = attachmentId;
        PoleId = poleId;
        AttachedDeviceId = attachedDeviceId;
    }

    public Guid AttachmentId { get; }

    public Guid PoleId { get; }

    public Guid AttachedDeviceId { get; }
}
