using System.ComponentModel;

namespace DistributionDrawing.Desktop.ViewModels;

public enum DesktopToolMode
{
    Select,
    CreateRingCabinet,
    CreatePole,
    CreateOverheadLine,
    CreateCable,
    AddCableTermination,
    AddPoleSwitch,
    AddGroundingPoint,
    AddWorkScope
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public void SetSelectedMode(DesktopToolMode mode) => SelectedMode = mode;

    public bool IsSelectActive => SelectedMode == DesktopToolMode.Select;
    public bool IsPoleActive => SelectedMode == DesktopToolMode.CreatePole;
    public bool IsRingCabinetActive => SelectedMode == DesktopToolMode.CreateRingCabinet;
    public bool IsOverheadLineActive => SelectedMode == DesktopToolMode.CreateOverheadLine;
    public bool IsCableActive => SelectedMode == DesktopToolMode.CreateCable;
    public bool IsCableTerminationActive => SelectedMode == DesktopToolMode.AddCableTermination;
    public bool IsPoleSwitchActive => SelectedMode == DesktopToolMode.AddPoleSwitch;
    public bool IsGroundingPointActive => SelectedMode == DesktopToolMode.AddGroundingPoint;
    public bool IsWorkScopeActive => SelectedMode == DesktopToolMode.AddWorkScope;
}
