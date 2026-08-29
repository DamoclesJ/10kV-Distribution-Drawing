using System.Windows;

namespace DistributionDrawing.Desktop.Actions;

public interface IDesktopMessageService
{
    void ShowError(string title, string message);

    void ShowWarning(string title, string message);

    bool Confirm(string title, string message);
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
}
