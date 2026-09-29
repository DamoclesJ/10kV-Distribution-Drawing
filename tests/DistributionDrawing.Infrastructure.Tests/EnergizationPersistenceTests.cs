using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class EnergizationPersistenceTests
{
    [Fact]
    public void V9RoundTripPreservesScenarioInputsAndRecalculatesResult()
    {
        string path = NextPath();
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "EA scenario");
            RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
                Guid.NewGuid(), "EA cabinet",
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                    SwitchState.Open, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                    SwitchState.Open, SwitchState.Open)]));
            project.Domain.AddDevice(cabinet);
            var zero = new ProjectPointDto(0, 0);
            service.SetLayout(new ProjectLayoutSnapshot(ProjectLayoutDto.Empty(project.Domain.Id) with
            {
                RingCabinets = [new ProjectRingCabinetLayoutDto(cabinet.Id, zero,
                    100, 100, 20, zero, cabinet.Intervals.Select(interval =>
                        new ProjectRingCabinetIntervalLayoutDto(interval.IntervalId,
                            zero, 40, 80, zero, zero, interval.SwitchDevices.Select(device =>
                                new ProjectRingCabinetSwitchLayoutDto(device.Id,
                                    zero, 10, 10, zero)).ToArray())).ToArray())]
            }));
            SwitchDevice boundary = cabinet.Intervals[0].SwitchDevices.Single(device =>
                device.SwitchKind == SwitchKind.LoadSwitch);
            EnergizedSeed seed = new(Guid.NewGuid(), boundary.Id, EnergizationSide.Bus);
            project.EnergizationScenario.AddSeed(seed);
            project.EnergizationScenario.SetSourceSetComplete(true);
            service.SaveProject();

            ProjectSession restored = new ProjectService().LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version9, restored.Manifest.FormatVersion);
            Assert.Equal(ProjectFileFormat.Version9, restored.OpenedFormatVersion);
            Assert.Equal(project.EnergizationScenario.Id, restored.EnergizationScenario.Id);
            Assert.True(restored.EnergizationScenario.IsSourceSetComplete);
            Assert.Equal(seed, Assert.Single(restored.EnergizationScenario.Seeds));
            EnergizationResult result = new EnergizationAnalyzer().Analyze(
                restored.Domain, restored.EnergizationScenario);
            Assert.Equal(EnergizationState.Energized,
                result.Terminals[boundary.FirstTerminalId].State);
            Assert.Equal(EnergizationState.Deenergized,
                result.Terminals[boundary.SecondTerminalId].State);

            JsonObject payload = Read(path, ProjectFileFormat.DocumentEntryName);
            Assert.NotNull(payload["energizationScenario"]);
            Assert.Null(payload["energizationResult"]);
            Assert.Null(payload["energizedBy"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void V8OpensWithUnconfirmedEmptyScenarioAndNextSaveWritesV9()
    {
        string path = NextPath();
        try
        {
            var writer = new ProjectService();
            ProjectSession original = writer.CreateProject(path, "V8 migration");
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                Task = new WorkTask("保留票据", "保留对象"),
                UserFacts = [new UserTicketFact("Risk", "人工事实", [], true)]
            };
            original.WorkTickets.Add(ticket);
            writer.SaveProject();
            Mutate(path, ProjectFileFormat.ManifestEntryName,
                json => json["formatVersion"] = 8);
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => json.Remove("energizationScenario"));

            var service = new ProjectService();
            ProjectSession migrated = service.LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version8, migrated.OpenedFormatVersion);
            Assert.Equal(ProjectFileFormat.Version9, migrated.Manifest.FormatVersion);
            Assert.Empty(migrated.EnergizationScenario.Seeds);
            Assert.False(migrated.EnergizationScenario.IsSourceSetComplete);
            Assert.Equal(ticket.Task, Assert.Single(migrated.WorkTickets.Tickets).Task);
            Assert.Equal("人工事实", migrated.WorkTickets.Tickets[0].UserFacts[0].Text);
            Guid scenarioId = migrated.EnergizationScenario.Id;
            service.SaveProject();

            ProjectSession reopened = new ProjectService().LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version9, reopened.OpenedFormatVersion);
            Assert.Equal(scenarioId, reopened.EnergizationScenario.Id);
            Assert.False(reopened.EnergizationScenario.IsSourceSetComplete);
            Assert.Equal(ticket.Id, Assert.Single(reopened.WorkTickets.Tickets).Id);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void V8NonEmptyTopologyMigrationPreservesElectricalAndLayoutFacts()
    {
        string path = NextPath();
        try
        {
            var writer = new ProjectService();
            ProjectSession original = writer.CreateProject(path, "V8 non-empty migration");
            RingCabinet first = RingCabinet.Create(RingCabinetDefinition.Create(
                Guid.NewGuid(), "first",
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                    SwitchState.Open, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                    SwitchState.Open, SwitchState.Open)]));
            RingCabinet second = RingCabinet.Create(RingCabinetDefinition.Create(
                Guid.NewGuid(), "second",
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                    SwitchState.Open, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                    SwitchState.Open, SwitchState.Open)]));
            original.Domain.AddDevice(first);
            original.Domain.AddDevice(second);
            Guid terminalId = first.Intervals[0].CableTerminalId!.Value;
            Guid nodeId = first.MainBusNodeId;
            var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
                terminalId, second.Intervals[0].CableTerminalId!.Value, "cable", "10kV");
            original.Domain.AddConnection(cable);
            var firstPosition = new ProjectPointDto(120, 240);
            var secondPosition = new ProjectPointDto(480, 240);
            writer.SetLayout(new ProjectLayoutSnapshot(ProjectLayoutDto.Empty(original.Domain.Id) with
            {
                RingCabinets = [LayoutFor(first, firstPosition), LayoutFor(second, secondPosition)]
            }));
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                Task = new WorkTask("保留票据", "保留对象")
            };
            original.WorkTickets.Add(ticket);
            writer.SaveProject();
            Mutate(path, ProjectFileFormat.ManifestEntryName,
                json => json["formatVersion"] = ProjectFileFormat.Version8);
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => json.Remove("energizationScenario"));

            var service = new ProjectService();
            ProjectSession migrated = service.LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version8, migrated.OpenedFormatVersion);
            Assert.Equal(first.Id, Assert.Single(migrated.Domain.Devices.OfType<RingCabinet>(),
                cabinet => cabinet.Id == first.Id).Id);
            Assert.Contains(migrated.Domain.Terminals, terminal => terminal.Id == terminalId);
            Assert.Contains(migrated.Domain.ElectricalNodes, node => node.Id == nodeId);
            Connection restoredCable = Assert.Single(migrated.Domain.Connections);
            Assert.Equal(cable.Id, restoredCable.Id);
            Assert.Equal(cable.StartTerminalId, restoredCable.StartTerminalId);
            Assert.Equal(cable.EndTerminalId, restoredCable.EndTerminalId);
            Assert.Equal(firstPosition, Assert.Single(migrated.Layout.RingCabinets,
                layout => layout.CabinetId == first.Id).Position);
            Assert.Equal(secondPosition, Assert.Single(migrated.Layout.RingCabinets,
                layout => layout.CabinetId == second.Id).Position);
            Assert.Equal(ticket.Id, Assert.Single(migrated.WorkTickets.Tickets).Id);
            Assert.Equal(ticket.Task, migrated.WorkTickets.Tickets[0].Task);
            Assert.Empty(migrated.EnergizationScenario.Seeds);
            Assert.False(migrated.EnergizationScenario.IsSourceSetComplete);

            service.SaveProject();
            ProjectSession reopened = new ProjectService().LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version9, reopened.OpenedFormatVersion);
            Assert.Equal(cable.Id, Assert.Single(reopened.Domain.Connections).Id);
            Assert.Contains(reopened.Domain.Terminals, terminal => terminal.Id == terminalId);
            Assert.Contains(reopened.Domain.ElectricalNodes, node => node.Id == nodeId);
            Assert.Equal(firstPosition, Assert.Single(reopened.Layout.RingCabinets,
                layout => layout.CabinetId == first.Id).Position);
            Assert.Equal(ticket.Id, Assert.Single(reopened.WorkTickets.Tickets).Id);
            Assert.Empty(reopened.EnergizationScenario.Seeds);
            Assert.False(reopened.EnergizationScenario.IsSourceSetComplete);
        }
        finally { File.Delete(path); }

        static ProjectRingCabinetLayoutDto LayoutFor(RingCabinet cabinet, ProjectPointDto position)
        {
            var zero = new ProjectPointDto(0, 0);
            return new ProjectRingCabinetLayoutDto(cabinet.Id, position,
                100, 100, 20, zero, cabinet.Intervals.Select(interval =>
                    new ProjectRingCabinetIntervalLayoutDto(interval.IntervalId,
                        zero, 40, 80, zero, zero, interval.SwitchDevices.Select(device =>
                            new ProjectRingCabinetSwitchLayoutDto(device.Id,
                                zero, 10, 10, zero)).ToArray())).ToArray());
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    public void UnsupportedVersionsAreRejected(int version)
    {
        string path = NextPath();
        try
        {
            new ProjectService().CreateProject(path, "versions");
            Mutate(path, ProjectFileFormat.ManifestEntryName,
                json => json["formatVersion"] = version);
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void V9RejectsMissingScenarioCompletenessAndUnexpectedFields()
    {
        string path = NextPath();
        try
        {
            new ProjectService().CreateProject(path, "strict EA");
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => json.Remove("energizationScenario"));
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));

            new ProjectService().CreateProject(path, "strict EA");
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => ((JsonObject)json["energizationScenario"]!).Remove("isSourceSetComplete"));
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));

            new ProjectService().CreateProject(path, "strict EA");
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => json["unexpectedEaField"] = true);
            Assert.Throws<JsonException>(() => new ProjectService().LoadProject(path));

            new ProjectService().CreateProject(path, "strict EA");
            Mutate(path, ProjectFileFormat.DocumentEntryName, json =>
            {
                JsonObject scenario = (JsonObject)json["energizationScenario"]!;
                scenario["seeds"] = new JsonArray(new JsonObject
                {
                    ["seedId"] = Guid.NewGuid(),
                    ["boundaryDeviceId"] = Guid.NewGuid(),
                    ["side"] = "FutureSide"
                });
            });
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));
        }
        finally { File.Delete(path); }
    }

    private static string NextPath() => Path.Combine(Path.GetTempPath(),
        $"ea-{Guid.NewGuid():N}.kvdrawing");

    private static JsonObject Read(string path, string entryName)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using Stream entry = archive.GetEntry(entryName)!.Open();
        return (JsonObject)JsonNode.Parse(entry)!;
    }

    private static void Mutate(string path, string entryName, Action<JsonObject> mutate)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);
        ZipArchiveEntry entry = archive.GetEntry(entryName)!;
        JsonObject json;
        using (Stream source = entry.Open()) json = (JsonObject)JsonNode.Parse(source)!;
        mutate(json);
        entry.Delete();
        using Stream target = archive.CreateEntry(entryName).Open();
        JsonSerializer.Serialize(target, json);
    }
}
