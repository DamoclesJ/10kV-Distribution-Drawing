using System.IO;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Desktop.Export;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class ExportDrawingControllerTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void ExportUsesActiveSceneDefaultNameAndDoesNotChangeDirtyState()
    {
        RunOnSta(() =>
        {
            ProjectRuntimeSession session = CreateSession("导出工程");
            AddPoleCommand addPole = new DeviceCommandFactory().CreateAddPole(
                session.PersistenceSession.Domain,
                session.Layout,
                new DocumentPoint(20, 30));
            session.CommandStack.ExecuteCommand(addPole);
            session.RebuildScene();
            bool dirtyBefore = session.IsDirty;
            string outputPath = NextPath(".png");
            var dialog = new TestExportDialog(outputPath);
            var messages = new TestMessages();
            var controller = new ExportDrawingController(
                () => session,
                () => "未命名 3",
                dialog,
                messages);

            Assert.True(controller.ExportPng());

            Assert.Equal("未命名 3.png", dialog.DefaultFileName);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(dirtyBefore, session.IsDirty);
            Assert.Empty(messages.Errors);
        });
    }

    [Fact]
    public void FailedRenderPreservesExistingOutputFileAndSessionState()
    {
        ProjectRuntimeSession session = CreateSession("空图工程");
        string outputPath = NextPath(".png");
        byte[] original = "existing-png-placeholder"u8.ToArray();
        File.WriteAllBytes(outputPath, original);
        bool dirtyBefore = session.IsDirty;
        var messages = new TestMessages();
        var controller = new ExportDrawingController(
            () => session,
            () => "空图工程",
            new TestExportDialog(outputPath),
            messages);

        Assert.False(controller.ExportPng());

        Assert.Equal(original, File.ReadAllBytes(outputPath));
        Assert.Equal(dirtyBefore, session.IsDirty);
        Assert.Single(messages.Errors);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(
                Path.GetDirectoryName(outputPath)!,
                $".{Path.GetFileName(outputPath)}.*.tmp"),
            _ => true);
    }

    private ProjectRuntimeSession CreateSession(string title)
    {
        string path = NextPath(".kvdrawing");
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(path, title);
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    private string NextPath(string extension)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-export-{Guid.NewGuid():N}{extension}");
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists)) File.Delete(path);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class TestExportDialog(string path) : IExportDrawingDialog
    {
        public string? DefaultFileName { get; private set; }
        public string? ChoosePngPath(string defaultFileName)
        {
            DefaultFileName = defaultFileName;
            return path;
        }
    }

    private sealed class TestMessages : IDesktopMessageService
    {
        public List<string> Errors { get; } = [];
        public void ShowError(string title, string message) => Errors.Add(message);
        public void ShowWarning(string title, string message) { }
        public bool Confirm(string title, string message) => true;
        public DirtyDecision ConfirmSaveChanges(string documentName) => DirtyDecision.Cancel;
    }
}
