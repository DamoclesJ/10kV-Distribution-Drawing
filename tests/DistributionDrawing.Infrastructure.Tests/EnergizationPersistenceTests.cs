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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V10RoundTripPreservesScenarioInputsAndIgnoresLegacyCompleteness(bool legacyComplete)
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
            project.EnergizationScenario.SetSourceSetComplete(legacyComplete);
            service.SaveProject();

            ProjectSession restored = new ProjectService().LoadProject(path);
            Assert.Equal(ProjectFileFormat.Version10, restored.Manifest.FormatVersion);
            Assert.Equal(ProjectFileFormat.Version10, restored.OpenedFormatVersion);
            Assert.Equal(project.EnergizationScenario.Id, restored.EnergizationScenario.Id);
            Assert.Equal(legacyComplete, restored.EnergizationScenario.IsSourceSetComplete);
            Assert.Equal(seed, Assert.Single(restored.EnergizationScenario.Seeds));
            EnergizationResult result = new EnergizationAnalyzer().Analyze(
                restored.Domain, restored.EnergizationScenario);
            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(result.Terminals.Values, point => point.State == EnergizationState.Unknown);
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

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void OldVersionRejectsBeforeReadingPayloadAndDoesNotRewriteFile(int version)
    {
        string path = NextPath();
        try
        {
            new ProjectService().CreateProject(path, "no migration");
            Mutate(path, ProjectFileFormat.ManifestEntryName,
                json => json["formatVersion"] = version);
            Mutate(path, ProjectFileFormat.DocumentEntryName,
                json => json.Remove("energizationScenario"));
            byte[] before = File.ReadAllBytes(path);
            var service = new ProjectService();
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => service.LoadProject(path));
            Assert.Contains("version", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(service.Current);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(11)]
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
    public void V10RejectsMissingScenarioCompletenessAndUnexpectedFields()
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
