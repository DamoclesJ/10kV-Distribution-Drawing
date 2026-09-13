using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class TransformerTests
{
    public static TheoryData<TransformerKind, ConnectionType, bool> ConnectionLegality => new()
    {
        { TransformerKind.PublicPoleMounted, ConnectionType.OverheadLine, true },
        { TransformerKind.PublicPoleMounted, ConnectionType.Cable, false },
        { TransformerKind.DedicatedPoleMounted, ConnectionType.OverheadLine, true },
        { TransformerKind.DedicatedPoleMounted, ConnectionType.Cable, false },
        { TransformerKind.PublicIndoor, ConnectionType.Cable, true },
        { TransformerKind.PublicIndoor, ConnectionType.OverheadLine, false }
    };

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void Constructor_RequiresAndTrimsDisplayName(TransformerKind kind)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();

        var transformer = new Transformer(transformerId, kind, terminalId, "  T1  ");

        Assert.Equal("T1", transformer.DisplayName);
        Assert.False(transformer.IsLegacyNamingIncomplete);
        Assert.Throws<ArgumentException>(() =>
            new Transformer(Guid.NewGuid(), kind, Guid.NewGuid(), null!));
        Assert.Throws<ArgumentException>(() =>
            new Transformer(Guid.NewGuid(), kind, Guid.NewGuid(), string.Empty));
        Assert.Throws<ArgumentException>(() =>
            new Transformer(Guid.NewGuid(), kind, Guid.NewGuid(), "   "));
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void Rename_RequiresNameAndPreservesIdentityAndTopology(TransformerKind kind)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(transformerId, kind, terminalId, "Before");

        transformer.Rename("  After  ");

        Assert.Equal("After", transformer.DisplayName);
        Assert.Equal(transformerId, transformer.Id);
        Assert.Equal(terminalId, transformer.HvTerminalId);
        Assert.Equal(kind, transformer.TransformerKind);
        Assert.Throws<ArgumentException>(() => transformer.Rename(null));
        Assert.Throws<ArgumentException>(() => transformer.Rename(string.Empty));
        Assert.Throws<ArgumentException>(() => transformer.Rename("   "));
        Assert.Equal("After", transformer.DisplayName);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void LegacyRestore_IncompleteCanBeCompletedAndRestoredForFutureUndo(
        TransformerKind kind)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        Transformer transformer = Transformer.RestoreLegacy(
            transformerId,
            kind,
            terminalId,
            displayName: null);

        Assert.True(transformer.IsLegacyNamingIncomplete);
        Assert.Null(transformer.DisplayName);

        transformer.Rename("  补录名称  ");

        Assert.False(transformer.IsLegacyNamingIncomplete);
        Assert.Equal("补录名称", transformer.DisplayName);
        Assert.Equal(transformerId, transformer.Id);
        Assert.Equal(terminalId, transformer.HvTerminalId);
        Assert.Equal(kind, transformer.TransformerKind);

        transformer.RestoreLegacyNamingIncomplete();

        Assert.True(transformer.IsLegacyNamingIncomplete);
        Assert.Null(transformer.DisplayName);
        Assert.Equal(transformerId, transformer.Id);
        Assert.Equal(terminalId, transformer.HvTerminalId);
    }

    [Fact]
    public void CurrentTransformer_CannotRestoreLegacyIncompleteState()
    {
        var transformer = new Transformer(
            Guid.NewGuid(),
            TransformerKind.PublicIndoor,
            Guid.NewGuid(),
            "T1");

        Assert.Throws<InvalidOperationException>(() =>
            transformer.RestoreLegacyNamingIncomplete());
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void CreateTransformer_RegistersExactlyOneHvTerminalWithoutElectricalNode(
        TransformerKind transformerKind)
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(transformerKind);

        Add(document, aggregate);

        Transformer transformer = Assert.Single(document.Transformers);
        Assert.Same(aggregate.Transformer, transformer);
        Assert.Equal(DeviceType.Transformer, transformer.Type);
        Assert.Equal(transformerKind, transformer.TransformerKind);
        Assert.Equal(aggregate.HvTerminal.Id, transformer.HvTerminalId);
        Terminal terminal = Assert.Single(document.Terminals);
        Assert.Same(aggregate.HvTerminal, terminal);
        Assert.Equal(TopologyOwnerType.Device, terminal.OwnerType);
        Assert.Equal(transformer.Id, terminal.OwnerId);
        Assert.Equal(Transformer.HvTerminalRole, terminal.Role);
        Assert.Equal(Transformer.TenKilovolts, terminal.VoltageLevel);
        Assert.True(terminal.IsExternal);
        Assert.False(terminal.AllowsMultipleConnections);
        Assert.Null(terminal.ElectricalNodeId);
        Assert.Equal(
            transformer.AllowedConnectionType,
            Assert.Single(terminal.AllowedConnectionTypes));
        Assert.Empty(document.ElectricalNodes);
    }

    [Theory]
    [MemberData(nameof(ConnectionLegality))]
    public void AddConnection_EnforcesTransformerKindLegalityWithoutUi(
        TransformerKind transformerKind,
        ConnectionType connectionType,
        bool expectedAllowed)
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(transformerKind);
        Add(document, aggregate);
        Terminal other = AddOtherEndpoint(document, connectionType);
        var connection = new Connection(
            Guid.NewGuid(),
            connectionType,
            aggregate.HvTerminal.Id,
            other.Id,
            "测试连接",
            Transformer.TenKilovolts);

        if (expectedAllowed)
        {
            document.AddConnection(connection);
            Assert.Same(connection, Assert.Single(document.Connections));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                document.AddConnection(connection));
            Assert.Empty(document.Connections);
        }
    }

    [Fact]
    public void TransformerHvTerminal_RejectsSecondConnection()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        Terminal first = AddOtherEndpoint(document, ConnectionType.Cable);
        Terminal second = AddOtherEndpoint(document, ConnectionType.Cable);
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            aggregate.HvTerminal.Id,
            first.Id,
            "第一条电缆",
            Transformer.TenKilovolts));

        Assert.Throws<InvalidOperationException>(() =>
            document.AddConnection(new Connection(
                Guid.NewGuid(),
                ConnectionType.Cable,
                aggregate.HvTerminal.Id,
                second.Id,
                "第二条电缆",
                Transformer.TenKilovolts)));

        Assert.Single(document.Connections);
    }

    [Fact]
    public void AddTransformer_RejectsMalformedAggregateWithoutPartialMutation()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate valid = CreateAggregate(TransformerKind.PublicPoleMounted);
        Terminal[] invalidTerminals =
        [
            CreateTerminal(valid.Transformer, id: Guid.NewGuid()),
            CreateTerminal(
                valid.Transformer,
                ownerType: TopologyOwnerType.IntermediateTerminal),
            CreateTerminal(valid.Transformer, ownerId: Guid.NewGuid()),
            CreateTerminal(valid.Transformer, role: "WrongRole"),
            CreateTerminal(valid.Transformer, voltageLevel: "0.4kV"),
            CreateTerminal(valid.Transformer, isExternal: false, allowedConnectionTypes: []),
            CreateTerminal(valid.Transformer, allowsMultipleConnections: true),
            CreateTerminal(valid.Transformer, electricalNodeId: Guid.NewGuid()),
            CreateTerminal(
                valid.Transformer,
                allowedConnectionTypes: [ConnectionType.Cable])
        ];

        foreach (Terminal invalid in invalidTerminals)
        {
            Assert.Throws<InvalidOperationException>(() =>
                document.AddTransformer(valid.Transformer, invalid));
            Assert.Empty(document.Devices);
            Assert.Empty(document.Terminals);
            Assert.Empty(document.ElectricalNodes);
        }
    }

    [Fact]
    public void TransformerAggregate_RejectsExtraOwnedTerminalAndElectricalNode()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        Terminal extra = CreateTerminal(
            aggregate.Transformer,
            id: Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => document.AddTerminal(extra));
        Assert.Throws<InvalidOperationException>(() =>
            document.AddElectricalNode(new ElectricalNode(
                Guid.NewGuid(),
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                aggregate.Transformer.Id)));

        Assert.Single(document.Terminals);
        Assert.Empty(document.ElectricalNodes);
    }

    [Fact]
    public void AddDevice_RejectsPartialTransformerRegistration()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);

        Assert.Throws<InvalidOperationException>(() =>
            document.AddDevice(aggregate.Transformer));

        Assert.Empty(document.Devices);
        Assert.Empty(document.Terminals);
    }

    [Fact]
    public void RemoveUnconnectedTransformer_RemovesOnlyAggregateObjects()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        Device other = new(Guid.NewGuid(), DeviceType.PT);
        document.AddDevice(other);

        document.RemoveDevice(aggregate.Transformer.Id);

        Assert.DoesNotContain(aggregate.Transformer, document.Devices);
        Assert.DoesNotContain(aggregate.HvTerminal, document.Terminals);
        Assert.Same(other, Assert.Single(document.Devices));
        Assert.Empty(document.ElectricalNodes);
    }

    [Fact]
    public void RemoveConnectedTransformer_IsRejectedBeforeMutation()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        Terminal other = AddOtherEndpoint(document, ConnectionType.Cable);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            aggregate.HvTerminal.Id,
            other.Id,
            "电缆",
            Transformer.TenKilovolts);
        document.AddConnection(connection);

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(aggregate.Transformer.Id));

        Assert.Contains(aggregate.Transformer, document.Devices);
        Assert.Contains(aggregate.HvTerminal, document.Terminals);
        Assert.Contains(connection, document.Connections);
    }

    [Fact]
    public void RemoveTransformer_WithGroundingPointDependency_IsRejected()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        GroundingPoint groundingPoint = GroundingPoint.Create(
            Guid.NewGuid(),
            aggregate.HvTerminal.Id,
            "legacy terminal target");
        document.AddGroundingPoint(groundingPoint);

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(aggregate.Transformer.Id));

        Assert.Contains(aggregate.Transformer, document.Devices);
        Assert.Contains(groundingPoint, document.GroundingPoints);
    }

    [Fact]
    public void RemoveTransformer_WithWorkScopeDependency_IsRejected()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Aggregate aggregate = CreateAggregate(TransformerKind.PublicIndoor);
        Add(document, aggregate);
        Terminal other = AddOtherEndpoint(document, ConnectionType.Cable);
        Device otherDevice = document.Devices.Single(device => device.Id == other.OwnerId);
        WorkScope workScope = WorkScope.Create(
            Guid.NewGuid(),
            new BoundaryPoint(
                aggregate.Transformer.Id,
                aggregate.HvTerminal.Id,
                "HV"),
            new BoundaryPoint(otherDevice.Id, other.Id, "Other"),
            "测试范围");
        document.AddWorkScope(workScope);

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(aggregate.Transformer.Id));

        Assert.Contains(aggregate.Transformer, document.Devices);
        Assert.Contains(workScope, document.WorkScopes);
    }

    [Fact]
    public void DropoutFuseToTransformer_ShortOverheadLineUsesOnlyRealSupportPole()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-Transformer");
        document.AddDevice(pole);
        Guid firstTerminalId = Guid.NewGuid();
        Guid secondTerminalId = Guid.NewGuid();
        SwitchDevice fuse = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.DropoutFuse,
            firstTerminalId,
            secondTerminalId);
        Terminal firstTerminal = CreateSwitchTerminal(
            fuse,
            firstTerminalId,
            "SwitchFirst",
            allowsMultipleConnections: true);
        Terminal secondTerminal = CreateSwitchTerminal(
            fuse,
            secondTerminalId,
            "SwitchSecond",
            allowsMultipleConnections: false);
        document.AddPoleSwitchAttachment(
            fuse,
            firstTerminal,
            secondTerminal,
            new PoleAttachment(Guid.NewGuid(), pole.Id, fuse.Id));
        Aggregate transformer = CreateAggregate(TransformerKind.PublicPoleMounted);
        Add(document, transformer);
        Terminal registeredFuseTerminal = document.Terminals.Single(terminal =>
            terminal.Id == secondTerminalId);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            registeredFuseTerminal.Id,
            transformer.HvTerminal.Id,
            "短架空线",
            Transformer.TenKilovolts);
        document.AddConnection(connection);
        OverheadLine line = new(
            connection.Id,
            "JKLYJ-10kV",
            [pole.Id]);

        document.AddOverheadLine(line);

        Assert.Same(line, Assert.Single(document.OverheadLines));
        Assert.Equal(pole.Id, Assert.Single(line.SupportPoleIds));
        Assert.DoesNotContain(
            transformer.Transformer.Id,
            line.SupportPoleIds);
        Assert.DoesNotContain(document.PoleAttachments, attachment =>
            attachment.AttachedDeviceId == transformer.Transformer.Id);
    }

    [Fact]
    public void AddOverheadLine_RejectsUnsupportedDeviceOwnerWithoutPhysicalPole()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-Unsupported");
        Terminal poleTerminal = TestFixtures.CreatePoleAnchorTerminal(pole);
        var unsupported = new Device(Guid.NewGuid(), DeviceType.PT);
        var unsupportedTerminal = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.Device,
            unsupported.Id,
            "Unsupported",
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        document.AddDevice(pole);
        document.AddTerminal(poleTerminal);
        document.AddDevice(unsupported);
        document.AddTerminal(unsupportedTerminal);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            poleTerminal.Id,
            unsupportedTerminal.Id,
            "不支持的端点",
            Transformer.TenKilovolts);
        document.AddConnection(connection);

        Assert.Throws<InvalidOperationException>(() =>
            document.AddOverheadLine(new OverheadLine(
                connection.Id,
                "JKLYJ-10kV",
                [pole.Id])));

        Assert.Empty(document.OverheadLines);
    }

    [Fact]
    public void AddOverheadLine_RejectsTransformerToTransformerWithFabricatedSupportPole()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var unrelatedPole = new Pole(Guid.NewGuid(), "P99");
        document.AddDevice(unrelatedPole);
        Aggregate start = CreateAggregate(TransformerKind.PublicPoleMounted);
        Aggregate end = CreateAggregate(TransformerKind.DedicatedPoleMounted);
        Add(document, start);
        Add(document, end);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            start.HvTerminal.Id,
            end.HvTerminal.Id,
            "伪造支撑杆的架空线",
            Transformer.TenKilovolts);
        document.AddConnection(connection);
        var overheadLine = new OverheadLine(
            connection.Id,
            "JKLYJ-10kV",
            [unrelatedPole.Id]);

        Assert.Throws<InvalidOperationException>(() =>
            document.AddOverheadLine(overheadLine));

        Assert.Empty(document.OverheadLines);
    }

    private static Aggregate CreateAggregate(TransformerKind transformerKind)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(
            transformerId,
            transformerKind,
            terminalId,
            "测试变压器");
        return new Aggregate(transformer, CreateTerminal(transformer));
    }

    private static Terminal CreateTerminal(
        Transformer transformer,
        Guid? id = null,
        TopologyOwnerType ownerType = TopologyOwnerType.Device,
        Guid? ownerId = null,
        string? role = null,
        string? voltageLevel = null,
        bool isExternal = true,
        bool allowsMultipleConnections = false,
        Guid? electricalNodeId = null,
        IEnumerable<ConnectionType>? allowedConnectionTypes = null)
    {
        return new Terminal(
            id ?? transformer.HvTerminalId,
            ownerType,
            ownerId ?? transformer.Id,
            role ?? Transformer.HvTerminalRole,
            voltageLevel ?? Transformer.TenKilovolts,
            isExternal,
            allowsMultipleConnections,
            electricalNodeId,
            allowedConnectionTypes ?? [transformer.AllowedConnectionType]);
    }

    private static Terminal AddOtherEndpoint(
        DrawingDocument document,
        ConnectionType connectionType)
    {
        var device = new Device(Guid.NewGuid(), DeviceType.PT);
        var terminal = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.Device,
            device.Id,
            "OtherTerminal",
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: true,
            allowedConnectionTypes: [connectionType]);
        document.AddDevice(device);
        document.AddTerminal(terminal);
        return terminal;
    }

    private static Terminal CreateSwitchTerminal(
        SwitchDevice switchDevice,
        Guid terminalId,
        string role,
        bool allowsMultipleConnections)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            switchDevice.Id,
            role,
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
    }

    private static void Add(DrawingDocument document, Aggregate aggregate)
    {
        document.AddTransformer(aggregate.Transformer, aggregate.HvTerminal);
    }

    private sealed record Aggregate(Transformer Transformer, Terminal HvTerminal);
}
