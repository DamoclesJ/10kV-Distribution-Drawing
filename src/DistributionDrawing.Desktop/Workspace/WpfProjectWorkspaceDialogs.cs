using System.IO;
using System.Windows;
using Microsoft.Win32;
using DistributionDrawing.Desktop.Actions;

namespace DistributionDrawing.Desktop.Workspace;

public sealed class WpfProjectWorkspaceDialogs : IProjectWorkspaceDialogs
{
    private readonly Window _owner;
    private readonly IDesktopMessageService? _messages;

    public WpfProjectWorkspaceDialogs(
        Window owner,
        IDesktopMessageService? messages = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _messages = messages;
    }

    public NewProjectRequest? RequestNewProject()
    {
        return new NewProjectRequest(string.Empty, string.Empty, null);
    }

    public string? ChooseOpenProject()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "10kV 配电工程 (*.kvdrawing)|*.kvdrawing",
            DefaultExt = ".kvdrawing",
            CheckFileExists = true,
            Title = "打开工程"
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? ChooseSaveAs(string? currentFilePath)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "10kV 配电工程 (*.kvdrawing)|*.kvdrawing",
            DefaultExt = ".kvdrawing",
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(currentFilePath)
                ? string.Empty
                : Path.GetFileName(currentFilePath),
            Title = "工程另存为"
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public DirtyDecision ConfirmDirty(string operation)
    {
        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"当前工程有未保存的修改，继续{operation}前是否保存？",
            "保存工程",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => DirtyDecision.Save,
            MessageBoxResult.No => DirtyDecision.Discard,
            _ => DirtyDecision.Cancel
        };
    }

    public DirtyDecision ConfirmDirtyDocument(string documentName, string operation)
    {
        return _messages?.ConfirmSaveChanges(documentName) ?? ConfirmDirty(operation);
    }

    public void ShowError(string title, string message)
    {
        if (_messages is not null)
        {
            _messages.ShowError(title, message);
            return;
        }

        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
