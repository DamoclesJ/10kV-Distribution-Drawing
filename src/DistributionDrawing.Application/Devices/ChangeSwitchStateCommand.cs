using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Devices;

public sealed class ChangeSwitchStateCommand
{
    private readonly DrawingDocument _document;
    private readonly Guid _switchDeviceId;
    private readonly SwitchState _targetState;
    private SwitchState? _originalState;

    public ChangeSwitchStateCommand(
        DrawingDocument document,
        Guid switchDeviceId,
        SwitchState targetState)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (switchDeviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Switch device ID cannot be empty.",
                nameof(switchDeviceId));
        }

        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState));
        }

        _switchDeviceId = switchDeviceId;
        _targetState = targetState;
    }

    public SwitchStateChangeResult? InitialChange { get; private set; }

    public void Execute()
    {
        SwitchStateChangeResult change = _document.ChangeSwitchState(
            _switchDeviceId,
            _targetState);
        _originalState ??= change.PreviousState;
        InitialChange ??= change;
    }

    public void Undo()
    {
        if (_originalState is not SwitchState originalState)
        {
            throw new InvalidOperationException(
                "The switch state command has not been executed.");
        }

        _document.ChangeSwitchState(_switchDeviceId, originalState);
    }

    public void Redo()
    {
        if (_originalState is null)
        {
            throw new InvalidOperationException(
                "The switch state command has not been executed.");
        }

        _document.ChangeSwitchState(_switchDeviceId, _targetState);
    }
}
