using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class CableTerminationAttachmentCreationFactory
{
    public CableTerminationAttachmentCreation Create(
        Guid poleId,
        string? displayName,
        DocumentPoint attachmentOffset)
    {
        Guid cableTerminationId = Guid.NewGuid();
        Guid cableSideTerminalId = Guid.NewGuid();
        Guid overheadSideTerminalId = Guid.NewGuid();
        Guid internalNodeId = Guid.NewGuid();
        Guid attachmentId = Guid.NewGuid();

        var cableTermination = new CableTermination(
            cableTerminationId,
            cableSideTerminalId,
            overheadSideTerminalId,
            internalNodeId,
            displayName);
        var internalNode = new ElectricalNode(
            internalNodeId,
            ElectricalNodeType.Intermediate,
            TopologyOwnerType.Device,
            cableTerminationId);
        var cableSideTerminal = new Terminal(
            cableSideTerminalId,
            TopologyOwnerType.Device,
            cableTerminationId,
            CableTermination.CableSideRole,
            cableTermination.VoltageLevel,
            isExternal: true,
            allowsMultipleConnections: false,
            electricalNodeId: internalNodeId,
            allowedConnectionTypes: [ConnectionType.Cable]);
        var overheadSideTerminal = new Terminal(
            overheadSideTerminalId,
            TopologyOwnerType.Device,
            cableTerminationId,
            CableTermination.OverheadSideRole,
            cableTermination.VoltageLevel,
            isExternal: true,
            allowsMultipleConnections: false,
            electricalNodeId: internalNodeId,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        var attachment = new PoleAttachment(
            attachmentId,
            poleId,
            cableTerminationId);

        return new CableTerminationAttachmentCreation(
            cableTermination,
            internalNode,
            cableSideTerminal,
            overheadSideTerminal,
            attachment,
            new AttachmentLayout(attachmentId, attachmentOffset));
    }
}
