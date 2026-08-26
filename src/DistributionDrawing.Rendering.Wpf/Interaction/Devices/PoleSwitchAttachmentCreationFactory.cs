using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class PoleSwitchAttachmentCreationFactory
{
    public PoleSwitchAttachmentCreation Create(
        Guid poleId,
        SwitchKind switchKind,
        DocumentPoint attachmentOffset)
    {
        if (switchKind is SwitchKind.GroundSwitch)
        {
            throw new ArgumentException("当前不支持单独创建接地刀闸柱上附着设备。", nameof(switchKind));
        }

        Guid switchId = Guid.NewGuid();
        Guid firstTerminalId = Guid.NewGuid();
        Guid secondTerminalId = Guid.NewGuid();
        Guid rightElectricalNodeId = Guid.NewGuid();
        var switchDevice = SwitchDevice.CreateForPole(
            switchId,
            switchKind,
            firstTerminalId,
            secondTerminalId);
        var firstTerminal = CreateTerminal(firstTerminalId, switchId, "SwitchLeftTerminal", true);
        var secondTerminal = CreateTerminal(
            secondTerminalId,
            switchId,
            "SwitchRightTerminal",
            false,
            rightElectricalNodeId);
        var attachment = new PoleAttachment(Guid.NewGuid(), poleId, switchId);

        return new PoleSwitchAttachmentCreation(
            switchDevice,
            firstTerminal,
            secondTerminal,
            attachment,
            new AttachmentLayout(attachment.AttachmentId, attachmentOffset));
    }

    private static Terminal CreateTerminal(
        Guid id,
        Guid ownerId,
        string role,
        bool allowsMultipleConnections,
        Guid? electricalNodeId = null) => new(
        id,
        TopologyOwnerType.Device,
        ownerId,
        role,
        "10kV",
        isExternal: true,
        allowsMultipleConnections,
        electricalNodeId,
        allowedConnectionTypes: [ConnectionType.OverheadLine]);
}
