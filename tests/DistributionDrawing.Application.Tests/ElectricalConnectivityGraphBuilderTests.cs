using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class ElectricalConnectivityGraphBuilderTests
{
    [Fact]
    public void Build_AddsElectricalNodeInternalEdge()
    {
        (DrawingDocument document, PoleCreationResult result) = CreateCableTermination();

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        Guid firstTerminalId = result.Terminals[0].Id;
        Guid secondTerminalId = result.Terminals[1].Id;

        ElectricalConnectivityEdge edge = Assert.Single(graph.Edges, candidate =>
            candidate.Type == ElectricalConnectivityEdgeType.ElectricalNodeInternal);
        Assert.True(edge.Connects(firstTerminalId, secondTerminalId));
        Assert.Equal(2, graph.TerminalIds.Count);
    }

    [Fact]
    public void Build_AddsConnectionEdge()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Connectivity connection test");
        Pole firstPole = new(Guid.NewGuid(), "P-001");
        Pole secondPole = new(Guid.NewGuid(), "P-002");
        Terminal firstTerminal = firstPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal secondTerminal = secondPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(firstPole);
        document.AddDevice(secondPole);
        document.AddTerminal(firstTerminal);
        document.AddTerminal(secondTerminal);
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            firstTerminal.Id,
            secondTerminal.Id,
            "架空连接",
            "10kV");
        document.AddConnection(connection);

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);

        ElectricalConnectivityEdge edge = Assert.Single(graph.Edges);
        Assert.Equal(ElectricalConnectivityEdgeType.Connection, edge.Type);
        Assert.Equal(connection.Id, edge.SourceId);
        Assert.True(edge.Connects(firstTerminal.Id, secondTerminal.Id));
    }

    [Theory]
    [InlineData(SwitchState.Open, false)]
    [InlineData(SwitchState.Closed, true)]
    public void Build_AddsSwitchEdgeOnlyWhenClosed(
        SwitchState state,
        bool expectedEdge)
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleSwitch(state);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(Assert.Single(result.Devices));

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);

        ElectricalConnectivityEdge? edge = graph.Edges.SingleOrDefault(candidate =>
            candidate.Type == ElectricalConnectivityEdgeType.ClosedSwitch);
        Assert.Equal(expectedEdge, edge is not null);
        if (edge is not null)
        {
            Assert.Equal(switchDevice.Id, edge.SourceId);
            Assert.True(edge.Connects(
                switchDevice.TerminalIds[0],
                switchDevice.TerminalIds[1]));
        }
    }

    [Fact]
    public void Build_IncludesPoleSwitchTerminalsAndDoesNotMutateDomain()
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleSwitch(
            SwitchState.Closed);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(Assert.Single(result.Devices));
        int deviceCount = document.Devices.Count;
        int terminalCount = document.Terminals.Count;
        int nodeCount = document.ElectricalNodes.Count;
        SwitchState? stateBefore = switchDevice.SwitchState;
        Guid[] stableIdsBefore = document.Devices
            .Select(device => device.Id)
            .Concat(document.Terminals.Select(terminal => terminal.Id))
            .ToArray();

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);

        Assert.All(switchDevice.TerminalIds, terminalId =>
            Assert.True(graph.ContainsTerminal(terminalId)));
        Assert.Equal(deviceCount, document.Devices.Count);
        Assert.Equal(terminalCount, document.Terminals.Count);
        Assert.Equal(nodeCount, document.ElectricalNodes.Count);
        Assert.Equal(stateBefore, switchDevice.SwitchState);
        Assert.Equal(
            stableIdsBefore,
            document.Devices.Select(device => device.Id)
                .Concat(document.Terminals.Select(terminal => terminal.Id)));
    }

    private static (DrawingDocument Document, PoleCreationResult Result) CreatePoleSwitch(
        SwitchState state)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Connectivity pole switch test");
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-010",
            PoleType.Cement,
            null,
            [SwitchKind.LoadSwitch],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(Assert.Single(result.Devices));
        if (state != switchDevice.SwitchState)
        {
            switchDevice = SwitchDevice.CreateForPole(
                switchDevice.Id,
                switchDevice.SwitchKind,
                switchDevice.TerminalIds[0],
                switchDevice.TerminalIds[1],
                state);
            result = new PoleCreationResult(
                result.Pole,
                result.Attachments,
                [switchDevice],
                result.Terminals,
                result.ElectricalNodes);
        }

        new CreatePoleCommand(document, result).Execute();
        return (document, result);
    }

    private static (DrawingDocument Document, PoleCreationResult Result) CreateCableTermination()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Connectivity node test");
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-011",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        new CreatePoleCommand(document, result).Execute();
        return (document, result);
    }
}
