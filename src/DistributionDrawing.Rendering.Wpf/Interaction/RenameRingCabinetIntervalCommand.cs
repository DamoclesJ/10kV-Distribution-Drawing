using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class RenameRingCabinetIntervalCommand : ICommand
{
    private readonly RingCabinet _cabinet;
    private readonly Guid _intervalId;
    private readonly string _displayName;
    private RingCabinetRestoreDefinition? _before;
    private RingCabinetRestoreDefinition? _after;

    public RenameRingCabinetIntervalCommand(
        RingCabinet cabinet,
        Guid intervalId,
        string displayName)
    {
        _cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        _intervalId = intervalId;
        _displayName = displayName;
    }

    public void Execute()
    {
        if (_after is not null)
        {
            _cabinet.RestoreState(_after);
            return;
        }

        _before = _cabinet.CaptureRestoreDefinition();
        _cabinet.RenameInterval(_intervalId, _displayName);
        _after = _cabinet.CaptureRestoreDefinition();
    }

    public void Undo()
    {
        if (_before is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        _cabinet.RestoreState(_before);
    }

    public void Redo() => Execute();
}
