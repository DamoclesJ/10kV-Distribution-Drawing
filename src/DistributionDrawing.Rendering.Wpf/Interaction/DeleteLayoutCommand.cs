using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class DeleteLayoutCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly SelectionService _selectionService;
    private readonly Device _device;
    private readonly object _beforeLayout;

    public DeleteLayoutCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        SelectionTarget target,
        SelectionService selectionService)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        ArgumentNullException.ThrowIfNull(target);
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

        Target = target;
        (_device, _beforeLayout) = ResolveTarget(target);
    }

    public SelectionTarget Target { get; }

    public object BeforeSnapshot => _device;

    public object BeforeLayout => _beforeLayout;

    public bool IsDeleted { get; private set; }

    public void Execute()
    {
        if (IsDeleted)
        {
            return;
        }

        EnsureLayoutExists();
        try
        {
            _document.RemoveDevice(_device.Id);
            RemoveLayout();
            IsDeleted = true;
            _selectionService.Clear();
        }
        catch
        {
            if (!_document.Devices.Any(device => device.Id == _device.Id))
            {
                _document.AddDevice(_device);
            }

            throw;
        }
    }

    public void Undo()
    {
        if (!IsDeleted)
        {
            return;
        }

        _document.AddDevice(_device);
        try
        {
            AddLayout();
            IsDeleted = false;
        }
        catch
        {
            _document.RemoveDevice(_device.Id);
            throw;
        }
    }

    public void Redo() => Execute();

    private (Device Device, object Layout) ResolveTarget(SelectionTarget target)
    {
        return target.TargetKind switch
        {
            ApplicationSelectionTargetKind.RingCabinet => ResolveRingCabinet(target.TargetId),
            ApplicationSelectionTargetKind.Pole => ResolvePole(target.TargetId),
            _ => throw new ArgumentException(
                "Only ring cabinets and poles can be deleted.",
                nameof(target))
        };
    }

    private (Device, object) ResolveRingCabinet(Guid id)
    {
        RingCabinet cabinet = _document.Devices
            .OfType<RingCabinet>()
            .SingleOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException($"Ring cabinet '{id}' does not exist.");
        RingCabinetLayout layout = _runtimeLayout.RingCabinetLayouts.TryGetValue(
                id,
                out RingCabinetLayout? found)
            ? found
            : throw new InvalidOperationException($"No layout exists for ring cabinet '{id}'.");
        return (cabinet, layout);
    }

    private (Device, object) ResolvePole(Guid id)
    {
        Pole pole = _document.Devices
            .OfType<Pole>()
            .SingleOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException($"Pole '{id}' does not exist.");
        PoleLayout layout = _runtimeLayout.DrawingLayout.Poles.TryGetValue(
                id,
                out PoleLayout? found)
            ? found
            : throw new InvalidOperationException($"No layout exists for pole '{id}'.");
        return (pole, layout);
    }

    private void EnsureLayoutExists()
    {
        if (Target.TargetKind == ApplicationSelectionTargetKind.RingCabinet)
        {
            _ = _runtimeLayout.RingCabinetLayouts[Target.TargetId];
            return;
        }

        if (Target.TargetKind == ApplicationSelectionTargetKind.Pole)
        {
            _ = _runtimeLayout.DrawingLayout.Poles[Target.TargetId];
            return;
        }

        throw new InvalidOperationException("Unsupported delete target.");
    }

    private void RemoveLayout()
    {
        if (Target.TargetKind == ApplicationSelectionTargetKind.RingCabinet)
        {
            _runtimeLayout.RemoveRingCabinet(Target.TargetId);
        }
        else
        {
            _runtimeLayout.DrawingLayout.RemovePole(Target.TargetId);
        }
    }

    private void AddLayout()
    {
        if (BeforeLayout is RingCabinetLayout cabinetLayout)
        {
            _runtimeLayout.AddRingCabinet(cabinetLayout);
        }
        else
        {
            _runtimeLayout.DrawingLayout.Add((PoleLayout)BeforeLayout);
        }
    }
}
