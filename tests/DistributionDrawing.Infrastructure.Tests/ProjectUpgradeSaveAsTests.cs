using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectUpgradeSaveAsTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void OpenedVersion6_OrdinarySaveIsBlockedAndOriginalRemainsUntouched()
    {
        string sourcePath = CreateVersion6Project();
        byte[] originalBytes = File.ReadAllBytes(sourcePath);
        var service = new ProjectService();
        ProjectSession opened = service.LoadProject(sourcePath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => service.SaveProject(opened.Layout));

        Assert.True(opened.RequiresUpgradeSaveAs);
        Assert.Equal(ProjectFileFormat.Version6, opened.OpenedFormatVersion);
        Assert.Contains("另存为", exception.Message);
        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        Assert.Same(opened, service.Current);
    }

    [Fact]
    public void OpenedVersion6_SaveAsWritesV7ThenAllowsOrdinarySave()
    {
        string sourcePath = CreateVersion6Project();
        string targetPath = NextPath();
        byte[] originalBytes = File.ReadAllBytes(sourcePath);
        var service = new ProjectService();
        ProjectSession opened = service.LoadProject(sourcePath);

        ProjectSession upgraded = service.SaveProjectAs(targetPath, opened.Layout);

        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(Path.GetFullPath(targetPath), upgraded.FilePath);
        Assert.Equal(ProjectFileFormat.Version7, upgraded.Manifest.FormatVersion);
        Assert.Equal(ProjectFileFormat.Version7, upgraded.OpenedFormatVersion);
        Assert.False(upgraded.RequiresUpgradeSaveAs);

        ProjectSession savedAgain = service.SaveProject(upgraded.Layout);
        Assert.Equal(Path.GetFullPath(targetPath), savedAgain.FilePath);
        Assert.False(savedAgain.RequiresUpgradeSaveAs);
        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    public void OpenedVersion6_SaveAsCannotTargetOriginalPath()
    {
        string sourcePath = CreateVersion6Project();
        byte[] originalBytes = File.ReadAllBytes(sourcePath);
        var service = new ProjectService();
        ProjectSession opened = service.LoadProject(sourcePath);

        Assert.Throws<InvalidOperationException>(() =>
            service.SaveProjectAs(sourcePath, opened.Layout));

        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        Assert.Same(opened, service.Current);
        Assert.True(service.Current!.RequiresUpgradeSaveAs);
    }

    [Fact]
    public void FailedUpgradeSaveAs_DoesNotTransitionSessionToV7()
    {
        string sourcePath = CreateVersion6Project();
        byte[] originalBytes = File.ReadAllBytes(sourcePath);
        string invalidTarget = NextPath();
        Directory.CreateDirectory(invalidTarget);
        var service = new ProjectService();
        ProjectSession opened = service.LoadProject(sourcePath);

        try
        {
            Exception? exception = Record.Exception(() =>
                service.SaveProjectAs(invalidTarget, opened.Layout));

            Assert.NotNull(exception);
            Assert.True(
                exception is IOException or UnauthorizedAccessException,
                $"Expected a file-system Save As failure, got {exception.GetType().FullName}.");
        }
        finally
        {
            Directory.Delete(invalidTarget);
        }

        Assert.Same(opened, service.Current);
        Assert.Equal(sourcePath, service.Current!.FilePath);
        Assert.Equal(ProjectFileFormat.Version6, service.Current.OpenedFormatVersion);
        Assert.True(service.Current.RequiresUpgradeSaveAs);
        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    public void NewAndOpenedVersion7_AllowOrdinarySave()
    {
        string path = NextPath();
        var service = new ProjectService();
        ProjectSession created = service.CreateProject(path, "V7 保存");

        ProjectSession firstSave = service.SaveProject(created.Layout);
        var reopenedService = new ProjectService();
        ProjectSession reopened = reopenedService.LoadProject(path);
        ProjectSession secondSave = reopenedService.SaveProject(reopened.Layout);

        Assert.False(firstSave.RequiresUpgradeSaveAs);
        Assert.False(reopened.RequiresUpgradeSaveAs);
        Assert.False(secondSave.RequiresUpgradeSaveAs);
        Assert.Equal(ProjectFileFormat.Version7, secondSave.OpenedFormatVersion);
    }

    [Fact]
    public void UnsupportedFutureVersion_FailsInsteadOfBeingReadAsV7()
    {
        string path = NextPath();
        new ProjectService().CreateProject(path, "未来格式");
        using (var stream = new FileStream(
                   path,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false))
        {
            JsonObject manifest = ReadJson(archive, ProjectFileFormat.ManifestEntryName);
            manifest["formatVersion"] = ProjectFileFormat.CurrentVersion + 1;
            ReplaceJson(archive, ProjectFileFormat.ManifestEntryName, manifest);
        }

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new ProjectFileContainer().Open(path));

        Assert.Contains("Unsupported project format version", exception.Message);
    }

    [Fact]
    public void MalformedV7TypedEnum_FailsExplicitly()
    {
        string path = NextPath();
        new ProjectService().CreateProject(path, "损坏 V7");
        using (var stream = new FileStream(
                   path,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false))
        {
            JsonObject payload = ReadJson(archive, ProjectFileFormat.DocumentEntryName);
            JsonObject professional = Assert.IsType<JsonObject>(payload["professional"]);
            professional["groundingAccessPoints"] = new JsonArray
            {
                new JsonObject
                {
                    ["groundingAccessPointId"] = Guid.NewGuid(),
                    ["connectionId"] = Guid.NewGuid(),
                    ["poleId"] = Guid.NewGuid(),
                    ["lineSide"] = "Sideways"
                }
            };
            ReplaceJson(archive, ProjectFileFormat.DocumentEntryName, payload);
        }

        Assert.Throws<JsonException>(() => new ProjectFileContainer().Open(path));
    }

    public void Dispose()
    {
        foreach (string path in _paths)
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    private string CreateVersion6Project()
    {
        string path = NextPath();
        new ProjectService().CreateProject(path, "V6 来源工程");
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false);
        JsonObject manifest = ReadJson(archive, ProjectFileFormat.ManifestEntryName);
        JsonObject payload = ReadJson(archive, ProjectFileFormat.DocumentEntryName);
        manifest["formatVersion"] = ProjectFileFormat.Version6;
        Assert.IsType<JsonObject>(payload["domain"]).Remove("transformers");
        Assert.IsType<JsonObject>(payload["domain"]).Remove("customerStations");
        Assert.IsType<JsonObject>(payload["professional"]).Remove("groundingAccessPoints");
        Assert.IsType<JsonObject>(payload["layout"]).Remove("transformerLayouts");
        Assert.IsType<JsonObject>(payload["layout"]).Remove("customerStationLayouts");
        Assert.IsType<JsonObject>(payload["layout"]).Remove("groundingPointLayouts");
        ReplaceJson(archive, ProjectFileFormat.ManifestEntryName, manifest);
        ReplaceJson(archive, ProjectFileFormat.DocumentEntryName, payload);
        return path;
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-upgrade-save-as-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    private static JsonObject ReadJson(ZipArchive archive, string entryName)
    {
        using Stream stream = archive.GetEntry(entryName)!.Open();
        return Assert.IsType<JsonObject>(JsonNode.Parse(stream));
    }

    private static void ReplaceJson(ZipArchive archive, string entryName, JsonObject value)
    {
        archive.GetEntry(entryName)!.Delete();
        using Stream stream = archive.CreateEntry(entryName).Open();
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }
}
