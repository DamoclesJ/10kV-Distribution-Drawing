using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class SetRingCabinetIntervalCableTerminalPresenceCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RingCabinet _cabinet;
    private readonly Guid _intervalId;
    private readonly bool _isPresent;
    private RingCabinetRestoreDefinition? _before;
    private RingCabinetRestoreDefinition? _after;

    public SetRingCabinetIntervalCableTerminalPresenceCommand(
        DrawingDocument document,
        RingCabinet cabinet,
        Guid intervalId,
        bool isPresent)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        if (intervalId == Guid.Empty)
        {
            throw new ArgumentException("Interval ID cannot be empty.", nameof(intervalId));
        }

        _intervalId = intervalId;
        _isPresent = isPresent;
    }

    public void Execute()
    {
        if (_after is not null)
        {
            Restore(_after);
            return;
        }

        _before = _cabinet.CaptureRestoreDefinition();
        RingCabinetInterval interval = _cabinet.Intervals.SingleOrDefault(candidate =>
                candidate.IntervalId == _intervalId)
            ?? throw new InvalidOperationException(
                $"Interval '{_intervalId}' does not belong to cabinet '{_cabinet.Id}'.");
        Guid? cableTerminalId = _isPresent
            ? interval.CableTerminalId ?? Guid.NewGuid()
            : null;

        try
        {
            _cabinet.SetIntervalCableTerminal(_intervalId, cableTerminalId);
            _document.SynchronizeRingCabinetAggregate(_cabinet);
            _after = _cabinet.CaptureRestoreDefinition();
        }
        catch
        {
            Restore(_before);
            throw;
        }
    }

    public void Undo()
    {
        if (_before is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        Restore(_before);
    }

    public void Redo() => Execute();

    private void Restore(RingCabinetRestoreDefinition definition)
    {
        RingCabinetRestoreDefinition current = _cabinet.CaptureRestoreDefinition();
        try
        {
            _cabinet.RestoreState(definition);
            _document.SynchronizeRingCabinetAggregate(_cabinet);
        }
        catch
        {
            _cabinet.RestoreState(current);
            _document.SynchronizeRingCabinetAggregate(_cabinet);
            throw;
        }
    }
}
