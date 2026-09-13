using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class TransformerNamingCompatibilityTests : IDisposable
{
    private const string MarkerProperty = "transformerNamingContractVersion";
    private readonly List<string> _paths = [];

    [Fact]
    public void OpenWithSource_DistinguishesAbsentMarkerFromIntegerOne()
    {
        string legacyPath = CreateCurrentTransformerProject();
        MutateDocument(legacyPath, payload => payload.Remove(MarkerProperty));
        string currentPath = CreateCurrentTransformerProject();

        ProjectFileOpenResult legacy = new ProjectFileContainer().OpenWithSource(legacyPath);
        ProjectFileOpenResult current = new ProjectFileContainer().OpenWithSource(currentPath);

        Assert.Equal(TransformerNamingContractMode.Legacy, legacy.TransformerNamingMode);
        Assert.Equal(TransformerNamingContractMode.Current, current.TransformerNamingMode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void OpenWithSource_RejectsUnsupportedPresentMarker(string markerJson)
    {
        string path = CreateCurrentTransformerProject();
        MutateDocument(path, payload =>
            payload[MarkerProperty] = JsonNode.Parse(markerJson));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new ProjectFileContainer().OpenWithSource(path));

        Assert.Contains(MarkerProperty, exception.Message);
    }

    [Theory]
    [InlineData("missing", null, true)]
    [InlineData("null", null, true)]
    [InlineData("empty", null, true)]
    [InlineData("whitespace", null, true)]
    [InlineData("valid", "T1", false)]
    public void LegacyMode_RestoresTransformerNamingTruthTable(
        string shape,
        string? expectedName,
        bool expectedIncomplete)
    {
        string path = CreateCurrentTransformerProject();
        MutateDocument(path, payload =>
        {
            payload.Remove(MarkerProperty);
            SetDisplayNameShape(payload, shape);
        });

        ProjectSession session = new ProjectService().LoadProject(path);
        Transformer transformer = Assert.Single(session.Domain.Transformers);

        Assert.Equal(TransformerNamingContractMode.Legacy, session.TransformerNamingMode);
        Assert.Equal(expectedName, transformer.DisplayName);
        Assert.Equal(expectedIncomplete, transformer.IsLegacyNamingIncomplete);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    public void CurrentMode_RejectsInvalidTransformerNamingShapes(string shape)
    {
        string path = CreateCurrentTransformerProject();
        MutateDocument(path, payload => SetDisplayNameShape(payload, shape));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new ProjectService().LoadProject(path));

        Assert.Contains("display name", exception.Message);
    }

    [Fact]
    public void CurrentMode_RejectsUntrimmedTransformerName()
    {
        string path = CreateCurrentTransformerProject();
        MutateDocument(path, payload => SetDisplayNameShape(payload, "padded-valid"));

        Assert.Throws<InvalidDataException>(() =>
            new ProjectService().LoadProject(path));
    }

    [Fact]
    public void Writer_AlwaysWritesCurrentMarkerAndValidDisplayName()
    {
        string path = CreateCurrentTransformerProject("  配变一号  ");

        JsonObject payload = ReadDocument(path);
        JsonObject transformer = GetTransformer(payload);

        Assert.Equal(1, payload[MarkerProperty]!.GetValue<int>());
        Assert.Equal("配变一号", transformer["displayName"]!.GetValue<string>());
        Assert.Equal(ProjectFileFormat.Version7,
            new ProjectFileContainer().Open(path).Manifest.FormatVersion);
    }

    [Fact]
    public void ContainerWriter_RejectsUnnamedTransformerBeforeCreatingTarget()
    {
        string path = NextPath();
        ProjectFileDocument document = CreateFileDocument(displayName: null);

        Assert.Throws<InvalidDataException>(() =>
            new ProjectFileContainer().Save(path, document));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ContainerWriter_RejectsUntrimmedTransformerName()
    {
        string path = NextPath();
        ProjectFileDocument document = CreateFileDocument("  T1  ");

        Assert.Throws<InvalidDataException>(() =>
            new ProjectFileContainer().Save(path, document));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DomainMapper_RejectsLegacyIncompleteTransformer()
    {
        string path = CreateLegacyIncompleteProject();
        ProjectSession session = new ProjectService().LoadProject(path);

        Assert.Throws<InvalidDataException>(() =>
            ProjectDomainMapper.ToDto(session.Domain));
    }

    [Fact]
    public void SaveLegacyIncomplete_IsRejectedWithoutChangingFileOrDirtyState()
    {
        string path = CreateLegacyIncompleteProject();
        var service = new ProjectService();
        ProjectSession loaded = service.LoadProject(path);
        ProjectSession dirty = service.MarkDirty();
        byte[] before = File.ReadAllBytes(path);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.SaveProject(dirty.Layout));

        Assert.Contains("1", exception.Message);
        Assert.Contains("变压器名称", exception.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Same(dirty, service.Current);
        Assert.True(service.Current!.IsDirty);
    }

    [Fact]
    public void SaveAsLegacyIncomplete_DoesNotCreateOrOverwriteTarget()
    {
        string sourcePath = CreateLegacyIncompleteProject();
        var service = new ProjectService();
        ProjectSession loaded = service.LoadProject(sourcePath);
        string missingTarget = NextPath();
        string existingTarget = NextPath();
        byte[] existingBytes = "existing target"u8.ToArray();
        File.WriteAllBytes(existingTarget, existingBytes);

        Assert.Throws<InvalidOperationException>(() =>
            service.SaveProjectAs(missingTarget, loaded.Layout));
        Assert.Throws<InvalidOperationException>(() =>
            service.SaveProjectAs(existingTarget, loaded.Layout));

        Assert.False(File.Exists(missingTarget));
        Assert.Equal(existingBytes, File.ReadAllBytes(existingTarget));
        Assert.Same(loaded, service.Current);
    }

    [Fact]
    public void CompletedLegacyNaming_SaveConvergesToCurrentV7Contract()
    {
        string path = CreateLegacyIncompleteProject();
        var service = new ProjectService();
        ProjectSession legacy = service.LoadProject(path);
        Transformer transformer = Assert.Single(legacy.Domain.Transformers);

        transformer.Rename("  补录后的真实名称  ");
        service.MarkDirty();
        ProjectSession saved = service.SaveProject(legacy.Layout);

        JsonObject payload = ReadDocument(path);
        Assert.Equal(1, payload[MarkerProperty]!.GetValue<int>());
        Assert.Equal(
            "补录后的真实名称",
            GetTransformer(payload)["displayName"]!.GetValue<string>());
        Assert.Equal(ProjectFileFormat.Version7, saved.Manifest.FormatVersion);
        Assert.Equal(TransformerNamingContractMode.Current, saved.TransformerNamingMode);
        Assert.False(saved.IsDirty);

        ProjectSession reopened = new ProjectService().LoadProject(path);
        Transformer reopenedTransformer = Assert.Single(reopened.Domain.Transformers);
        Assert.Equal("补录后的真实名称", reopenedTransformer.DisplayName);
        Assert.False(reopenedTransformer.IsLegacyNamingIncomplete);
        Assert.Equal(TransformerNamingContractMode.Current, reopened.TransformerNamingMode);
    }

    [Fact]
    public void EmptyNewProject_StillWritesCurrentNamingMarker()
    {
        string path = NextPath();

        new ProjectService().CreateProject(path, "无变压器工程");

        Assert.Equal(1, ReadDocument(path)[MarkerProperty]!.GetValue<int>());
    }

    public void Dispose()
    {
        foreach (string path in _paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string CreateCurrentTransformerProject(string displayName = "T1")
    {
        string path = NextPath();
        new ProjectFileContainer().Save(path, CreateFileDocument(displayName.Trim()));
        return path;
    }

    private string CreateLegacyIncompleteProject()
    {
        string path = CreateCurrentTransformerProject();
        MutateDocument(path, payload =>
        {
            payload.Remove(MarkerProperty);
            GetTransformer(payload).Remove("displayName");
        });
        return path;
    }

    private static ProjectFileDocument CreateFileDocument(string? displayName)
    {
        Guid projectId = Guid.NewGuid();
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectDomainDto domain = ProjectDomainDto.Empty(projectId, "Transformer naming") with
        {
            Transformers =
            [
                new ProjectTransformerDto(
                    transformerId,
                    ProjectTransformerKind.PublicIndoor,
                    terminalId,
                    displayName)
            ],
            Terminals =
            [
                new ProjectTerminalDto(
                    terminalId,
                    "device",
                    transformerId,
                    Transformer.HvTerminalRole,
                    Transformer.TenKilovolts,
                    true,
                    false,
                    null,
                    ["cable"])
            ]
        };
        ProjectLayoutDto layout = ProjectLayoutDto.Empty(projectId) with
        {
            TransformerLayouts =
            [
                new ProjectTransformerLayoutDto(
                    transformerId,
                    new ProjectPointDto(10, 20),
                    ProjectTransformerOrientation.Horizontal)
            ]
        };
        return new ProjectFileDocument(
            ProjectFileManifest.Create(
                projectId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new ProjectFileMetadata("Transformer naming"),
            domain,
            layout,
            ProjectProfessionalDto.Empty(projectId));
    }

    private static void SetDisplayNameShape(JsonObject payload, string shape)
    {
        JsonObject transformer = GetTransformer(payload);
        switch (shape)
        {
            case "missing":
                transformer.Remove("displayName");
                break;
            case "null":
                transformer["displayName"] = null;
                break;
            case "empty":
                transformer["displayName"] = string.Empty;
                break;
            case "whitespace":
                transformer["displayName"] = "   ";
                break;
            case "valid":
                transformer["displayName"] = "T1";
                break;
            case "padded-valid":
                transformer["displayName"] = "  T1  ";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }
    }

    private static JsonObject GetTransformer(JsonObject payload)
    {
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        JsonArray transformers = Assert.IsType<JsonArray>(domain["transformers"]);
        return Assert.IsType<JsonObject>(Assert.Single(transformers));
    }

    private static JsonObject ReadDocument(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry entry = archive.GetEntry(ProjectFileFormat.DocumentEntryName)
            ?? throw new InvalidDataException("document.json is missing.");
        using Stream entryStream = entry.Open();
        return Assert.IsType<JsonObject>(JsonNode.Parse(entryStream));
    }

    private static void MutateDocument(string path, Action<JsonObject> mutation)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);
        ZipArchiveEntry entry = archive.GetEntry(ProjectFileFormat.DocumentEntryName)
            ?? throw new InvalidDataException("document.json is missing.");
        JsonObject payload;
        using (Stream entryStream = entry.Open())
        {
            payload = Assert.IsType<JsonObject>(JsonNode.Parse(entryStream));
        }

        mutation(payload);
        entry.Delete();
        ZipArchiveEntry replacement = archive.CreateEntry(
            ProjectFileFormat.DocumentEntryName,
            CompressionLevel.Optimal);
        using Stream replacementStream = replacement.Open();
        JsonSerializer.Serialize(replacementStream, payload);
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-transformer-naming-{Guid.NewGuid():N}.kvdproj");
        _paths.Add(path);
        return path;
    }
}
