using System.ComponentModel;

namespace DistributionDrawing.Desktop.ViewModels;

public enum DesktopToolMode
{
    Select,
    CreateRingCabinet,
    CreatePole,
    CreateOverheadLine,
    CreateCable
}

public sealed class ToolboxViewModel : INotifyPropertyChanged
{
    private DesktopToolMode _selectedMode;

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

    public void SetSelectedMode(DesktopToolMode mode) => SelectedMode = mode;
}
