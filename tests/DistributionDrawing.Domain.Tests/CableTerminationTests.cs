using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class CableTerminationTests
{
    [Fact]
    public void Cable_termination_exposes_cable_and_overhead_terminals()
    {
        var document = TestFixtures.CreateDocument();
        var termination = TestFixtures.CreateCableTermination();

        TestFixtures.AddCableTerminationTopology(document, termination);

        Assert.Contains(termination.CableSideTerminalId, termination.TerminalIds);
        Assert.Contains(termination.OverheadSideTerminalId, termination.TerminalIds);
        Assert.Equal(2, document.Terminals.Count);
        Assert.Equal(ConnectionType.Cable, document.Terminals
            .Single(terminal => terminal.Id == termination.CableSideTerminalId)
            .AllowedConnectionTypes.Single());
        Assert.Equal(ConnectionType.OverheadLine, document.Terminals
            .Single(terminal => terminal.Id == termination.OverheadSideTerminalId)
            .AllowedConnectionTypes.Single());
    }

    [Fact]
    public void Cable_termination_internal_fixed_conduction_is_one_electrical_node_not_a_connection()
    {
        var document = TestFixtures.CreateDocument();
        var termination = TestFixtures.CreateCableTermination();

        TestFixtures.AddCableTerminationTopology(document, termination);

        ElectricalNode node = Assert.Single(document.ElectricalNodes);
        Assert.Equal(termination.InternalNodeId, node.Id);
        Assert.Equal(TopologyOwnerType.Device, node.OwnerType);
        Assert.Equal(termination.Id, node.OwnerId);
        Assert.True(node.TerminalIds.SetEquals(
            [termination.CableSideTerminalId, termination.OverheadSideTerminalId]));
        Assert.Empty(document.Connections);
    }

    [Fact]
    public void Cable_termination_rejects_a_terminal_with_the_wrong_side_policy()
    {
        var document = TestFixtures.CreateDocument();
        var termination = TestFixtures.CreateCableTermination();

        document.AddDevice(termination);
        document.AddElectricalNode(TestFixtures.CreateCableTerminationNode(termination));

        var wrongTerminal = new Terminal(
            termination.CableSideTerminalId,
            TopologyOwnerType.Device,
            termination.Id,
            "CableSide",
            TestFixtures.TenKilovolts,
            true,
            false,
            termination.InternalNodeId,
            [ConnectionType.OverheadLine]);

        Assert.Throws<InvalidOperationException>(() => document.AddTerminal(wrongTerminal));
    }
}
