using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Devices;

public sealed class PoleCreationFactory
{
    public PoleCreationResult Create(
        string poleNumber,
        PoleType poleType = PoleType.Cement,
        string? displayName = null)
    {
        return CreateWithAttachments(
            poleNumber,
            poleType,
            displayName,
            switchKinds: null,
            includeCableTerminal: false);
    }

    public PoleCreationResult CreateWithAttachments(
        string poleNumber,
        PoleType poleType,
        string? displayName,
        IEnumerable<SwitchKind>? switchKinds,
        bool includeCableTerminal,
        string? cableTerminalDisplayName = null)
    {
        var pole = new Pole(
            Guid.NewGuid(),
            poleNumber,
            displayName,
            poleType);

        var attachments = new List<PoleAttachment>();
        var devices = new List<Device>();
        var terminals = new List<Terminal>();
        var electricalNodes = new List<ElectricalNode>();
        SwitchKind[] requestedSwitchKinds = (switchKinds ?? []).ToArray();
        bool needsJunction = requestedSwitchKinds.Length > 0 || includeCableTerminal;
        Guid? poleJunctionNodeId = needsJunction ? Guid.NewGuid() : null;
        if (poleJunctionNodeId is Guid junctionNodeId)
        {
            electricalNodes.Add(new ElectricalNode(
                junctionNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                pole.Id));
        }
        Terminal poleAnchor = pole.CreateOverheadAnchorTerminal(
            Guid.NewGuid(),
            allowsMultipleConnections: true,
            poleJunctionNodeId);

        foreach (SwitchKind switchKind in requestedSwitchKinds)
        {
            Guid switchId = Guid.NewGuid();
            Guid firstTerminalId = Guid.NewGuid();
            Guid secondTerminalId = Guid.NewGuid();
            Guid rightElectricalNodeId = Guid.NewGuid();
            electricalNodes.Add(new ElectricalNode(
                rightElectricalNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                switchId));
            var switchDevice = SwitchDevice.CreateForPole(
                switchId,
                switchKind,
                firstTerminalId,
                secondTerminalId);
            var firstTerminal = CreateSwitchTerminal(
                firstTerminalId,
                switchId,
                "SwitchLeftTerminal",
                allowsMultipleConnections: true);
            var secondTerminal = CreateSwitchTerminal(
                secondTerminalId,
                switchId,
                "SwitchRightTerminal",
                allowsMultipleConnections: false,
                rightElectricalNodeId);

            devices.Add(switchDevice);
            terminals.Add(firstTerminal);
            terminals.Add(secondTerminal);
            attachments.Add(new PoleAttachment(
                Guid.NewGuid(),
                pole.Id,
                switchId));
        }

        if (includeCableTerminal)
        {
            AddCableTerminal(
                pole,
                cableTerminalDisplayName,
                devices,
                terminals,
                electricalNodes,
                attachments);
        }

        // Keep the pole anchor last so existing attachment result ordering
        // remains stable while every production pole creation path exposes
        // the same center terminal.
        terminals.Add(poleAnchor);

        return new PoleCreationResult(
            pole,
            attachments,
            devices,
            terminals,
            electricalNodes);
    }

    private static Terminal CreateSwitchTerminal(
        Guid terminalId,
        Guid switchId,
        string role,
        bool allowsMultipleConnections,
        Guid? electricalNodeId = null)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            switchId,
            role,
            "10kV",
            isExternal: true,
            allowsMultipleConnections,
            electricalNodeId,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
    }

    private static void AddCableTerminal(
        Pole pole,
        string? displayName,
        List<Device> devices,
        List<Terminal> terminals,
        List<ElectricalNode> electricalNodes,
        List<PoleAttachment> attachments)
    {
        Guid cableTerminationId = Guid.NewGuid();
        Guid cableSideTerminalId = Guid.NewGuid();
        Guid overheadSideTerminalId = Guid.NewGuid();
        Guid internalNodeId = Guid.NewGuid();

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

        devices.Add(cableTermination);
        terminals.Add(cableSideTerminal);
        terminals.Add(overheadSideTerminal);
        electricalNodes.Add(internalNode);
        attachments.Add(new PoleAttachment(
            Guid.NewGuid(),
            pole.Id,
            cableTerminationId));
    }
}
