using Microsoft.Win32;
using System.Windows;
using System.IO;
using DistributionDrawing.Desktop.Actions;
using DistributionDrawing.Rendering.Wpf.Rendering;

namespace DistributionDrawing.Desktop.Export;

public interface IExportDrawingDialog
{
    string? ChoosePngPath(string defaultFileName);
}

public enum ExportPngStatus
{
    Success,
    Cancelled,
    Failed
}

public sealed record ExportPngOperationResult(ExportPngStatus Status, int? SelectedDpi = null)
{
    public bool IsSuccess => Status == ExportPngStatus.Success && SelectedDpi.HasValue;
}

public sealed class WpfExportDrawingDialog : IExportDrawingDialog
{
    private readonly Window _owner;

    public WpfExportDrawingDialog(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string? ChoosePngPath(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图像 (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = defaultFileName,
            Title = "导出 PNG"
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }
}

public sealed class ExportDrawingController
{
    private readonly Func<ProjectRuntimeSession?> _activeSession;
    private readonly Func<string> _activeDocumentName;
    private readonly IExportDrawingDialog _dialog;
    private readonly IDesktopMessageService _messages;
    private readonly IDrawingSceneBitmapRenderer _renderer;

    public ExportDrawingController(
        Func<ProjectRuntimeSession?> activeSession,
        Func<string> activeDocumentName,
        IExportDrawingDialog dialog,
        IDesktopMessageService messages,
        IDrawingSceneBitmapRenderer? renderer = null)
    {
        _activeSession = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
        _activeDocumentName = activeDocumentName ?? throw new ArgumentNullException(nameof(activeDocumentName));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _renderer = renderer ?? new DrawingSceneBitmapRenderer();
    }

    public bool ExportPng()
    {
        return ExportPngDetailed().IsSuccess;
    }

    public ExportPngOperationResult ExportPngDetailed()
    {
        ProjectRuntimeSession? session = _activeSession();
        if (session is null)
        {
            return new ExportPngOperationResult(ExportPngStatus.Failed);
        }

        string baseName = Path.GetFileNameWithoutExtension(_activeDocumentName());
        string? path = _dialog.ChoosePngPath($"{baseName}.png");
        if (path is null)
        {
            return new ExportPngOperationResult(ExportPngStatus.Cancelled);
        }

        try
        {
            DrawingSceneBitmapResult result = ExportToTemporaryFile(session, path);
            return new ExportPngOperationResult(ExportPngStatus.Success, result.SelectedDpi);
        }
        catch (PngExportSizeException exception)
        {
            _messages.ShowError("导出 PNG 失败", exception.Message);
            return new ExportPngOperationResult(ExportPngStatus.Failed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _messages.ShowError("导出 PNG 失败", $"无法写入 PNG 文件：{exception.Message}");
            return new ExportPngOperationResult(ExportPngStatus.Failed);
        }
        catch (Exception exception) when (exception is PngExportRenderException or OutOfMemoryException)
        {
            _messages.ShowError("导出 PNG 失败", $"PNG 渲染或编码失败：{exception.Message}");
            return new ExportPngOperationResult(ExportPngStatus.Failed);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            _messages.ShowError("导出 PNG 失败", exception.Message);
            return new ExportPngOperationResult(ExportPngStatus.Failed);
        }
    }

    private DrawingSceneBitmapResult ExportToTemporaryFile(ProjectRuntimeSession session, string path)
    {
        if (session.Scene.Diagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                $"图纸存在无法导出的专业显示错误：{session.Scene.Diagnostics[0].Message}");
        }

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            DrawingSceneBitmapResult renderResult;
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                renderResult = _renderer.RenderPng(session.Scene, stream);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
            return renderResult;
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not hide the primary export outcome.
        }
    }
}
