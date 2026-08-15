using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class ChangeIntervalTypeCommand : ICommand
{
    private readonly RingCabinet _cabinet;
    private readonly Guid _intervalId;
    private readonly IntervalKind _targetIntervalKind;
    private readonly GroundingStructureKind? _targetGroundingStructureKind;
    private RingCabinetRestoreDefinition? _before;
    private RingCabinetRestoreDefinition? _after;

    public ChangeIntervalTypeCommand(
        RingCabinet cabinet,
        Guid intervalId,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind)
    {
        _cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        if (intervalId == Guid.Empty)
        {
            throw new ArgumentException("Interval ID cannot be empty.", nameof(intervalId));
        }

        _intervalId = intervalId;
        _targetIntervalKind = targetIntervalKind;
        _targetGroundingStructureKind = targetGroundingStructureKind;
    }

    public void Execute()
    {
        if (_after is not null)
        {
            _cabinet.RestoreState(_after);
            return;
        }

        _before = _cabinet.CaptureRestoreDefinition();
        _cabinet.ChangeIntervalType(
            _intervalId,
            _targetIntervalKind,
            _targetGroundingStructureKind);
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
