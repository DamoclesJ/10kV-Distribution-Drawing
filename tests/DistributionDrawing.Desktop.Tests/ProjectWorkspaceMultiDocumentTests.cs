using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Rendering;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class ProjectWorkspaceMultiDocumentTests : IDisposable
{
    private readonly List<string> _paths = [];
    private readonly List<ProjectWorkspaceController> _controllers = [];

    [Fact]
    public void NewAddsAndActivatesUntitledSessionsWithoutRemovingExisting()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        ProjectWorkspaceController controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.Equal("未命名 1", first.DocumentName);
        Assert.True(first.IsUntitled);

        Assert.True(controller.NewProject());
        Assert.Equal(2, controller.Workspace.Sessions.Count);
        Assert.Equal("未命名 2", controller.ActiveDocumentSession!.DocumentName);
        Assert.Contains(first, controller.Workspace.Sessions);
    }

    [Fact]
    public void OpenAddsSessionAndDuplicateCanonicalPathActivatesExisting()
    {
        string firstPath = CreateSavedProject("工程 A");
        string secondPath = CreateSavedProject("工程 B");
        var dialogs = new TestDialogs();
        dialogs.OpenPaths.Enqueue(firstPath);
        dialogs.OpenPaths.Enqueue(secondPath);
        dialogs.OpenPaths.Enqueue(firstPath.ToUpperInvariant());
        ProjectWorkspaceController controller = CreateController(dialogs);

        Assert.True(controller.OpenProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.True(controller.OpenProject());
        Assert.Equal(2, controller.Workspace.Sessions.Count);
        Assert.True(controller.OpenProject());

        Assert.Same(first, controller.ActiveDocumentSession);
        Assert.Equal(2, controller.Workspace.Sessions.Count);
    }

    [Fact]
    public void SaveOnlyChangesActiveSessionAndClearsItsDirtyMarker()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        first.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        DocumentSession second = controller.ActiveDocumentSession!;
        second.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());

        controller.Workspace.ActivateSession(first);
        Assert.True(controller.SaveProject());

        Assert.False(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.DoesNotContain("*", first.TabTitle);
        Assert.Contains("*", second.TabTitle);
    }

    [Fact]
    public void LegacyIncompleteTransformer_SaveUxTracksRenameUndoRedoEligibility()
    {
        string sourcePath = CreateLegacyIncompleteTransformerProject();
        string targetPath = NextPath();
        byte[] sourceBefore = File.ReadAllBytes(sourcePath);
        var dialogs = new TestDialogs();
        dialogs.OpenPaths.Enqueue(sourcePath);
        dialogs.SaveAsPaths.Enqueue(targetPath);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.OpenProject());
        ProjectRuntimeSession runtime = controller.CurrentSession!;
        runtime.CommandStack.ExecuteCommand(new TestCommand());
        Transformer transformer = Assert.Single(runtime.PersistenceSession.Domain.Transformers);

        Assert.False(controller.SaveProject());
        Assert.True(controller.IsDirty);
        Assert.Equal(sourceBefore, File.ReadAllBytes(sourcePath));
        Assert.Contains(dialogs.Errors, message =>
            message.Contains("1 台历史变压器", StringComparison.Ordinal) &&
            message.Contains("变压器名称", StringComparison.Ordinal));

        Assert.False(controller.SaveProjectAs());
        Assert.False(File.Exists(targetPath));
        Assert.True(controller.IsDirty);

        runtime.CommandStack.ExecuteCommand(
            new RenameTransformerCommand(transformer, "补录名称"),
            runtime.RebuildScene);
        Assert.True(controller.SaveProject());
        Assert.False(controller.IsDirty);
        Assert.Equal(TransformerNamingContractMode.Current,
            new ProjectFileContainer().OpenWithSource(sourcePath).TransformerNamingMode);

        Assert.True(runtime.CommandStack.Undo());
        runtime.RebuildScene();
        Assert.True(transformer.IsLegacyNamingIncomplete);
        Assert.True(controller.IsDirty);
        Assert.False(controller.SaveProject());

        Assert.True(runtime.CommandStack.Redo());
        runtime.RebuildScene();
        dialogs.SaveAsPaths.Enqueue(targetPath);
        Assert.True(controller.SaveProjectAs());
        Assert.False(controller.IsDirty);
        ProjectSession reopened = new ProjectService().LoadProject(targetPath);
        Assert.Equal(TransformerNamingContractMode.Current, reopened.TransformerNamingMode);
        Assert.Equal("补录名称", Assert.Single(reopened.Domain.Transformers).DisplayName);
    }

    [Fact]
    public void OrdinarySaveForOpenedV6_UsesSaveAsAndThenBecomesNormalV7Session()
    {
        string sourcePath = CreateVersion6Project();
        string targetPath = NextPath();
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        var dialogs = new TestDialogs();
        dialogs.OpenPaths.Enqueue(sourcePath);
        dialogs.SaveAsPaths.Enqueue(targetPath);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.OpenProject());

        Assert.True(controller.SaveProject());

        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(Path.GetFullPath(targetPath), controller.ActiveDocumentSession!.FilePath);
        Assert.False(controller.CurrentSession!.PersistenceSession.RequiresUpgradeSaveAs);
        Assert.Equal(
            ProjectFileFormat.Version7,
            new ProjectFileContainer().OpenWithSource(targetPath).OpenedFormatVersion);
        Assert.True(controller.SaveProject());
    }

    [Fact]
    public void CancelledUpgradeSaveAs_LeavesV6SessionAndOriginalUntouched()
    {
        string sourcePath = CreateVersion6Project();
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        var dialogs = new TestDialogs();
        dialogs.OpenPaths.Enqueue(sourcePath);
        dialogs.SaveAsPaths.Enqueue(null);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.OpenProject());

        Assert.False(controller.SaveProject());

        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal(Path.GetFullPath(sourcePath), controller.ActiveDocumentSession!.FilePath);
        Assert.True(controller.CurrentSession!.PersistenceSession.RequiresUpgradeSaveAs);
    }

    [Fact]
    public void SaveAsPersistsUntitledAndRejectsPathOwnedByAnotherSession()
    {
        string firstPath = NextPath();
        string secondPath = NextPath();
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(firstPath, "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.SaveAsPaths.Enqueue(firstPath);
        dialogs.SaveAsPaths.Enqueue(secondPath);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        Assert.True(controller.NewProject());
        DocumentSession untitled = controller.ActiveDocumentSession!;

        Assert.False(controller.SaveProjectAs());
        Assert.True(untitled.IsUntitled);
        Assert.NotEmpty(dialogs.Errors);

        Assert.True(controller.SaveProjectAs());
        Assert.False(untitled.IsUntitled);
        Assert.Equal(Path.GetFullPath(secondPath), untitled.FilePath);
    }

    [Theory]
    [InlineData(DirtyDecision.Discard, true)]
    [InlineData(DirtyDecision.Cancel, false)]
    public void CloseDirtySessionHonorsDiscardAndCancel(
        DirtyDecision decision,
        bool expectedClosed)
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "关闭测试", null));
        dialogs.DirtyDecisions.Enqueue(decision);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.Equal(expectedClosed, controller.CloseCurrentProject());
        Assert.Equal(expectedClosed ? 0 : 1, controller.Workspace.Sessions.Count);
    }

    [Fact]
    public void CloseDirtySavePersistsThenLeavesEmptyWorkspace()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "保存关闭", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.True(controller.CloseCurrentProject());
        Assert.Empty(controller.Workspace.Sessions);
        Assert.Null(controller.ActiveDocumentSession);
    }

    [Fact]
    public void ClosingNonActiveSessionTargetsThatTabAndKeepsActiveSession()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.True(controller.NewProject());
        DocumentSession active = controller.ActiveDocumentSession!;

        Assert.True(controller.CloseProject(first));

        Assert.Same(active, controller.ActiveDocumentSession);
        Assert.DoesNotContain(first, controller.Workspace.Sessions);
    }

    [Fact]
    public void CancellingDirtyNonActiveTabCloseRestoresOriginalActiveSession()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Cancel);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        first.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        DocumentSession active = controller.ActiveDocumentSession!;

        Assert.False(controller.CloseProject(first));

        Assert.Same(active, controller.ActiveDocumentSession);
        Assert.Contains(first, controller.Workspace.Sessions);
    }

    [Fact]
    public void ExitChecksEveryDirtySessionAndCancelStopsExit()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Discard);
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Cancel);
        ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.False(controller.CanCloseApplication());
        Assert.Equal(2, dialogs.ConfirmedDocuments.Count);
        Assert.Equal(2, controller.Workspace.Sessions.Count);
    }

    [Fact]
    public void ClosingActiveMiddleSessionSelectsNextAndNewWorksAfterEmptyWorkspace()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 C", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        using ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        Assert.True(controller.NewProject());
        DocumentSession middle = controller.ActiveDocumentSession!;
        Assert.True(controller.NewProject());
        DocumentSession last = controller.ActiveDocumentSession!;

        controller.Workspace.ActivateSession(middle);
        Assert.True(controller.CloseCurrentProject());
        Assert.Same(last, controller.ActiveDocumentSession);
        Assert.DoesNotContain(middle, controller.Workspace.Sessions);

        controller.Workspace.ActivateSession(first);
        Assert.True(controller.CloseCurrentProject());
        Assert.True(controller.CloseCurrentProject());
        Assert.Empty(controller.Workspace.Sessions);
        Assert.Null(controller.ActiveDocumentSession);

        Assert.True(controller.NewProject());
        Assert.Single(controller.Workspace.Sessions);
        Assert.Equal("未命名 1", controller.ActiveDocumentSession!.DocumentName);
    }

    [Fact]
    public void SaveAsImmediatelyUpdatesCanonicalIdentityForDuplicateOpen()
    {
        string path = NextPath();
        string normalizedAlias = Path.Combine(
            Path.GetDirectoryName(path)!,
            ".",
            Path.GetFileName(path));
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.SaveAsPaths.Enqueue(path);
        dialogs.OpenPaths.Enqueue(normalizedAlias);
        using ProjectWorkspaceController controller = CreateController(dialogs);

        Assert.True(controller.NewProject());
        DocumentSession original = controller.ActiveDocumentSession!;
        Assert.True(controller.SaveProjectAs());
        Assert.True(controller.OpenProject());

        Assert.Same(original, controller.ActiveDocumentSession);
        Assert.Single(controller.Workspace.Sessions);
    }

    [Fact]
    public void UnnamedSaveAsCancellationPreventsClose()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        using ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.False(controller.CloseCurrentProject());

        Assert.Single(controller.Workspace.Sessions);
        Assert.True(controller.ActiveDocumentSession!.IsUntitled);
        Assert.True(controller.ActiveDocumentSession.IsDirty);
    }

    [Fact]
    public void UnnamedSaveAsCancellationPreventsApplicationExit()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        using ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());

        Assert.False(controller.CanCloseApplication());

        Assert.Single(controller.Workspace.Sessions);
        Assert.True(controller.ActiveDocumentSession!.IsUntitled);
        Assert.True(controller.ActiveDocumentSession.IsDirty);
    }

    [Fact]
    public void FailedSavePreventsCloseAndApplicationExit()
    {
        string path = NextPath();
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(path, "保存失败", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        using ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        controller.CurrentSession!.CommandStack.ExecuteCommand(new TestCommand());
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            Assert.False(controller.CloseCurrentProject());
            Assert.Single(controller.Workspace.Sessions);
            Assert.False(controller.CanCloseApplication());
            Assert.Single(controller.Workspace.Sessions);
            Assert.Equal(2, dialogs.Errors.Count);
        }
        finally
        {
            Directory.Delete(path);
        }
    }

    [Fact]
    public void ExitCanSaveFirstDirtySessionThenCancelSecond()
    {
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 A", null));
        dialogs.NewRequests.Enqueue(new NewProjectRequest(NextPath(), "工程 B", null));
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Save);
        dialogs.DirtyDecisions.Enqueue(DirtyDecision.Cancel);
        using ProjectWorkspaceController controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        DocumentSession first = controller.ActiveDocumentSession!;
        first.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());
        Assert.True(controller.NewProject());
        DocumentSession second = controller.ActiveDocumentSession!;
        second.RuntimeSession.CommandStack.ExecuteCommand(new TestCommand());

        Assert.False(controller.CanCloseApplication());

        Assert.False(first.IsDirty);
        Assert.True(second.IsDirty);
        Assert.Same(second, controller.ActiveDocumentSession);
    }

    [Fact]
    public void UntitledBackingFileIsRemovedAfterSaveAsAndWorkspaceDispose()
    {
        string savedPath = NextPath();
        var dialogs = new TestDialogs();
        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        dialogs.SaveAsPaths.Enqueue(savedPath);
        var controller = CreateController(dialogs);
        Assert.True(controller.NewProject());
        string firstBackingPath = controller.ActiveDocumentSession!.FilePath;
        Assert.True(File.Exists(firstBackingPath));

        Assert.True(controller.SaveProjectAs());
        Assert.False(File.Exists(firstBackingPath));

        dialogs.NewRequests.Enqueue(new NewProjectRequest(string.Empty, string.Empty, null));
        Assert.True(controller.NewProject());
        string secondBackingPath = controller.ActiveDocumentSession!.FilePath;
        Assert.True(File.Exists(secondBackingPath));

        controller.Dispose();

        Assert.False(File.Exists(secondBackingPath));
    }

    private ProjectWorkspaceController CreateController(TestDialogs dialogs)
    {
        var controller = new ProjectWorkspaceController(dialogs, new DrawingSceneBuilder());
        _controllers.Add(controller);
        return controller;
    }

    private string CreateSavedProject(string title)
    {
        string path = NextPath();
        var service = new ProjectService();
        ProjectSession session = service.CreateProject(path, title);
        service.SaveProject(session.Layout);
        return path;
    }

    private string CreateVersion6Project()
    {
        string path = CreateSavedProject("V6 升级工程");
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

    private string CreateLegacyIncompleteTransformerProject()
    {
        string path = NextPath();
        Guid projectId = Guid.NewGuid();
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectDomainDto domain = ProjectDomainDto.Empty(projectId, "历史变压器") with
        {
            Transformers =
            [
                new ProjectTransformerDto(
                    transformerId,
                    ProjectTransformerKind.PublicIndoor,
                    terminalId,
                    "待移除名称")
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
        var document = new ProjectFileDocument(
            ProjectFileManifest.Create(
                projectId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new ProjectFileMetadata("历史变压器"),
            domain,
            layout,
            ProjectProfessionalDto.Empty(projectId));
        new ProjectFileContainer().Save(path, document);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: false);
        JsonObject payload = ReadJson(archive, ProjectFileFormat.DocumentEntryName);
        payload.Remove("transformerNamingContractVersion");
        JsonObject payloadDomain = Assert.IsType<JsonObject>(payload["domain"]);
        JsonArray transformers = Assert.IsType<JsonArray>(payloadDomain["transformers"]);
        Assert.IsType<JsonObject>(Assert.Single(transformers)).Remove("displayName");
        ReplaceJson(archive, ProjectFileFormat.DocumentEntryName, payload);
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

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-tabs-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (ProjectWorkspaceController controller in _controllers)
        {
            controller.Dispose();
        }

        foreach (string path in _paths.Where(File.Exists)) File.Delete(path);
    }

    private sealed class TestDialogs : IProjectWorkspaceDialogs
    {
        public Queue<NewProjectRequest?> NewRequests { get; } = new();
        public Queue<string?> OpenPaths { get; } = new();
        public Queue<string?> SaveAsPaths { get; } = new();
        public Queue<DirtyDecision> DirtyDecisions { get; } = new();
        public List<string> Errors { get; } = [];
        public List<string> ConfirmedDocuments { get; } = [];

        public NewProjectRequest? RequestNewProject() =>
            NewRequests.Count == 0 ? null : NewRequests.Dequeue();

        public string? ChooseOpenProject() =>
            OpenPaths.Count == 0 ? null : OpenPaths.Dequeue();

        public string? ChooseSaveAs(string? currentFilePath) =>
            SaveAsPaths.Count == 0 ? null : SaveAsPaths.Dequeue();

        public DirtyDecision ConfirmDirty(string operation) =>
            DirtyDecisions.Count == 0 ? DirtyDecision.Cancel : DirtyDecisions.Dequeue();

        public DirtyDecision ConfirmDirtyDocument(string documentName, string operation)
        {
            ConfirmedDocuments.Add(documentName);
            return ConfirmDirty(operation);
        }

        public void ShowError(string title, string message) => Errors.Add(message);
    }

    private sealed class TestCommand : ICommand
    {
        public void Execute() { }
        public void Undo() { }
        public void Redo() { }
    }
}
