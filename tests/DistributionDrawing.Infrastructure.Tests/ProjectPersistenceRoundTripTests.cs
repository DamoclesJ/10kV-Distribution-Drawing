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
    public void Version4RoundTrip_OmitsFunctionAndPreservesStructureAndStableIds()
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = GetCabinet(originalDocument);
        string filePath = CreateTemporaryPath("v4-round-trip");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));

            JsonObject savedPayload = ReadArchiveJsonObject(
                filePath,
                ProjectFileFormat.DocumentEntryName);
            Assert.All(GetIntervals(savedPayload), interval =>
                Assert.False(interval.ContainsKey("function")));

            ProjectFileDocument opened = container.Open(filePath);
            DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(opened.Domain!);
            RingCabinet restored = GetCabinet(restoredDocument);

            Assert.Equal(ProjectFileFormat.Version4, opened.Manifest.FormatVersion);
            Assert.Equal(
                original.Intervals.Select(x => x.Sequence),
                restored.Intervals.Select(x => x.Sequence));
            Assert.Equal(
                original.Intervals.Select(x => x.BayIndex),
                restored.Intervals.Select(x => x.BayIndex));
            AssertStableIds(original, restored);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Theory]
    [InlineData(ProjectFileFormat.Version1)]
    [InlineData(ProjectFileFormat.Version2)]
    [InlineData(ProjectFileFormat.Version3)]
    public void LegacyArchive_MigratesToVersion4WithoutChangingStableIds(int sourceVersion)
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = GetCabinet(originalDocument);
        string filePath = CreateTemporaryPath($"legacy-v{sourceVersion}");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));
            SetArchiveVersion(filePath, sourceVersion);

            ProjectFileDocument opened = container.Open(filePath);
            RingCabinet restored = GetCabinet(
                ProjectDomainMapper.ToDomain(opened.Domain!));

            Assert.Equal(ProjectFileFormat.Version4, opened.Manifest.FormatVersion);
            if (sourceVersion <= ProjectFileFormat.Version2)
            {
                Assert.Equal(
                    original.Intervals.Select(x => x.Sequence),
                    restored.Intervals.Select(x => x.BayIndex));
            }
            else
            {
                Assert.Equal(
                    original.Intervals.Select(x => x.BayIndex),
                    restored.Intervals.Select(x => x.BayIndex));
            }
            AssertStableIds(original, restored);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Theory]
    [InlineData("\"unknown\"")]
    [InlineData("\"incoming\"")]
    [InlineData("\"outgoing\"")]
    [InlineData("\"tie\"")]
    [InlineData("\"pt\"")]
    [InlineData("\"metering\"")]
    [InlineData("\"reserve\"")]
    [InlineData("\"arbitrary-legacy-value\"")]
    [InlineData("123")]
    [InlineData("null")]
    public void Version3Archive_DiscardsAnyLegacyFunctionAndPreservesStableIds(
        string legacyJson)
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = GetCabinet(originalDocument);
        string filePath = CreateTemporaryPath("v3-legacy-function");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));
            MutateArchive(filePath, (manifest, payload) =>
            {
                manifest["formatVersion"] = ProjectFileFormat.Version3;
                foreach (JsonObject interval in GetIntervals(payload))
                {
                    interval["function"] = JsonNode.Parse(legacyJson);
                }
            });

            ProjectFileDocument opened = container.Open(filePath);
            RingCabinet restored = GetCabinet(
                ProjectDomainMapper.ToDomain(opened.Domain!));

            AssertStableIds(original, restored);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void Version3Archive_AllowsMissingFunction()
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = GetCabinet(originalDocument);
        string filePath = CreateTemporaryPath("v3-missing-function");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));
            MutateArchive(filePath, (manifest, payload) =>
            {
                manifest["formatVersion"] = ProjectFileFormat.Version3;
                foreach (JsonObject interval in GetIntervals(payload))
                {
                    interval.Remove("function");
                }
            });

            ProjectFileDocument opened = container.Open(filePath);
            RingCabinet restored = GetCabinet(
                ProjectDomainMapper.ToDomain(opened.Domain!));

            AssertStableIds(original, restored);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void Version4Archive_IgnoresExtraLegacyFunctionAndDoesNotWriteItBack()
    {
        DrawingDocument originalDocument = CreateDocumentWithRingCabinet();
        RingCabinet original = GetCabinet(originalDocument);
        string filePath = CreateTemporaryPath("v4-extra-function");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(originalDocument));
            MutateArchive(filePath, (_, payload) =>
            {
                foreach (JsonObject interval in GetIntervals(payload))
                {
                    interval["function"] = "legacy-extra";
                }
            });

            ProjectFileDocument opened = container.Open(filePath);
            RingCabinet restored = GetCabinet(
                ProjectDomainMapper.ToDomain(opened.Domain!));
            AssertStableIds(original, restored);

            container.Save(filePath, opened);
            JsonObject resavedPayload = ReadArchiveJsonObject(
                filePath,
                ProjectFileFormat.DocumentEntryName);
            Assert.All(GetIntervals(resavedPayload), interval =>
                Assert.False(interval.ContainsKey("function")));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void CurrentDto_RejectsNonPositiveBayIndex()
    {
        ProjectDomainDto dto = ProjectDomainMapper.ToDto(CreateDocumentWithRingCabinet());
        ProjectRingCabinetDto cabinet = Assert.Single(dto.RingCabinets);
        ProjectRingCabinetIntervalDto[] intervals = cabinet.Intervals.ToArray();
        intervals[0] = intervals[0] with { BayIndex = 0 };
        ProjectDomainDto invalid = dto with
        {
            RingCabinets = [cabinet with { Intervals = intervals }]
        };

        Assert.Throws<InvalidDataException>(() => ProjectDomainMapper.ToDomain(invalid));
    }

    [Fact]
    public void Version4Archive_StillRejectsMissingBayIndex()
    {
        DrawingDocument document = CreateDocumentWithRingCabinet();
        string filePath = CreateTemporaryPath("v4-missing-bay-index");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, CreateFileDocument(document));
            MutateArchive(filePath, (_, payload) =>
                GetIntervals(payload)[0].Remove("bayIndex"));

            ProjectFileDocument opened = container.Open(filePath);
            Assert.Throws<InvalidDataException>(() =>
                ProjectDomainMapper.ToDomain(opened.Domain!));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    private static DrawingDocument CreateDocumentWithRingCabinet()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Persistence test project");
        RingCabinetIntervalDefinition[] intervals =
        [
            CreateLoadSwitchDefinition(1),
            CreateLoadSwitchDefinition(3),
            CreateLoadSwitchDefinition(7)
        ];
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            intervals));
        document.AddDevice(cabinet);
        return document;
    }

    private static RingCabinetIntervalDefinition CreateLoadSwitchDefinition(int bayIndex)
    {
        return RingCabinetIntervalDefinition.CreateLoadSwitch(
            bayIndex,
            SwitchState.Open,
            SwitchState.Open,
            $"负{bayIndex}间隔");
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

    private static void SetArchiveVersion(string filePath, int sourceVersion)
    {
        MutateArchive(filePath, (manifest, payload) =>
        {
            manifest["formatVersion"] = sourceVersion;
            if (sourceVersion <= ProjectFileFormat.Version2)
            {
                foreach (JsonObject interval in GetIntervals(payload))
                {
                    interval.Remove("bayIndex");
                    interval.Remove("function");
                }
            }
            else
            {
                foreach (JsonObject interval in GetIntervals(payload))
                {
                    interval["function"] = "incoming";
                }
            }

            if (sourceVersion == ProjectFileFormat.Version1)
            {
                payload.Remove("professional");
            }
        });
    }

    private static void MutateArchive(
        string filePath,
        Action<JsonObject, JsonObject> mutation)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false);
        JsonObject manifest = ReadJsonObject(archive, ProjectFileFormat.ManifestEntryName);
        JsonObject payload = ReadJsonObject(archive, ProjectFileFormat.DocumentEntryName);

        mutation(manifest, payload);

        ReplaceJsonEntry(archive, ProjectFileFormat.ManifestEntryName, manifest);
        ReplaceJsonEntry(archive, ProjectFileFormat.DocumentEntryName, payload);
    }

    private static JsonObject ReadArchiveJsonObject(string filePath, string entryName)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        return ReadJsonObject(archive, entryName);
    }

    private static JsonObject ReadJsonObject(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Archive entry '{entryName}' was not found.");
        using Stream stream = entry.Open();
        return JsonNode.Parse(stream) as JsonObject
            ?? throw new InvalidOperationException($"Archive entry '{entryName}' is invalid.");
    }

    private static IReadOnlyList<JsonObject> GetIntervals(JsonObject payload)
    {
        var domain = Assert.IsType<JsonObject>(payload["domain"]);
        var cabinets = Assert.IsType<JsonArray>(domain["ringCabinets"]);
        var cabinet = Assert.IsType<JsonObject>(Assert.Single(cabinets));
        var intervals = Assert.IsType<JsonArray>(cabinet["intervals"]);
        return intervals.Select(node => Assert.IsType<JsonObject>(node)).ToArray();
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

    private static RingCabinet GetCabinet(DrawingDocument document)
    {
        return Assert.Single(document.Devices.OfType<RingCabinet>());
    }

    private static void AssertStableIds(RingCabinet expected, RingCabinet actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.MainBusNodeId, actual.MainBusNodeId);
        Assert.Equal(
            expected.Intervals.Select(x => x.IntervalId),
            actual.Intervals.Select(x => x.IntervalId));
        Assert.Equal(
            expected.ElectricalNodes.Select(x => x.Id).OrderBy(x => x),
            actual.ElectricalNodes.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            expected.Terminals.Select(x => x.Id).OrderBy(x => x),
            actual.Terminals.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            expected.Intervals.SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id).OrderBy(x => x),
            actual.Intervals.SelectMany(x => x.SwitchDevices)
                .Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(
            expected.Intervals.Select(x => x.SwitchAssembly.AssemblyId).OrderBy(x => x),
            actual.Intervals.Select(x => x.SwitchAssembly.AssemblyId).OrderBy(x => x));
    }

    private static string CreateTemporaryPath(string scenario)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-{scenario}-{Guid.NewGuid():N}.kvdrawing");
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
