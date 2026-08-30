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
    private readonly DrawingSceneBitmapRenderer _renderer;

    public ExportDrawingController(
        Func<ProjectRuntimeSession?> activeSession,
        Func<string> activeDocumentName,
        IExportDrawingDialog dialog,
        IDesktopMessageService messages,
        DrawingSceneBitmapRenderer? renderer = null)
    {
        _activeSession = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
        _activeDocumentName = activeDocumentName ?? throw new ArgumentNullException(nameof(activeDocumentName));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _renderer = renderer ?? new DrawingSceneBitmapRenderer();
    }

    public bool ExportPng()
    {
        ProjectRuntimeSession? session = _activeSession();
        if (session is null)
        {
            return false;
        }

        string baseName = Path.GetFileNameWithoutExtension(_activeDocumentName());
        string? path = _dialog.ChoosePngPath($"{baseName}.png");
        if (path is null)
        {
            return false;
        }

        try
        {
            using FileStream stream = File.Create(path);
            _renderer.RenderPng(session.Scene, stream);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _messages.ShowError("导出 PNG 失败", exception.Message);
            return false;
        }
        catch (OutOfMemoryException)
        {
            _messages.ShowError("导出 PNG 失败", "图纸范围过大，无法按当前分辨率导出。");
            return false;
        }
    }
}
