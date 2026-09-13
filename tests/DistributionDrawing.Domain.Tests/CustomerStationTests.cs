using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class CustomerStationTests
{
    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void ValidAggregate_UsesFrozenDomainShape(StationKind kind, int feederCount)
    {
        CustomerStation station = CreateStation(kind, feederCount);

        Assert.Equal(DeviceType.CustomerStation, station.Type);
        Assert.Null(station.DisplayName);
        Assert.Equal(IncomingFeeder.TenKilovolts, station.VoltageLevel);
        Assert.Equal(feederCount, station.IncomingFeeders.Count);
        Assert.Equal(
            Enumerable.Range(1, feederCount),
            station.IncomingFeeders.Select(feeder => feeder.Sequence));
        Assert.All(station.IncomingFeeders, AssertFrozenFeederShape);
        Device stationAsDevice = station;
        Assert.Throws<InvalidOperationException>(() => stationAsDevice.Rename("重复站名"));
        Assert.Null(station.DisplayName);
    }

    [Fact]
    public void StationKind_EnforcesFrozenFeederCounts()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(Guid.NewGuid(), StationKind.BoxStation, []));
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(
                Guid.NewGuid(),
                StationKind.BoxStation,
                [CreateFeeder(1), CreateFeeder(2)]));
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(Guid.NewGuid(), StationKind.IndoorStation, []));
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(
                Guid.NewGuid(),
                StationKind.IndoorStation,
                [CreateFeeder(1), CreateFeeder(2), CreateFeeder(3)]));
    }

    [Fact]
    public void Sequence_MustBeUniqueContinuousAndStartAtOne()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(
                Guid.NewGuid(),
                StationKind.IndoorStation,
                [CreateFeeder(2)]));
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(
                Guid.NewGuid(),
                StationKind.IndoorStation,
                [CreateFeeder(1), CreateFeeder(1)]));
    }

    [Fact]
    public void DisplayName_IsRequiredAndTrimmed()
    {
        IncomingFeeder feeder = CreateFeeder(1, "  主供电源  ");

        Assert.Equal("主供电源", feeder.DisplayName);
        Assert.Throws<ArgumentException>(() => CreateFeeder(1, "  "));
    }

    [Fact]
    public void Aggregate_RejectsDuplicateStableIds()
    {
        Guid duplicateFeederId = Guid.NewGuid();
        IncomingFeeder first = CreateFeeder(1, incomingFeederId: duplicateFeederId);
        IncomingFeeder second = CreateFeeder(2, incomingFeederId: duplicateFeederId);

        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStation(
                Guid.NewGuid(),
                StationKind.IndoorStation,
                [first, second]));
    }

    [Fact]
    public void IncomingFeeder_RejectsInvalidSwitchKindOwnerAndTerminalOrder()
    {
        FeederParts parts = CreateFeederParts();
        SwitchDevice wrongKind = SwitchDevice.CreateForPole(
            parts.SwitchId,
            SwitchKind.LoadSwitch,
            parts.CableTerminalId,
            parts.StationTerminalId);
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(parts, wrongKind));

        parts = CreateFeederParts();
        SwitchDevice wrongOwner = SwitchDevice.CreateForCustomerStationIncomingFeeder(
            parts.SwitchId,
            Guid.NewGuid(),
            parts.CableTerminalId,
            parts.StationTerminalId);
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(parts, wrongOwner));

        parts = CreateFeederParts();
        SwitchDevice reversed = SwitchDevice.CreateForCustomerStationIncomingFeeder(
            parts.SwitchId,
            parts.IncomingFeederId,
            parts.StationTerminalId,
            parts.CableTerminalId);
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(parts, reversed));
    }

    [Fact]
    public void IncomingFeeder_RejectsInvalidNodeOwnerAndType()
    {
        FeederParts parts = CreateFeederParts();
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(
            parts,
            electricalNode: new ElectricalNode(
                parts.ElectricalNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.InternalAggregate,
                parts.IncomingFeederId)));

        parts = CreateFeederParts();
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(
            parts,
            electricalNode: new ElectricalNode(
                parts.ElectricalNodeId,
                ElectricalNodeType.Circuit,
                TopologyOwnerType.InternalAggregate,
                Guid.NewGuid())));
    }

    [Fact]
    public void IncomingFeeder_AcceptsExistingElectricalStateAsNonStructuralFact()
    {
        FeederParts parts = CreateFeederParts();
        IncomingFeeder feeder = CreateFeeder(
            parts,
            electricalNode: new ElectricalNode(
                parts.ElectricalNodeId,
                ElectricalNodeType.Circuit,
                TopologyOwnerType.InternalAggregate,
                parts.IncomingFeederId,
                ElectricalState.Energized));
        Assert.Equal(
            ElectricalState.Energized,
            feeder.ElectricalNode.ElectricalState);
    }

    [Fact]
    public void IncomingFeeder_RejectsInvalidTerminalContracts()
    {
        FeederParts parts = CreateFeederParts();
        var overheadCableTerminal = new Terminal(
            parts.CableTerminalId,
            TopologyOwnerType.Device,
            parts.SwitchId,
            IncomingFeeder.CableTerminalRole,
            IncomingFeeder.TenKilovolts,
            true,
            false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(
            parts,
            cableTerminal: overheadCableTerminal));

        parts = CreateFeederParts();
        var externalStationTerminal = new Terminal(
            parts.StationTerminalId,
            TopologyOwnerType.Device,
            parts.SwitchId,
            IncomingFeeder.StationTerminalRole,
            IncomingFeeder.TenKilovolts,
            true,
            false,
            parts.ElectricalNodeId,
            [ConnectionType.Cable]);
        Assert.Throws<InvalidOperationException>(() => CreateFeeder(
            parts,
            stationTerminal: externalStationTerminal));
    }

    [Fact]
    public void AddCustomerStation_RegistersEveryAggregateFactAtomically()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.IndoorStation, 2);

        document.AddCustomerStation(station);

        Assert.Same(station, Assert.Single(document.CustomerStations));
        Assert.Equal(3, document.Devices.Count);
        Assert.Equal(4, document.Terminals.Count);
        Assert.Equal(2, document.ElectricalNodes.Count);
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.Contains(feeder.IsolationSwitch, document.Devices);
            Assert.Contains(feeder.CableTerminal, document.Terminals);
            Assert.Contains(feeder.StationTerminal, document.Terminals);
            Assert.Contains(feeder.ElectricalNode, document.ElectricalNodes);
            Assert.Contains(feeder.StationTerminalId, feeder.ElectricalNode.TerminalIds);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AddCustomerStation_AnyAggregateIdCollisionLeavesDocumentUnchanged(
        int collisionIndex)
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        IncomingFeeder feeder = station.IncomingFeeders[0];
        Guid[] aggregateIds =
        [
            station.Id,
            feeder.IncomingFeederId,
            feeder.IsolationSwitch.Id,
            feeder.CableTerminalId,
            feeder.StationTerminalId,
            feeder.ElectricalNodeId
        ];
        Device existing = new(aggregateIds[collisionIndex], DeviceType.PT);
        document.AddDevice(existing);

        Assert.Throws<InvalidOperationException>(() =>
            document.AddCustomerStation(station));

        Assert.Same(existing, Assert.Single(document.Devices));
        Assert.Empty(document.CustomerStations);
        Assert.Empty(document.Terminals);
        Assert.Empty(document.ElectricalNodes);
    }

    [Fact]
    public void GenericDevicePaths_RejectCustomerStationAndOwnedSwitch()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);

        Assert.Throws<InvalidOperationException>(() => document.AddDevice(station));
        Assert.Empty(document.Devices);

        document.AddCustomerStation(station);
        IncomingFeeder feeder = station.IncomingFeeders[0];
        SwitchDevice rogue = SwitchDevice.CreateForCustomerStationIncomingFeeder(
            Guid.NewGuid(),
            feeder.IncomingFeederId,
            Guid.NewGuid(),
            Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => document.AddDevice(rogue));
        Assert.Throws<InvalidOperationException>(() => document.AddElectricalNode(
            new ElectricalNode(
                Guid.NewGuid(),
                ElectricalNodeType.Circuit,
                TopologyOwnerType.InternalAggregate,
                feeder.IncomingFeederId)));
        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(feeder.IsolationSwitch.Id));
        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(station.Id));
        Assert.Equal(2, document.Devices.Count);
    }

    [Fact]
    public void AddTerminal_RejectsCustomerStationIncomingFeederOwnerWithoutMutation()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        document.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        int terminalCount = document.Terminals.Count;
        var extra = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.InternalAggregate,
            feeder.IncomingFeederId,
            "额外内部端子",
            IncomingFeeder.TenKilovolts,
            false,
            false);

        Assert.Throws<InvalidOperationException>(() => document.AddTerminal(extra));

        Assert.Equal(terminalCount, document.Terminals.Count);
        Assert.DoesNotContain(extra, document.Terminals);
        Assert.True(feeder.ElectricalNode.TerminalIds.ToHashSet()
            .SetEquals([feeder.StationTerminalId]));
    }

    [Fact]
    public void AddTerminal_RejectsReferenceToCustomerStationCircuitNodeWithoutMutation()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        document.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        var otherOwner = new Device(Guid.NewGuid(), DeviceType.PT);
        document.AddDevice(otherOwner);
        int terminalCount = document.Terminals.Count;
        var extra = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.Device,
            otherOwner.Id,
            "额外节点端子",
            IncomingFeeder.TenKilovolts,
            false,
            false,
            feeder.ElectricalNodeId);

        Assert.Throws<InvalidOperationException>(() => document.AddTerminal(extra));

        Assert.Equal(terminalCount, document.Terminals.Count);
        Assert.DoesNotContain(extra, document.Terminals);
        Assert.True(feeder.ElectricalNode.TerminalIds.ToHashSet()
            .SetEquals([feeder.StationTerminalId]));
    }

    [Fact]
    public void RemoveCustomerStation_RemovesEveryInternalFact()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.IndoorStation, 2);
        document.AddCustomerStation(station);

        CustomerStation removed = document.RemoveCustomerStation(station.Id);

        Assert.Same(station, removed);
        Assert.Empty(document.CustomerStations);
        Assert.Empty(document.Devices);
        Assert.Empty(document.Terminals);
        Assert.Empty(document.ElectricalNodes);
    }

    [Fact]
    public void RemoveCustomerStation_WithConnectionDependency_IsAtomic()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        document.AddCustomerStation(station);
        Terminal other = AddCableEndpoint(document);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            station.IncomingFeeders[0].CableTerminalId,
            other.Id,
            "进线电缆",
            IncomingFeeder.TenKilovolts);
        document.AddConnection(connection);

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCustomerStation(station.Id));

        Assert.Contains(station, document.CustomerStations);
        Assert.Equal(2, document.Terminals.Count(terminal =>
            station.IncomingFeeders[0].IsolationSwitch.OwnsTerminal(terminal.Id)));
        Assert.Contains(connection, document.Connections);
    }

    [Fact]
    public void RemoveCustomerStation_WithGroundingDependency_IsAtomic()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        document.AddCustomerStation(station);
        GroundingPoint grounding = document.CreateGroundingPoint(
            Guid.NewGuid(),
            station.IncomingFeeders[0].CableTerminalId,
            "主供进线",
            "S01");

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCustomerStation(station.Id));

        Assert.Contains(station, document.CustomerStations);
        Assert.Contains(grounding, document.GroundingPoints);
    }

    [Fact]
    public void RemoveCustomerStation_WithFeederSwitchWorkScopeBoundary_IsAtomic()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.IndoorStation, 2);
        document.AddCustomerStation(station);
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        WorkScope workScope = document.CreateWorkScope(
            Guid.NewGuid(),
            new BoundaryPoint(
                first.IsolationSwitch.Id,
                first.CableTerminalId,
                "主供侧"),
            new BoundaryPoint(
                second.IsolationSwitch.Id,
                second.CableTerminalId,
                "备供侧"),
            "双电源用户站工作范围");

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCustomerStation(station.Id));

        Assert.Same(station, Assert.Single(document.CustomerStations));
        Assert.Same(first, station.IncomingFeeders[0]);
        Assert.Same(second, station.IncomingFeeders[1]);
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.Same(
                feeder.IsolationSwitch,
                document.Devices.Single(device => device.Id == feeder.IsolationSwitch.Id));
            Assert.Same(
                feeder.CableTerminal,
                document.Terminals.Single(terminal => terminal.Id == feeder.CableTerminalId));
            Assert.Same(
                feeder.StationTerminal,
                document.Terminals.Single(terminal => terminal.Id == feeder.StationTerminalId));
            Assert.Same(
                feeder.ElectricalNode,
                document.ElectricalNodes.Single(node => node.Id == feeder.ElectricalNodeId));
        });
        Assert.Same(workScope, Assert.Single(document.WorkScopes));
    }

    [Fact]
    public void ConnectionLegality_IsDerivedFromFrozenTerminalFacts()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        CustomerStation station = CreateStation(StationKind.BoxStation, 1);
        document.AddCustomerStation(station);
        IncomingFeeder feeder = station.IncomingFeeders[0];
        Terminal overhead = AddOverheadEndpoint(document);
        Assert.Throws<InvalidOperationException>(() => document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            feeder.CableTerminalId,
            overhead.Id,
            "架空线",
            IncomingFeeder.TenKilovolts)));
        Terminal firstOther = AddCableEndpoint(document);
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            feeder.CableTerminalId,
            firstOther.Id,
            "第一条电缆",
            IncomingFeeder.TenKilovolts));

        Assert.Throws<InvalidOperationException>(() => document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            feeder.CableTerminalId,
            AddCableEndpoint(document).Id,
            "第二条电缆",
            IncomingFeeder.TenKilovolts)));
        Assert.Throws<InvalidOperationException>(() => document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            feeder.StationTerminalId,
            AddCableEndpoint(document).Id,
            "内部端连接",
            IncomingFeeder.TenKilovolts)));
        Assert.Single(document.Connections);
    }

    private static CustomerStation CreateStation(StationKind kind, int feederCount)
    {
        return new CustomerStation(
            Guid.NewGuid(),
            kind,
            Enumerable.Range(1, feederCount).Select(sequence => CreateFeeder(sequence)));
    }

    private static IncomingFeeder CreateFeeder(
        int sequence,
        string displayName = "进线",
        Guid? incomingFeederId = null)
    {
        FeederParts parts = CreateFeederParts(incomingFeederId);
        return CreateFeeder(parts, displayName: displayName, sequence: sequence);
    }

    private static FeederParts CreateFeederParts(Guid? incomingFeederId = null)
    {
        return new FeederParts(
            incomingFeederId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static IncomingFeeder CreateFeeder(
        FeederParts parts,
        SwitchDevice? isolationSwitch = null,
        ElectricalNode? electricalNode = null,
        Terminal? cableTerminal = null,
        Terminal? stationTerminal = null,
        string displayName = "进线",
        int sequence = 1)
    {
        isolationSwitch ??= SwitchDevice.CreateForCustomerStationIncomingFeeder(
            parts.SwitchId,
            parts.IncomingFeederId,
            parts.CableTerminalId,
            parts.StationTerminalId);
        electricalNode ??= new ElectricalNode(
            parts.ElectricalNodeId,
            ElectricalNodeType.Circuit,
            TopologyOwnerType.InternalAggregate,
            parts.IncomingFeederId);
        cableTerminal ??= new Terminal(
            parts.CableTerminalId,
            TopologyOwnerType.Device,
            parts.SwitchId,
            IncomingFeeder.CableTerminalRole,
            IncomingFeeder.TenKilovolts,
            true,
            false,
            allowedConnectionTypes: [ConnectionType.Cable]);
        stationTerminal ??= new Terminal(
            parts.StationTerminalId,
            TopologyOwnerType.Device,
            parts.SwitchId,
            IncomingFeeder.StationTerminalRole,
            IncomingFeeder.TenKilovolts,
            false,
            false,
            parts.ElectricalNodeId);
        return new IncomingFeeder(
            parts.IncomingFeederId,
            sequence,
            displayName,
            parts.CableTerminalId,
            parts.StationTerminalId,
            parts.ElectricalNodeId,
            isolationSwitch,
            cableTerminal,
            stationTerminal,
            electricalNode);
    }

    private static void AssertFrozenFeederShape(IncomingFeeder feeder)
    {
        Assert.Equal(SwitchKind.IsolationSwitch, feeder.IsolationSwitch.SwitchKind);
        Assert.Equal(SwitchState.Open, feeder.IsolationSwitch.SwitchState);
        Assert.Equal(
            SwitchInstallationType.CustomerStationIncomingFeeder,
            feeder.IsolationSwitch.InstallationType);
        Assert.Equal(feeder.IncomingFeederId, feeder.IsolationSwitch.ParentId);
        Assert.Equal(feeder.CableTerminalId, feeder.IsolationSwitch.FirstTerminalId);
        Assert.Equal(feeder.StationTerminalId, feeder.IsolationSwitch.SecondTerminalId);
        Assert.NotEqual(feeder.CableTerminalId, feeder.StationTerminalId);
        Assert.True(feeder.CableTerminal.IsExternal);
        Assert.False(feeder.CableTerminal.AllowsMultipleConnections);
        Assert.Equal(TopologyOwnerType.Device, feeder.CableTerminal.OwnerType);
        Assert.Equal(feeder.IsolationSwitch.Id, feeder.CableTerminal.OwnerId);
        Assert.Equal(ConnectionType.Cable, Assert.Single(feeder.CableTerminal.AllowedConnectionTypes));
        Assert.False(feeder.StationTerminal.IsExternal);
        Assert.Equal(TopologyOwnerType.Device, feeder.StationTerminal.OwnerType);
        Assert.Equal(feeder.IsolationSwitch.Id, feeder.StationTerminal.OwnerId);
        Assert.Empty(feeder.StationTerminal.AllowedConnectionTypes);
        Assert.Equal(feeder.ElectricalNodeId, feeder.StationTerminal.ElectricalNodeId);
        Assert.Equal(ElectricalNodeType.Circuit, feeder.ElectricalNode.Type);
        Assert.Equal(TopologyOwnerType.InternalAggregate, feeder.ElectricalNode.OwnerType);
        Assert.Equal(feeder.IncomingFeederId, feeder.ElectricalNode.OwnerId);
    }

    private static Terminal AddCableEndpoint(DrawingDocument document)
    {
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(
            Guid.NewGuid(),
            TransformerKind.PublicIndoor,
            terminalId,
            "电缆端变压器");
        var terminal = new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            transformer.Id,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            true,
            false,
            allowedConnectionTypes: [ConnectionType.Cable]);
        document.AddTransformer(transformer, terminal);
        return terminal;
    }

    private static Terminal AddOverheadEndpoint(DrawingDocument document)
    {
        var pole = new Pole(Guid.NewGuid(), "P-01");
        document.AddDevice(pole);
        Terminal terminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        document.AddTerminal(terminal);
        return terminal;
    }

    private sealed record FeederParts(
        Guid IncomingFeederId,
        Guid SwitchId,
        Guid CableTerminalId,
        Guid StationTerminalId,
        Guid ElectricalNodeId);
}
