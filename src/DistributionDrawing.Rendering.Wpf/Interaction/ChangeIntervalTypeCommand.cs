using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class ChangeIntervalTypeCommand : ICommand
{
    private readonly RingCabinet _cabinet;
    private readonly DrawingDocument? _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly RingCabinetLayoutFactory _layoutFactory;
    private readonly Guid _intervalId;
    private readonly IntervalKind _targetIntervalKind;
    private readonly GroundingStructureKind? _targetGroundingStructureKind;
    private RingCabinetRestoreDefinition? _before;
    private RingCabinetRestoreDefinition? _after;
    private RingCabinetLayout? _beforeLayout;
    private RingCabinetLayout? _afterLayout;

    public ChangeIntervalTypeCommand(
        RingCabinet cabinet,
        RuntimeLayoutDocument runtimeLayout,
        Guid intervalId,
        IntervalKind targetIntervalKind,
        GroundingStructureKind? targetGroundingStructureKind,
        RingCabinetLayoutFactory? layoutFactory = null,
        DrawingDocument? document = null)
    {
        _cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        _document = document;
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        _layoutFactory = layoutFactory ?? new RingCabinetLayoutFactory();
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
        if (_after is not null && _afterLayout is not null)
        {
            RestoreState(_after, _afterLayout);
            return;
        }

        _before = _cabinet.CaptureRestoreDefinition();
        _beforeLayout = _runtimeLayout.RingCabinetLayouts.GetValueOrDefault(_cabinet.Id)
            ?? throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{_cabinet.Id}'.");
        try
        {
            _cabinet.ChangeIntervalType(
                _intervalId,
                _targetIntervalKind,
                _targetGroundingStructureKind);
            _document?.SynchronizeRingCabinetAggregate(_cabinet);
            _afterLayout = _layoutFactory.RebuildInterval(
                _cabinet,
                _beforeLayout,
                _intervalId);
            _runtimeLayout.ReplaceRingCabinet(_afterLayout);
            _after = _cabinet.CaptureRestoreDefinition();
        }
        catch
        {
            _cabinet.RestoreState(_before);
            _document?.SynchronizeRingCabinetAggregate(_cabinet);
            _runtimeLayout.ReplaceRingCabinet(_beforeLayout);
            throw;
        }
    }

    public void Undo()
    {
        if (_before is null || _beforeLayout is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        RestoreState(_before, _beforeLayout);
    }

    public void Redo() => Execute();

    private void RestoreState(
        RingCabinetRestoreDefinition definition,
        RingCabinetLayout layout)
    {
        RingCabinetRestoreDefinition currentDefinition =
            _cabinet.CaptureRestoreDefinition();
        RingCabinetLayout currentLayout = _runtimeLayout.RingCabinetLayouts
            .GetValueOrDefault(_cabinet.Id)
            ?? throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{_cabinet.Id}'.");
        try
        {
            _cabinet.RestoreState(definition);
            _document?.SynchronizeRingCabinetAggregate(_cabinet);
            _runtimeLayout.ReplaceRingCabinet(layout);
        }
        catch
        {
            _cabinet.RestoreState(currentDefinition);
            _document?.SynchronizeRingCabinetAggregate(_cabinet);
            _runtimeLayout.ReplaceRingCabinet(currentLayout);
            throw;
        }
    }
}
