using System.IO;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Desktop.Export;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
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

            ExportPngOperationResult result = controller.ExportPngDetailed();

            Assert.True(result.IsSuccess);
            Assert.Equal(300, result.SelectedDpi);
            Assert.Equal("未命名 3.png", dialog.DefaultFileName);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(dirtyBefore, session.IsDirty);
            Assert.Empty(messages.Errors);
        });
    }

    [Fact]
    public void DetailedResultCarriesReducedDpiFromRenderer()
    {
        ProjectRuntimeSession session = CreateSession("降分辨率工程");
        string outputPath = NextPath(".png");
        var renderer = new TestRenderer((_, stream, _) =>
        {
            stream.WriteByte(1);
            return Result(237);
        });
        var controller = new ExportDrawingController(
            () => session,
            () => "降分辨率工程",
            new TestExportDialog(outputPath),
            new TestMessages(),
            renderer);

        ExportPngOperationResult result = controller.ExportPngDetailed();

        Assert.Equal(ExportPngStatus.Success, result.Status);
        Assert.Equal(237, result.SelectedDpi);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void CancelledDialogReturnsCancelledWithoutRenderingOrSuccess()
    {
        ProjectRuntimeSession session = CreateSession("取消导出");
        var renderer = new TestRenderer((_, _, _) => throw new InvalidOperationException());
        var controller = new ExportDrawingController(
            () => session,
            () => "取消导出",
            new TestExportDialog(null),
            new TestMessages(),
            renderer);

        ExportPngOperationResult result = controller.ExportPngDetailed();

        Assert.Equal(ExportPngStatus.Cancelled, result.Status);
        Assert.Null(result.SelectedDpi);
        Assert.Equal(0, renderer.CallCount);
    }

    [Fact]
    public void NoSafeReadableDpiReturnsFailureWithSizeMessage()
    {
        AssertCategorizedFailure(
            new PngExportSizeException(),
            "最低可读分辨率");
    }

    [Fact]
    public void IoFailureReturnsFailureWithFileMessage()
    {
        AssertCategorizedFailure(
            new IOException("disk full"),
            "无法写入 PNG 文件");
    }

    [Fact]
    public void UnauthorizedFailureReturnsFailureWithFileMessage()
    {
        AssertCategorizedFailure(
            new UnauthorizedAccessException("denied"),
            "无法写入 PNG 文件");
    }

    [Fact]
    public void RenderFailureReturnsFailureWithRenderMessage()
    {
        AssertCategorizedFailure(
            new PngExportRenderException("encoder failed", new InvalidOperationException()),
            "PNG 渲染或编码失败");
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

    [Fact]
    public void GroundingPresentationDiagnostic_BlocksExportWithExplicitError()
    {
        ProjectRuntimeSession session = CreateSession("接地锚点缺失");
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-missing",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        session.PersistenceSession.Domain.AddDevice(switchDevice);
        foreach (ElectricalNode node in result.ElectricalNodes.Where(
                     node => node.OwnerId == switchDevice.Id))
        {
            session.PersistenceSession.Domain.AddElectricalNode(node);
        }

        foreach (Terminal terminal in result.Terminals.Where(
                     terminal => terminal.OwnerId == switchDevice.Id))
        {
            session.PersistenceSession.Domain.AddTerminal(terminal);
        }

        session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(),
            switchDevice.TerminalIds[0],
            "缺失布局");
        session.RebuildScene();
        string outputPath = NextPath(".png");
        var messages = new TestMessages();
        var controller = new ExportDrawingController(
            () => session,
            () => "接地锚点缺失",
            new TestExportDialog(outputPath),
            messages);

        Assert.False(controller.ExportPng());

        Assert.False(File.Exists(outputPath));
        Assert.Single(messages.Errors);
        Assert.Contains("无法解析专业显示锚点", messages.Errors[0]);
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

    private void AssertCategorizedFailure(Exception exception, string expectedMessage)
    {
        ProjectRuntimeSession session = CreateSession("失败分类");
        string outputPath = NextPath(".png");
        var messages = new TestMessages();
        var controller = new ExportDrawingController(
            () => session,
            () => "失败分类",
            new TestExportDialog(outputPath),
            messages,
            new TestRenderer((_, _, _) => throw exception));

        ExportPngOperationResult result = controller.ExportPngDetailed();

        Assert.Equal(ExportPngStatus.Failed, result.Status);
        Assert.Null(result.SelectedDpi);
        Assert.False(File.Exists(outputPath));
        Assert.Single(messages.Errors);
        Assert.Contains(expectedMessage, messages.Errors[0]);
    }

    private static DrawingSceneBitmapResult Result(int dpi) => new(
        100,
        80,
        dpi,
        new DocumentRect(0, 0, 10, 8),
        new DocumentRect(-10, -10, 30, 28));

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

    private sealed class TestExportDialog(string? path) : IExportDrawingDialog
    {
        public string? DefaultFileName { get; private set; }
        public string? ChoosePngPath(string defaultFileName)
        {
            DefaultFileName = defaultFileName;
            return path;
        }
    }

    private sealed class TestRenderer(
        Func<DrawingScene, Stream, DrawingSceneBitmapOptions?, DrawingSceneBitmapResult> render)
        : IDrawingSceneBitmapRenderer
    {
        public int CallCount { get; private set; }

        public DrawingSceneBitmapResult RenderPng(
            DrawingScene scene,
            Stream output,
            DrawingSceneBitmapOptions? options = null)
        {
            CallCount++;
            return render(scene, output, options);
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
