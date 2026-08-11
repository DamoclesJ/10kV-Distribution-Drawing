using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class TopologyBoundaryTests
{
    [Fact]
    public void Support_poles_do_not_create_extra_connections_or_nodes()
    {
        var document = TestFixtures.CreateDocument();
        var startPole = new Pole(Guid.NewGuid(), "P-30");
        var supportPole = new Pole(Guid.NewGuid(), "P-31");
        var endPole = new Pole(Guid.NewGuid(), "P-32");
        var startAnchor = TestFixtures.CreatePoleAnchorTerminal(startPole);
        var endAnchor = TestFixtures.CreatePoleAnchorTerminal(endPole);

        document.AddDevice(startPole);
        document.AddDevice(supportPole);
        document.AddDevice(endPole);
        document.AddTerminal(startAnchor);
        document.AddTerminal(endAnchor);

        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            startAnchor.Id,
            endAnchor.Id,
            "跨杆架空线",
            TestFixtures.TenKilovolts);
        document.AddConnection(connection);
        document.AddOverheadLine(
            new OverheadLine(
                connection.Id,
                "JKLYJ-10kV",
                [startPole.Id, supportPole.Id, endPole.Id]));

        Assert.Single(document.Connections);
        Assert.Single(document.OverheadLines);
        Assert.Empty(document.ElectricalNodes);
        Assert.Empty(supportPole.OverheadAnchorTerminalIds);
        Assert.Equal(startAnchor.Id, connection.StartTerminalId);
        Assert.Equal(endAnchor.Id, connection.EndTerminalId);
    }

    [Fact]
    public void Cable_termination_fixed_node_is_separate_from_external_cable_connection()
    {
        var document = TestFixtures.CreateDocument();
        var termination = TestFixtures.CreateCableTermination();
        TestFixtures.AddCableTerminationTopology(document, termination);

        var cabinet = RingCabinet.CreateNormalLoadSwitchCabinet(
            Guid.NewGuid(),
            "环网柜",
            3,
            SwitchState.Open,
            SwitchState.Open);
        document.AddDevice(cabinet);
        Terminal cabinetTerminal = document.Terminals.Single(
            terminal => terminal.Id == cabinet.Intervals[0].ExternalTerminalId);

        var cable = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            cabinetTerminal.Id,
            termination.CableSideTerminalId,
            "环网柜至电缆终端",
            TestFixtures.TenKilovolts);
        document.AddConnection(cable);

        Assert.Single(document.Connections);
        Assert.Equal(ConnectionType.Cable, document.Connections[0].Type);
        Assert.Equal(2, document.ElectricalNodes.Single(
            node => node.Id == termination.InternalNodeId).TerminalIds.Count);
        Assert.DoesNotContain(
            document.Connections,
            connection => connection.StartTerminalId == termination.CableSideTerminalId &&
                          connection.EndTerminalId == termination.OverheadSideTerminalId);
    }
}
