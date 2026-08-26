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
            candidate.Type == ElectricalConnectivityEdgeType.PassiveDeviceInternal);
        Assert.True(edge.Connects(firstTerminalId, secondTerminalId));
        Assert.Equal(3, graph.TerminalIds.Count);
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

    [Fact]
    public void Query_IsConnected_FollowsElectricalNodeAndConnectionEdges()
    {
        (DrawingDocument document, PoleCreationResult result) = CreateCableTermination();
        Pole secondPole = new(Guid.NewGuid(), "P-012");
        Terminal secondTerminal = secondPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(secondPole);
        document.AddTerminal(secondTerminal);
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            result.Terminals[1].Id,
            secondTerminal.Id,
            "架空连接",
            "10kV"));

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.True(query.IsConnected(result.Terminals[0].Id, secondTerminal.Id));
        Assert.Contains(
            result.Terminals[0].Id,
            query.FindConnectedTerminalIds(secondTerminal.Id));
    }

    [Fact]
    public void Query_DoesNotCrossOpenSwitch_AndCrossesClosedSwitch()
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleSwitch(
            SwitchState.Open);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(Assert.Single(result.Devices));
        ElectricalConnectivityGraph openGraph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var openQuery = new ElectricalConnectivityQuery(openGraph);

        Assert.False(openQuery.IsConnected(
            switchDevice.TerminalIds[0],
            switchDevice.TerminalIds[1]));

        document.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        ElectricalConnectivityGraph closedGraph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var closedQuery = new ElectricalConnectivityQuery(closedGraph);

        Assert.True(closedQuery.IsConnected(
            switchDevice.TerminalIds[0],
            switchDevice.TerminalIds[1]));
        Assert.False(openQuery.IsConnected(
            switchDevice.TerminalIds[0],
            switchDevice.TerminalIds[1]));
    }

    [Fact]
    public void Query_ReturnsImmutableConnectedSet_AndRejectsUnknownTerminal()
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleSwitch(
            SwitchState.Closed);
        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        IReadOnlySet<Guid> connected = query.FindConnectedTerminalIds(result.Terminals[0].Id);

        Assert.Contains(result.Terminals[0].Id, connected);
        Assert.Contains(result.Terminals[1].Id, connected);
        Assert.Throws<KeyNotFoundException>(() => query.IsConnected(
            Guid.NewGuid(),
            result.Terminals[0].Id));
    }

    [Fact]
    public void PoleAttachmentCreation_BindsPoleJunctionAndSwitchSidesToDistinctNodes()
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleWithSwitchAndCableTermination();
        Pole pole = result.Pole;
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices.OfType<SwitchDevice>()));
        CableTermination termination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices.OfType<CableTermination>()));

        Terminal poleTerminal = Assert.Single(document.Terminals, terminal =>
            pole.OwnsTerminal(terminal.Id));
        Terminal left = Assert.Single(document.Terminals, terminal =>
            terminal.OwnerId == switchDevice.Id && terminal.Role == "SwitchLeftTerminal");
        Terminal right = Assert.Single(document.Terminals, terminal =>
            terminal.OwnerId == switchDevice.Id && terminal.Role == "SwitchRightTerminal");
        Terminal overhead = Assert.Single(document.Terminals, terminal =>
            terminal.Id == termination.OverheadSideTerminalId);

        Assert.NotNull(poleTerminal.ElectricalNodeId);
        Assert.Equal(poleTerminal.ElectricalNodeId, left.ElectricalNodeId);
        Assert.Equal(poleTerminal.ElectricalNodeId, overhead.ElectricalNodeId);
        Assert.NotEqual(left.ElectricalNodeId, right.ElectricalNodeId);
        Assert.True(left.AllowsMultipleConnections);
        Assert.False(right.AllowsMultipleConnections);
        Assert.Equal(termination.InternalNodeId,
            document.Terminals.Single(terminal =>
                terminal.Id == termination.CableSideTerminalId).ElectricalNodeId);
    }

    [Theory]
    [InlineData(SwitchState.Open, false)]
    [InlineData(SwitchState.Closed, true)]
    public void PoleJunctionGraph_UsesSwitchStateToConnectNodeAAndNodeB(
        SwitchState state,
        bool expectedDownstreamConnectivity)
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleWithSwitchAndCableTermination(state);
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices.OfType<SwitchDevice>()));
        CableTermination termination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices.OfType<CableTermination>()));
        Pole downstreamPole = new(Guid.NewGuid(), "P-099");
        Terminal downstreamTerminal = downstreamPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(downstreamPole);
        document.AddTerminal(downstreamTerminal);
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            switchDevice.TerminalIds[1],
            downstreamTerminal.Id,
            "出线",
            "10kV"));
        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder().Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.Equal(
            expectedDownstreamConnectivity,
            query.IsConnected(termination.CableSideTerminalId, downstreamTerminal.Id));
    }

    [Fact]
    public void PoleJunctionGraph_PreservesTBranchConnectivityOnNodeAWhenSwitchIsOpen()
    {
        (DrawingDocument document, PoleCreationResult result) = CreatePoleWithSwitchAndCableTermination();
        SwitchDevice switchDevice = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices.OfType<SwitchDevice>()));
        CableTermination termination = Assert.IsType<CableTermination>(
            Assert.Single(result.Devices.OfType<CableTermination>()));
        Pole firstBranchPole = new(Guid.NewGuid(), "P-100");
        Pole secondBranchPole = new(Guid.NewGuid(), "P-101");
        Terminal firstBranchTerminal = firstBranchPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal secondBranchTerminal = secondBranchPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(firstBranchPole);
        document.AddDevice(secondBranchPole);
        document.AddTerminal(firstBranchTerminal);
        document.AddTerminal(secondBranchTerminal);
        Terminal poleTerminal = Assert.Single(document.Terminals, terminal =>
            terminal.OwnerId == result.Pole.Id);
        document.AddConnection(new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine, poleTerminal.Id,
            firstBranchTerminal.Id, "支线1", "10kV"));
        document.AddConnection(new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine, poleTerminal.Id,
            secondBranchTerminal.Id, "支线2", "10kV"));

        var query = new ElectricalConnectivityQuery(new ElectricalConnectivityGraphBuilder().Build(document));
        Assert.True(query.IsConnected(
            termination.CableSideTerminalId,
            firstBranchTerminal.Id));
        Assert.True(query.IsConnected(
            firstBranchTerminal.Id,
            secondBranchTerminal.Id));
        Assert.False(query.IsConnected(
            firstBranchTerminal.Id,
            switchDevice.TerminalIds[1]));
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

    private static (DrawingDocument Document, PoleCreationResult Result)
        CreatePoleWithSwitchAndCableTermination(
            SwitchState state = SwitchState.Open)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Pole junction test");
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-098",
            PoleType.Cement,
            null,
            [SwitchKind.CircuitBreaker],
            includeCableTerminal: true);
        SwitchDevice originalSwitch = Assert.IsType<SwitchDevice>(
            Assert.Single(result.Devices.OfType<SwitchDevice>()));
        if (state != originalSwitch.SwitchState)
        {
            originalSwitch = SwitchDevice.CreateForPole(
                originalSwitch.Id,
                originalSwitch.SwitchKind,
                originalSwitch.TerminalIds[0],
                originalSwitch.TerminalIds[1],
                state);
            result = new PoleCreationResult(
                result.Pole,
                result.Attachments,
                result.Devices.Select(device => device.Id == originalSwitch.Id
                    ? originalSwitch
                    : device),
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
