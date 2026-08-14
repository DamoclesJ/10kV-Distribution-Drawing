using System.ComponentModel;
using System.Windows.Input;

namespace DistributionDrawing.Desktop.ViewModels;

public enum DesktopToolMode
{
    Select,
    CreateRingCabinet,
    CreatePole
}

public sealed class ToolboxViewModel : INotifyPropertyChanged
{
    private DesktopToolMode _selectedMode;

    public ToolboxViewModel(
        Action selectMode,
        Action createRingCabinetMode,
        Action createPoleMode)
    {
        ArgumentNullException.ThrowIfNull(selectMode);
        ArgumentNullException.ThrowIfNull(createRingCabinetMode);
        ArgumentNullException.ThrowIfNull(createPoleMode);

        SelectModeCommand = new RelayCommand(
            () => SelectMode(selectMode, DesktopToolMode.Select));
        CreateRingCabinetModeCommand = new RelayCommand(
            () => SelectMode(createRingCabinetMode, DesktopToolMode.CreateRingCabinet));
        CreatePoleModeCommand = new RelayCommand(
            () => SelectMode(createPoleMode, DesktopToolMode.CreatePole));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DesktopToolMode SelectedMode
    {
        get => _selectedMode;
        private set
        {
            if (_selectedMode == value)
            {
                return;
            }

            _selectedMode = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(SelectedMode)));
        }
    }

    public ICommand SelectModeCommand { get; }

    public ICommand CreateRingCabinetModeCommand { get; }

    public ICommand CreatePoleModeCommand { get; }

    private void SelectMode(Action selectMode, DesktopToolMode mode)
    {
        selectMode();
        SelectedMode = mode;
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
