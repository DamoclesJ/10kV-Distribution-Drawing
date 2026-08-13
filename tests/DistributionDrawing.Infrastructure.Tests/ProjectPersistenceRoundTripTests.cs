using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectPersistenceRoundTripTests
{
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public void Version3RoundTrip_PreservesBayMetadataAndStableIds()
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = Assert.Single(originalDocument.Devices.OfType<RingCabinet>());
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-{Guid.NewGuid():N}.kvdrawing");

        try
        {
            var container = new ProjectFileContainer();
            var metadata = new ProjectFileMetadata(originalDocument.Title);
            var fileDocument = new ProjectFileDocument(
                ProjectFileManifest.Create(
                    originalDocument.Id,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                metadata,
                ProjectDomainMapper.ToDto(originalDocument),
                ProjectLayoutDto.Empty(originalDocument.Id),
                ProjectProfessionalDto.Empty(originalDocument.Id));

            container.Save(filePath, fileDocument);
            ProjectFileDocument opened = container.Open(filePath);
            DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(opened.Domain!);
            RingCabinet restored = Assert.Single(restoredDocument.Devices.OfType<RingCabinet>());

            Assert.Equal(ProjectFileFormat.CurrentVersion, opened.Manifest.FormatVersion);
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.MainBusNodeId, restored.MainBusNodeId);
            Assert.Equal(
                original.Intervals.Select(interval => interval.Sequence),
                restored.Intervals.Select(interval => interval.Sequence));
            Assert.Equal(
                original.Intervals.Select(interval => interval.BayIndex),
                restored.Intervals.Select(interval => interval.BayIndex));
            Assert.Equal(
                original.Intervals.Select(interval => interval.Function),
                restored.Intervals.Select(interval => interval.Function));
            Assert.Equal(
                original.Intervals.Select(interval => interval.IntervalId),
                restored.Intervals.Select(interval => interval.IntervalId));
            Assert.Equal(
                original.ElectricalNodes.Select(node => node.Id).OrderBy(id => id),
                restored.ElectricalNodes.Select(node => node.Id).OrderBy(id => id));
            Assert.Equal(
                original.Terminals.Select(terminal => terminal.Id).OrderBy(id => id),
                restored.Terminals.Select(terminal => terminal.Id).OrderBy(id => id));
            Assert.Equal(
                original.Intervals.SelectMany(interval => interval.SwitchDevices)
                    .Select(device => device.Id).OrderBy(id => id),
                restored.Intervals.SelectMany(interval => interval.SwitchDevices)
                    .Select(device => device.Id).OrderBy(id => id));
            Assert.Equal(
                original.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId),
                restored.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData(ProjectFileFormat.Version1)]
    [InlineData(ProjectFileFormat.Version2)]
    public void LegacyArchive_MigratesToVersion3WithoutChangingStableIds(int sourceVersion)
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = Assert.Single(originalDocument.Devices.OfType<RingCabinet>());
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-legacy-{Guid.NewGuid():N}.kvdrawing");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));
            DowngradeArchive(filePath, sourceVersion);

            ProjectFileDocument opened = container.Open(filePath);
            DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(opened.Domain!);
            RingCabinet restored = Assert.Single(restoredDocument.Devices.OfType<RingCabinet>());

            Assert.Equal(ProjectFileFormat.CurrentVersion, opened.Manifest.FormatVersion);
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(
                original.Intervals.Select(interval => interval.IntervalId),
                restored.Intervals.Select(interval => interval.IntervalId));
            Assert.Equal(
                original.Intervals.Select(interval => interval.Sequence),
                restored.Intervals.Select(interval => interval.BayIndex));
            Assert.All(
                restored.Intervals,
                interval => Assert.Equal(BayFunction.Unknown, interval.Function));
            Assert.Equal(
                original.ElectricalNodes.Select(node => node.Id).OrderBy(id => id),
                restored.ElectricalNodes.Select(node => node.Id).OrderBy(id => id));
            Assert.Equal(
                original.Terminals.Select(terminal => terminal.Id).OrderBy(id => id),
                restored.Terminals.Select(terminal => terminal.Id).OrderBy(id => id));
            Assert.Equal(
                original.Intervals.SelectMany(interval => interval.SwitchDevices)
                    .Select(device => device.Id).OrderBy(id => id),
                restored.Intervals.SelectMany(interval => interval.SwitchDevices)
                    .Select(device => device.Id).OrderBy(id => id));
            Assert.Equal(
                original.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId),
                restored.Intervals.Select(interval => interval.SwitchAssembly.AssemblyId));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData(false, "outgoing")]
    [InlineData(true, null)]
    [InlineData(true, "unsupported-function")]
    public void CurrentDto_RejectsMissingOrInvalidBayMetadata(
        bool validBayIndex,
        string? function)
    {
        ProjectDomainDto dto = ProjectDomainMapper.ToDto(CreateDocumentWithRingCabinet());
        ProjectRingCabinetDto cabinet = Assert.Single(dto.RingCabinets);
        ProjectRingCabinetIntervalDto[] intervals = cabinet.Intervals.ToArray();
        intervals[0] = intervals[0] with
        {
            BayIndex = validBayIndex ? intervals[0].BayIndex : 0,
            Function = function!
        };
        ProjectDomainDto invalid = dto with
        {
            RingCabinets = [cabinet with { Intervals = intervals }]
        };

        Assert.Throws<InvalidDataException>(() => ProjectDomainMapper.ToDomain(invalid));
    }

    [Theory]
    [InlineData("missing-bay-index")]
    [InlineData("missing-function")]
    [InlineData("invalid-function")]
    public void CorruptedVersion3Archive_FailsDuringDomainRestore(string corruption)
    {
        DrawingDocument document = CreateDocumentWithRingCabinet();
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-corrupt-{Guid.NewGuid():N}.kvdrawing");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(document));
            CorruptBayMetadata(filePath, corruption);

            Exception exception = Assert.ThrowsAny<Exception>(() =>
            {
                ProjectFileDocument opened = container.Open(filePath);
                _ = ProjectDomainMapper.ToDomain(opened.Domain!);
            });
            Assert.True(exception is InvalidDataException or JsonException);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static DrawingDocument CreateDocumentWithRingCabinet()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Persistence test project");
        RingCabinetIntervalDefinition[] intervals =
        [
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                1,
                BayFunction.Incoming,
                SwitchState.Open,
                SwitchState.Open,
                "负1间隔"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                3,
                BayFunction.Outgoing,
                SwitchState.Open,
                SwitchState.Open,
                "负3间隔"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                7,
                BayFunction.Reserve,
                SwitchState.Open,
                SwitchState.Open,
                "负7间隔")
        ];
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            intervals));
        document.AddDevice(cabinet);
        return document;
    }

    private static ProjectFileDocument CreateFileDocument(DrawingDocument document)
    {
        return new ProjectFileDocument(
            ProjectFileManifest.Create(
                document.Id,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new ProjectFileMetadata(document.Title),
            ProjectDomainMapper.ToDto(document),
            ProjectLayoutDto.Empty(document.Id),
            ProjectProfessionalDto.Empty(document.Id));
    }

    private static void DowngradeArchive(string filePath, int sourceVersion)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false);

        JsonObject manifest = ReadJsonObject(archive, ProjectFileFormat.ManifestEntryName);
        manifest["formatVersion"] = sourceVersion;
        ReplaceJsonEntry(archive, ProjectFileFormat.ManifestEntryName, manifest);

        JsonObject payload = ReadJsonObject(archive, ProjectFileFormat.DocumentEntryName);
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        JsonArray cabinets = Assert.IsType<JsonArray>(domain["ringCabinets"]);
        foreach (JsonNode? cabinetNode in cabinets)
        {
            JsonObject cabinet = Assert.IsType<JsonObject>(cabinetNode);
            JsonArray intervals = Assert.IsType<JsonArray>(cabinet["intervals"]);
            foreach (JsonNode? intervalNode in intervals)
            {
                JsonObject interval = Assert.IsType<JsonObject>(intervalNode);
                interval.Remove("bayIndex");
                interval.Remove("function");
            }
        }

        if (sourceVersion == ProjectFileFormat.Version1)
        {
            payload.Remove("professional");
        }

        ReplaceJsonEntry(archive, ProjectFileFormat.DocumentEntryName, payload);
    }

    private static void CorruptBayMetadata(string filePath, string corruption)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false);
        JsonObject payload = ReadJsonObject(archive, ProjectFileFormat.DocumentEntryName);
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        JsonArray cabinets = Assert.IsType<JsonArray>(domain["ringCabinets"]);
        JsonObject cabinet = Assert.IsType<JsonObject>(Assert.Single(cabinets));
        JsonArray intervals = Assert.IsType<JsonArray>(cabinet["intervals"]);
        JsonObject interval = Assert.IsType<JsonObject>(intervals[0]);

        switch (corruption)
        {
            case "missing-bay-index":
                interval.Remove("bayIndex");
                break;
            case "missing-function":
                interval.Remove("function");
                break;
            case "invalid-function":
                interval["function"] = "unsupported-function";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }

        ReplaceJsonEntry(archive, ProjectFileFormat.DocumentEntryName, payload);
    }

    private static JsonObject ReadJsonObject(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Archive entry '{entryName}' was not found.");
        using Stream stream = entry.Open();
        return JsonNode.Parse(stream) as JsonObject
            ?? throw new InvalidOperationException($"Archive entry '{entryName}' is invalid.");
    }

    private static void ReplaceJsonEntry(
        ZipArchive archive,
        string entryName,
        JsonObject value)
    {
        archive.GetEntry(entryName)?.Delete();
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        JsonSerializer.Serialize(stream, value, ArchiveJsonOptions);
    }
}
