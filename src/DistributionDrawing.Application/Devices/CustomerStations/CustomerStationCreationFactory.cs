using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Devices.CustomerStations;

public sealed class CustomerStationCreationFactory
{
    public CustomerStation Create(
        StationKind stationKind,
        IReadOnlyList<string> incomingFeederDisplayNames)
    {
        ArgumentNullException.ThrowIfNull(incomingFeederDisplayNames);

        IncomingFeeder[] feeders = incomingFeederDisplayNames
            .Select((displayName, index) => CreateIncomingFeeder(index + 1, displayName))
            .ToArray();
        return new CustomerStation(Guid.NewGuid(), stationKind, feeders);
    }

    private static IncomingFeeder CreateIncomingFeeder(int sequence, string displayName)
    {
        Guid incomingFeederId = Guid.NewGuid();
        Guid switchId = Guid.NewGuid();
        Guid cableTerminalId = Guid.NewGuid();
        Guid stationTerminalId = Guid.NewGuid();
        Guid electricalNodeId = Guid.NewGuid();
        SwitchDevice isolationSwitch =
            SwitchDevice.CreateForCustomerStationIncomingFeeder(
                switchId,
                incomingFeederId,
                cableTerminalId,
                stationTerminalId);
        var electricalNode = new ElectricalNode(
            electricalNodeId,
            ElectricalNodeType.Circuit,
            TopologyOwnerType.InternalAggregate,
            incomingFeederId);
        var cableTerminal = new Terminal(
            cableTerminalId,
            TopologyOwnerType.Device,
            switchId,
            IncomingFeeder.CableTerminalRole,
            IncomingFeeder.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: false,
            allowedConnectionTypes: [ConnectionType.Cable]);
        var stationTerminal = new Terminal(
            stationTerminalId,
            TopologyOwnerType.Device,
            switchId,
            IncomingFeeder.StationTerminalRole,
            IncomingFeeder.TenKilovolts,
            isExternal: false,
            allowsMultipleConnections: false,
            electricalNodeId);

        return new IncomingFeeder(
            incomingFeederId,
            sequence,
            displayName,
            cableTerminalId,
            stationTerminalId,
            electricalNodeId,
            isolationSwitch,
            cableTerminal,
            stationTerminal,
            electricalNode);
    }
}
