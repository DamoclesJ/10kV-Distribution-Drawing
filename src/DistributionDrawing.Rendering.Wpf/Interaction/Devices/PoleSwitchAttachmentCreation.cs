using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class PoleSwitchAttachmentCreation
{
    public PoleSwitchAttachmentCreation(
        SwitchDevice switchDevice,
        Terminal firstTerminal,
        Terminal secondTerminal,
        PoleAttachment attachment,
        AttachmentLayout layout)
    {
        SwitchDevice = switchDevice ?? throw new ArgumentNullException(nameof(switchDevice));
        FirstTerminal = firstTerminal ?? throw new ArgumentNullException(nameof(firstTerminal));
        SecondTerminal = secondTerminal ?? throw new ArgumentNullException(nameof(secondTerminal));
        Attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));

        if (!switchDevice.OwnsTerminal(firstTerminal.Id) ||
            !switchDevice.OwnsTerminal(secondTerminal.Id) ||
            firstTerminal.Id == secondTerminal.Id ||
            attachment.AttachedDeviceId != switchDevice.Id ||
            attachment.AttachmentId != layout.AttachmentId)
        {
            throw new ArgumentException("柱上开关、端子、附着关系和布局不一致。", nameof(attachment));
        }
    }

    public SwitchDevice SwitchDevice { get; }

    public Terminal FirstTerminal { get; }

    public Terminal SecondTerminal { get; }

    public PoleAttachment Attachment { get; }

    public AttachmentLayout Layout { get; }
}
