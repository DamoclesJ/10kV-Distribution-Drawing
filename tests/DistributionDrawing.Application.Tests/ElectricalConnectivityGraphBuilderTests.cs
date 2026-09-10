using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class ElectricalConnectivityGraphBuilderTests
{
    [Theory]
    [InlineData(SwitchState.Open, false)]
    [InlineData(SwitchState.Closed, true)]
    public void CustomerStationIncomingFeeder_UsesExistingClosedSwitchTraversal(
        SwitchState state,
        bool expectedConnected)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "customer station graph");
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.BoxStation,
            ["主供"]);
        document.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        document.ChangeSwitchState(feeder.IsolationSwitch.Id, state);

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.Equal(expectedConnected,
            query.IsConnected(feeder.CableTerminalId, feeder.StationTerminalId));
        Assert.Equal(
            expectedConnected ? 1 : 0,
            graph.Edges.Count(edge =>
                edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch &&
                edge.SourceId == feeder.IsolationSwitch.Id));
        Assert.Contains(feeder.StationTerminalId, feeder.ElectricalNode.TerminalIds);
    }

    [Fact]
    public void IndoorStationDualFeeders_RemainDisconnectedWhenBothSwitchesAreClosed()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "dual customer station graph");
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供", "备供"]);
        document.AddCustomerStation(station);
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        document.ChangeSwitchState(first.IsolationSwitch.Id, SwitchState.Closed);
        document.ChangeSwitchState(second.IsolationSwitch.Id, SwitchState.Closed);

        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));

        Assert.True(query.IsConnected(first.CableTerminalId, first.StationTerminalId));
        Assert.True(query.IsConnected(second.CableTerminalId, second.StationTerminalId));
        Assert.False(query.IsConnected(first.CableTerminalId, second.CableTerminalId));
        Assert.False(query.IsConnected(first.StationTerminalId, second.StationTerminalId));

        document.ChangeSwitchState(first.IsolationSwitch.Id, SwitchState.Open);
        var changed = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));
        Assert.False(changed.IsConnected(first.CableTerminalId, first.StationTerminalId));
        Assert.True(changed.IsConnected(second.CableTerminalId, second.StationTerminalId));
    }

    [Fact]
    public void TransformerLeaf_IsIncludedWithoutInternalEdge()
    {
        (DrawingDocument document, Transformer transformer, Terminal terminal) =
            CreateTransformer(TransformerKind.PublicIndoor);

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder().Build(document);

        Assert.True(graph.ContainsTerminal(transformer.HvTerminalId));
        Assert.Empty(graph.Edges);
        Assert.Empty(document.ElectricalNodes);
        Assert.Equal(terminal.Id, transformer.HvTerminalId);
    }

    [Fact]
    public void CableConnection_ReachesPublicIndoorTransformerLeaf()
    {
        (DrawingDocument document, Transformer transformer, Terminal terminal) =
            CreateTransformer(TransformerKind.PublicIndoor);
        Guid sourceId = Guid.NewGuid();
        Guid sourceTerminalId = Guid.NewGuid();
        var sourceTransformer = new Transformer(
            sourceId, TransformerKind.PublicIndoor, sourceTerminalId);
        var source = new Terminal(
            sourceTerminalId, TopologyOwnerType.Device, sourceId,
            Transformer.HvTerminalRole, Transformer.TenKilovolts,
            true, false, null, [ConnectionType.Cable]);
        document.AddTransformer(sourceTransformer, source);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.Cable, source.Id, terminal.Id, "cable", "10kV");
        document.AddConnection(connection);

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder().Build(document);

        ElectricalConnectivityEdge edge = Assert.Single(graph.Edges);
        Assert.True(edge.Connects(source.Id, transformer.HvTerminalId));
        Assert.DoesNotContain(graph.Edges,
            item => item.Type == ElectricalConnectivityEdgeType.PassiveDeviceInternal &&
                    item.SourceId == transformer.Id);
    }

    [Fact]
    public void OverheadConnection_ReachesPoleMountedTransformerLeaf()
    {
        (DrawingDocument document, _, Terminal transformerTerminal) =
            CreateTransformer(TransformerKind.DedicatedPoleMounted);
        var pole = new Pole(Guid.NewGuid(), "P1");
        Terminal poleTerminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(pole);
        document.AddTerminal(poleTerminal);
        document.AddConnection(new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            poleTerminal.Id, transformerTerminal.Id, "overhead", "10kV"));

        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));

        Assert.True(query.IsConnected(poleTerminal.Id, transformerTerminal.Id));
    }

    [Theory]
    [InlineData(SwitchState.Open, false)]
    [InlineData(SwitchState.Closed, true)]
    public void DropoutFusePath_ReachesPoleMountedTransformerOnlyWhenClosed(
        SwitchState state,
        bool expectedConnected)
    {
        (DrawingDocument document, _, Terminal transformerTerminal) =
            CreateTransformer(TransformerKind.PublicPoleMounted);
        Guid switchId = Guid.NewGuid();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        SwitchDevice fuse = SwitchDevice.CreateForPole(
            switchId, SwitchKind.DropoutFuse, firstId, secondId, state,
            "跌落式熔断器", "10kV", null);
        document.AddDevice(fuse);
        var first = new Terminal(firstId, TopologyOwnerType.Device, switchId,
            "SwitchLeftTerminal", "10kV", true, false, null,
            [ConnectionType.OverheadLine]);
        var second = new Terminal(secondId, TopologyOwnerType.Device, switchId,
            "SwitchRightTerminal", "10kV", true, false, null,
            [ConnectionType.OverheadLine]);
        document.AddTerminal(first);
        document.AddTerminal(second);
        var sourcePole = new Pole(Guid.NewGuid(), "P1");
        Terminal source = sourcePole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddDevice(sourcePole);
        document.AddTerminal(source);
        document.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            source.Id, first.Id, "incoming", "10kV"));
        document.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            second.Id, transformerTerminal.Id, "short", "10kV"));

        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));

        Assert.Equal(expectedConnected,
            query.IsConnected(source.Id, transformerTerminal.Id));
    }

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

    private static (DrawingDocument, Transformer, Terminal) CreateTransformer(
        TransformerKind kind)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "transformer graph");
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(transformerId, kind, terminalId);
        var terminal = new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            transformerId,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            true,
            false,
            null,
            [transformer.AllowedConnectionType]);
        document.AddTransformer(transformer, terminal);
        return (document, transformer, terminal);
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
