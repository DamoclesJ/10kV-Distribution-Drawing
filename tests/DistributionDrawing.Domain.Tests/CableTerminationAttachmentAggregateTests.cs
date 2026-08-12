using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class CableTerminationAttachmentAggregateTests
{
    [Fact]
    public void Complete_aggregate_is_registered_atomically()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-40");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);

        Add(document, aggregate);

        Assert.Contains(aggregate.Termination, document.Devices);
        Assert.Same(aggregate.Node, Assert.Single(document.ElectricalNodes));
        Assert.Equal(2, document.Terminals.Count);
        Assert.Contains(aggregate.CableSideTerminal, document.Terminals);
        Assert.Contains(aggregate.OverheadSideTerminal, document.Terminals);
        Assert.Same(aggregate.Attachment, Assert.Single(document.PoleAttachments));
        Assert.Equal(aggregate.Termination.Id, aggregate.Node.OwnerId);
        Assert.Equal(aggregate.Termination.Id, aggregate.Attachment.AttachedDeviceId);
        Assert.Equal(pole.Id, aggregate.Attachment.PoleId);
        Assert.Equal(
            new[]
            {
                aggregate.Termination.CableSideTerminalId,
                aggregate.Termination.OverheadSideTerminalId
            }.OrderBy(id => id),
            aggregate.Node.TerminalIds.OrderBy(id => id));
    }

    [Fact]
    public void Complete_aggregate_preserves_confirmed_topology()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-41");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);

        Add(document, aggregate);

        Assert.Equal(
            ConnectionType.Cable,
            Assert.Single(aggregate.CableSideTerminal.AllowedConnectionTypes));
        Assert.Equal(
            ConnectionType.OverheadLine,
            Assert.Single(aggregate.OverheadSideTerminal.AllowedConnectionTypes));
        Assert.Equal(
            aggregate.Termination.InternalNodeId,
            aggregate.CableSideTerminal.ElectricalNodeId);
        Assert.Equal(
            aggregate.Termination.InternalNodeId,
            aggregate.OverheadSideTerminal.ElectricalNodeId);
        Assert.Empty(document.Connections);
    }

    [Fact]
    public void Complete_aggregate_can_be_removed_and_restored_with_the_same_ids()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-42");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);
        Add(document, aggregate);

        document.RemoveCableTerminationAttachment(aggregate.Attachment.AttachmentId);

        AssertAggregateAbsent(document, aggregate);

        Add(document, aggregate);

        AssertAggregatePresent(document, aggregate);
        Assert.Equal(aggregate.Termination.Id, document.Devices
            .OfType<CableTermination>().Single().Id);
        Assert.Equal(aggregate.Node.Id, document.ElectricalNodes.Single().Id);
        Assert.Equal(
            aggregate.Termination.TerminalIds.OrderBy(id => id),
            document.Terminals.Select(terminal => terminal.Id).OrderBy(id => id));
        Assert.Equal(
            aggregate.Attachment.AttachmentId,
            document.PoleAttachments.Single().AttachmentId);
    }

    [Fact]
    public void Removal_is_rejected_when_cable_side_has_a_connection()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var firstPole = new Pole(Guid.NewGuid(), "P-43");
        var secondPole = new Pole(Guid.NewGuid(), "P-44");
        document.AddDevice(firstPole);
        document.AddDevice(secondPole);
        Aggregate aggregate = CreateAggregate(firstPole.Id);
        Aggregate other = CreateAggregate(secondPole.Id);
        Add(document, aggregate);
        Add(document, other);
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            aggregate.CableSideTerminal.Id,
            other.CableSideTerminal.Id,
            "电缆连接",
            TestFixtures.TenKilovolts));

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCableTerminationAttachment(
                aggregate.Attachment.AttachmentId));

        AssertAggregatePresent(document, aggregate);
    }

    [Fact]
    public void Removal_is_rejected_when_overhead_side_has_an_overhead_line()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var firstPole = new Pole(Guid.NewGuid(), "P-45");
        var secondPole = new Pole(Guid.NewGuid(), "P-46");
        Terminal secondAnchor = secondPole.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        document.AddDevice(firstPole);
        document.AddDevice(secondPole);
        document.AddTerminal(secondAnchor);
        Aggregate aggregate = CreateAggregate(firstPole.Id);
        Add(document, aggregate);
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            aggregate.OverheadSideTerminal.Id,
            secondAnchor.Id,
            "架空线路",
            TestFixtures.TenKilovolts);
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(
            connection.Id,
            "JKLYJ-10kV",
            [firstPole.Id, secondPole.Id]));

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCableTerminationAttachment(
                aggregate.Attachment.AttachmentId));

        AssertAggregatePresent(document, aggregate);
        Assert.Contains(connection, document.Connections);
        Assert.Contains(document.OverheadLines, line => line.ConnectionId == connection.Id);
    }

    [Fact]
    public void Removal_is_rejected_when_a_terminal_has_a_grounding_point()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-47");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);
        Add(document, aggregate);
        document.AddGroundingPoint(GroundingPoint.Create(
            Guid.NewGuid(),
            aggregate.CableSideTerminal.Id,
            "电缆侧"));

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCableTerminationAttachment(
                aggregate.Attachment.AttachmentId));

        AssertAggregatePresent(document, aggregate);
    }

    [Fact]
    public void Removal_is_rejected_when_a_work_scope_references_the_device_and_terminals()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-48");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);
        Add(document, aggregate);
        document.AddWorkScope(WorkScope.Create(
            Guid.NewGuid(),
            new BoundaryPoint(
                aggregate.Termination.Id,
                aggregate.CableSideTerminal.Id,
                "电缆侧"),
            new BoundaryPoint(
                aggregate.Termination.Id,
                aggregate.OverheadSideTerminal.Id,
                "架空侧"),
            "测试工作范围"));

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCableTerminationAttachment(
                aggregate.Attachment.AttachmentId));

        AssertAggregatePresent(document, aggregate);
    }

    [Fact]
    public void Invalid_aggregate_registration_does_not_change_the_document()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-49");
        document.AddDevice(pole);
        Aggregate valid = CreateAggregate(pole.Id);
        var cases = new Aggregate[]
        {
            valid with
            {
                Attachment = new PoleAttachment(
                    valid.Attachment.AttachmentId,
                    pole.Id,
                    Guid.NewGuid())
            },
            valid with
            {
                Node = new ElectricalNode(
                    valid.Node.Id,
                    ElectricalNodeType.Intermediate,
                    TopologyOwnerType.Device,
                    pole.Id)
            },
            valid with
            {
                CableSideTerminal = CreateTerminal(
                    valid.Termination.CableSideTerminalId,
                    valid.Termination.Id,
                    Guid.NewGuid(),
                    CableTermination.CableSideRole,
                    ConnectionType.Cable)
            },
            valid with
            {
                CableSideTerminal = CreateTerminal(
                    Guid.NewGuid(),
                    valid.Termination.Id,
                    valid.Termination.InternalNodeId,
                    CableTermination.CableSideRole,
                    ConnectionType.Cable)
            },
            valid with
            {
                CableSideTerminal = CreateTerminal(
                    valid.Termination.CableSideTerminalId,
                    valid.Termination.Id,
                    valid.Termination.InternalNodeId,
                    CableTermination.OverheadSideRole,
                    ConnectionType.Cable)
            },
            valid with
            {
                OverheadSideTerminal = CreateTerminal(
                    valid.Termination.OverheadSideTerminalId,
                    valid.Termination.Id,
                    valid.Termination.InternalNodeId,
                    CableTermination.OverheadSideRole,
                    ConnectionType.Cable)
            },
            valid with
            {
                Attachment = new PoleAttachment(
                    valid.Attachment.AttachmentId,
                    Guid.NewGuid(),
                    valid.Termination.Id)
            }
        };

        foreach (Aggregate invalid in cases)
        {
            Assert.Throws<InvalidOperationException>(() => Add(document, invalid));
            Assert.DoesNotContain(
                document.Devices,
                device => device.Id == valid.Termination.Id);
            Assert.Empty(document.ElectricalNodes);
            Assert.Empty(document.Terminals);
            Assert.Empty(document.PoleAttachments);
        }
    }

    [Fact]
    public void Duplicate_aggregate_registration_is_rejected_without_changing_existing_state()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-50");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);
        Add(document, aggregate);

        Assert.Throws<InvalidOperationException>(() => Add(document, aggregate));

        AssertAggregatePresent(document, aggregate);
        Assert.Single(document.Devices.OfType<CableTermination>());
        Assert.Single(document.ElectricalNodes);
        Assert.Equal(2, document.Terminals.Count);
        Assert.Single(document.PoleAttachments);
    }

    [Fact]
    public void Removal_rejects_an_incomplete_aggregate_without_removing_existing_objects()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-51");
        document.AddDevice(pole);
        Aggregate aggregate = CreateAggregate(pole.Id);
        document.AddDevice(aggregate.Termination);
        document.AddElectricalNode(aggregate.Node);
        document.AddTerminal(aggregate.CableSideTerminal);
        document.AddPoleAttachment(aggregate.Attachment);

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveCableTerminationAttachment(
                aggregate.Attachment.AttachmentId));

        Assert.Contains(aggregate.Termination, document.Devices);
        Assert.Contains(aggregate.Node, document.ElectricalNodes);
        Assert.Contains(aggregate.CableSideTerminal, document.Terminals);
        Assert.Contains(aggregate.Attachment, document.PoleAttachments);
        Assert.DoesNotContain(aggregate.OverheadSideTerminal, document.Terminals);
    }

    private static Aggregate CreateAggregate(Guid poleId)
    {
        var termination = new CableTermination(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "电缆终端");
        return new Aggregate(
            termination,
            new ElectricalNode(
                termination.InternalNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                termination.Id),
            CreateTerminal(
                termination.CableSideTerminalId,
                termination.Id,
                termination.InternalNodeId,
                CableTermination.CableSideRole,
                ConnectionType.Cable),
            CreateTerminal(
                termination.OverheadSideTerminalId,
                termination.Id,
                termination.InternalNodeId,
                CableTermination.OverheadSideRole,
                ConnectionType.OverheadLine),
            new PoleAttachment(Guid.NewGuid(), poleId, termination.Id));
    }

    private static Terminal CreateTerminal(
        Guid terminalId,
        Guid ownerId,
        Guid nodeId,
        string role,
        ConnectionType connectionType)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            ownerId,
            role,
            TestFixtures.TenKilovolts,
            true,
            false,
            nodeId,
            [connectionType]);
    }

    private static void Add(DrawingDocument document, Aggregate aggregate)
    {
        document.AddCableTerminationAttachment(
            aggregate.Termination,
            aggregate.Node,
            aggregate.CableSideTerminal,
            aggregate.OverheadSideTerminal,
            aggregate.Attachment);
    }

    private static void AssertAggregatePresent(
        DrawingDocument document,
        Aggregate aggregate)
    {
        Assert.Contains(aggregate.Termination, document.Devices);
        Assert.Contains(aggregate.Node, document.ElectricalNodes);
        Assert.Contains(aggregate.CableSideTerminal, document.Terminals);
        Assert.Contains(aggregate.OverheadSideTerminal, document.Terminals);
        Assert.Contains(aggregate.Attachment, document.PoleAttachments);
    }

    private static void AssertAggregateAbsent(
        DrawingDocument document,
        Aggregate aggregate)
    {
        Assert.DoesNotContain(aggregate.Termination, document.Devices);
        Assert.DoesNotContain(aggregate.Node, document.ElectricalNodes);
        Assert.DoesNotContain(aggregate.CableSideTerminal, document.Terminals);
        Assert.DoesNotContain(aggregate.OverheadSideTerminal, document.Terminals);
        Assert.DoesNotContain(aggregate.Attachment, document.PoleAttachments);
    }

    private sealed record Aggregate(
        CableTermination Termination,
        ElectricalNode Node,
        Terminal CableSideTerminal,
        Terminal OverheadSideTerminal,
        PoleAttachment Attachment);
}
