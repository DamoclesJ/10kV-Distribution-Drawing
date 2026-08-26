using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class CompleteWorkTicketScenarioTests
{
    [Fact]
    public void CompleteScenario_SaveLoadPreservesTopologyIdentityAndNumbering()
    {
        ScenarioFixture fixture = CreateFixture();

        ProjectFileManifest manifest;
        DrawingDocument restored = RoundTrip(fixture.Document, out manifest);

        Assert.Equal(ProjectFileFormat.CurrentVersion, manifest.FormatVersion);
        Assert.Equal(fixture.Document.Id, restored.Id);
        RingCabinet restoredCabinet = Assert.Single(restored.Devices.OfType<RingCabinet>());
        RingCabinetInterval restoredInterval = Assert.Single(
            restoredCabinet.Intervals,
            interval => interval.IntervalId == fixture.Interval.IntervalId);
        Assert.Equal(fixture.Cabinet.Id, restoredCabinet.Id);
        Assert.Equal(fixture.Interval.IntervalId, restoredInterval.IntervalId);
        Assert.Equal(fixture.Interval.BusinessNumber, restoredInterval.BusinessNumber);
        Assert.Equal(
            fixture.Interval.SwitchDevices.Select(device => device.Id).Order(),
            restoredInterval.SwitchDevices.Select(device => device.Id).Order());
        Assert.Equal(
            fixture.Document.CableSegments.Select(cable => cable.Id).Order(),
            restored.CableSegments.Select(cable => cable.Id).Order());
        Assert.Equal(
            fixture.Document.IntermediateTerminals.Select(joint => joint.Id).Order(),
            restored.IntermediateTerminals.Select(joint => joint.Id).Order());
        Assert.Equal(
            fixture.Document.Connections.Select(connection => connection.Id).Order(),
            restored.Connections.Select(connection => connection.Id).Order());

        CableTermination restoredTermination = restored.Devices
            .OfType<CableTermination>()
            .Single();
        ElectricalConnectivityQuery query = new(
            new ElectricalConnectivityGraphBuilder().Build(restored));
        Assert.True(query.IsConnected(
            restoredInterval.ExternalTerminalId,
            restoredTermination.CableSideTerminalId));
        Assert.True(restoredInterval.SwitchAssembly.Evaluate().IsValid);
    }

    private static DrawingDocument RoundTrip(
        DrawingDocument document,
        out ProjectFileManifest manifest)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-complete-scenario-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, new ProjectFileDocument(
                ProjectFileManifest.Create(
                    document.Id,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                new ProjectFileMetadata(document.Title),
                ProjectDomainMapper.ToDto(document),
                ProjectLayoutDto.Empty(document.Id),
                ProjectProfessionalDto.Empty(document.Id)));

            ProjectFileDocument opened = container.Open(filePath);
            manifest = opened.Manifest;
            return ProjectDomainMapper.ToDomain(opened.Domain!);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static ScenarioFixture CreateFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "完整工作票场景");
        RingCabinetDefinition cabinetDefinition = RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "环网柜-601",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(
                3,
                SwitchState.Closed,
                SwitchState.Open,
                "负3负荷开关"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                4,
                SwitchState.Open,
                SwitchState.Open,
                "负4负荷开关"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                5,
                SwitchState.Open,
                SwitchState.Open,
                "负5负荷开关")]);
        RingCabinet cabinet = RingCabinet.Create(cabinetDefinition);
        document.AddDevice(cabinet);

        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P-601",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true);
        AddPoleAggregate(document, pole);

        IntermediateTerminalCreationResult joint =
            new IntermediateTerminalCreationFactory().Create("中间接头-601");
        document.AddIntermediateTerminal(joint.IntermediateTerminal, joint.Terminal);

        CableTermination termination = Assert.Single(
            pole.Devices.OfType<CableTermination>());
        AddCable(document, cabinet.Intervals[0].ExternalTerminalId, joint.Terminal.Id, "Cable-601-A");
        AddCable(document, joint.Terminal.Id, termination.CableSideTerminalId, "Cable-601-B");

        return new ScenarioFixture(document, cabinet, cabinet.Intervals[0]);
    }

    private static void AddCable(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId,
        string name)
    {
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            name,
            "10kV");
        document.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(),
                name,
                "YJV22-8.7/15kV",
                120,
                "10kV",
                connection.Id,
                startTerminalId,
                endTerminalId),
            connection);
    }

    private static void AddPoleAggregate(
        DrawingDocument document,
        PoleCreationResult result)
    {
        document.AddDevice(result.Pole);
        foreach (Device device in result.Devices)
        {
            document.AddDevice(device);
        }

        foreach (ElectricalNode node in result.ElectricalNodes)
        {
            document.AddElectricalNode(node);
        }

        foreach (SwitchDevice switchDevice in result.Devices.OfType<SwitchDevice>())
        {
            Guid rightNodeId = result.Terminals
                .Single(terminal => terminal.OwnerId == switchDevice.Id &&
                    terminal.Role == "SwitchRightTerminal")
                .ElectricalNodeId
                ?? throw new InvalidOperationException("Pole switch right node is missing.");
            document.AddElectricalNode(new ElectricalNode(
                rightNodeId,
                ElectricalNodeType.Intermediate,
                TopologyOwnerType.Device,
                switchDevice.Id));
        }

        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }

        foreach (PoleAttachment attachment in result.Attachments)
        {
            document.AddPoleAttachment(attachment);
        }
    }

    private sealed record ScenarioFixture(
        DrawingDocument Document,
        RingCabinet Cabinet,
        RingCabinetInterval Interval);
}
