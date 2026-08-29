using System.Windows.Input;

namespace DistributionDrawing.Desktop.Actions;

public sealed class DesktopAction : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public DesktopAction(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
