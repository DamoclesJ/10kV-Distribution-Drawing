using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class ChangeIntervalTypeCommand : ICommand
{
    private readonly RingCabinet _cabinet;
    private readonly DrawingDocument? _document;
    private readonly EnergizationScenario? _scenario;
    private EnergizationScenarioCommand? _scenarioRepair;
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
        DrawingDocument? document = null,
        EnergizationScenario? scenario = null)
    {
        _cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        _document = document;
        _scenario = scenario;
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

    public IReadOnlyList<EnergizedSeed> RemovedSeeds { get; private set; } = [];

    public void Execute()
    {
        if (_after is not null && _afterLayout is not null)
        {
            RestoreState(_after, _afterLayout);
            _scenarioRepair?.Redo();
            return;
        }

        _before = _cabinet.CaptureRestoreDefinition();
        _beforeLayout = _runtimeLayout.RingCabinetLayouts.GetValueOrDefault(_cabinet.Id)
            ?? throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{_cabinet.Id}'.");
        try
        {
            Guid[] affectedIntervalIds = ResolveAffectedIntervalIds();
            _cabinet.ChangeIntervalType(
                _intervalId,
                _targetIntervalKind,
                _targetGroundingStructureKind);
            _document?.SynchronizeRingCabinetAggregate(_cabinet);
            _afterLayout = affectedIntervalIds.Aggregate(
                _beforeLayout,
                (current, affectedIntervalId) => _layoutFactory.RebuildInterval(
                    _cabinet,
                    current,
                    affectedIntervalId));
            _runtimeLayout.ReplaceRingCabinet(_afterLayout);
            _after = _cabinet.CaptureRestoreDefinition();
            if (_scenario is not null)
            {
                HashSet<Guid> deletedIds = _before.Intervals.SelectMany(interval => interval.Switches)
                    .Select(device => device.Id).Except(_cabinet.Intervals.SelectMany(interval => interval.SwitchDevices).Select(device => device.Id))
                    .ToHashSet();
                RemovedSeeds = _scenario.Seeds.Where(seed => deletedIds.Contains(seed.BoundaryDeviceId)).ToArray();
                _scenarioRepair = EnergizationScenarioCommand.RemoveDeletedBoundaries(_scenario, deletedIds);
                _scenarioRepair.Execute();
            }
        }
        catch
        {
            _scenarioRepair?.Undo();
            _scenarioRepair = null;
            RemovedSeeds = [];
            _after = null;
            _afterLayout = null;
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
        _scenarioRepair?.Undo();
    }

    public void Redo() => Execute();

    private Guid[] ResolveAffectedIntervalIds()
    {
        Guid? existingPTId = _targetIntervalKind == IntervalKind.PTInterval
            ? _cabinet.Intervals
                .Where(interval => interval.IntervalKind == IntervalKind.PTInterval &&
                                   interval.IntervalId != _intervalId)
                .Select(interval => (Guid?)interval.IntervalId)
                .SingleOrDefault()
            : null;
        return existingPTId is Guid ptId
            ? [_intervalId, ptId]
            : [_intervalId];
    }

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
