using System.Windows;
using DistributionDrawing.Desktop.Workspace;

namespace DistributionDrawing.Desktop.Actions;

public interface IDesktopMessageService
{
    void ShowError(string title, string message);

    void ShowWarning(string title, string message);

    bool Confirm(string title, string message);

    DirtyDecision ConfirmSaveChanges(string documentName);
}

public sealed class DesktopMessageService : IDesktopMessageService
{
    private readonly Window _owner;

    public DesktopMessageService(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void ShowError(string title, string message) =>
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowWarning(string title, string message) =>
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            _owner,
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question) == MessageBoxResult.OK;

    public DirtyDecision ConfirmSaveChanges(string documentName)
    {
        MessageBoxResult result = MessageBox.Show(
            _owner,
            $"是否保存对“{documentName}”的更改？",
            "保存更改",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => DirtyDecision.Save,
            MessageBoxResult.No => DirtyDecision.Discard,
            _ => DirtyDecision.Cancel
        };
    }
}
