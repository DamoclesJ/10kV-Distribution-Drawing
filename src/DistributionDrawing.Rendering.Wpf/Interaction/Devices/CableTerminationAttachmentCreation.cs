using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class CableTerminationAttachmentCreation
{
    public CableTerminationAttachmentCreation(
        CableTermination cableTermination,
        ElectricalNode internalNode,
        Terminal cableSideTerminal,
        Terminal overheadSideTerminal,
        PoleAttachment attachment,
        AttachmentLayout layout)
    {
        CableTermination = cableTermination ??
            throw new ArgumentNullException(nameof(cableTermination));
        InternalNode = internalNode ?? throw new ArgumentNullException(nameof(internalNode));
        CableSideTerminal = cableSideTerminal ??
            throw new ArgumentNullException(nameof(cableSideTerminal));
        OverheadSideTerminal = overheadSideTerminal ??
            throw new ArgumentNullException(nameof(overheadSideTerminal));
        Attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));

        if (Attachment.AttachedDeviceId != CableTermination.Id ||
            Attachment.AttachmentId != Layout.AttachmentId)
        {
            throw new ArgumentException(
                "Cable termination, attachment, and layout IDs must match.");
        }
    }

    public CableTermination CableTermination { get; }

    public ElectricalNode InternalNode { get; }

    public Terminal CableSideTerminal { get; }

    public Terminal OverheadSideTerminal { get; }

    public PoleAttachment Attachment { get; }

    public AttachmentLayout Layout { get; }
}
