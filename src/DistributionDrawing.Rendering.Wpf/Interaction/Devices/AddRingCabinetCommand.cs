using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Application.Interaction;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddRingCabinetCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly SelectionService? _selectionService;

    public AddRingCabinetCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        RingCabinet cabinet,
        RingCabinetLayout layout,
        SelectionService? selectionService = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        _selectionService = selectionService;
        Cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (Cabinet.Id != Layout.CabinetId)
        {
            throw new ArgumentException("Ring cabinet and layout IDs must match.", nameof(layout));
        }
    }

    public RingCabinet Cabinet { get; }

    public RingCabinetLayout Layout { get; }

    public void Execute()
    {
        if (_runtimeLayout.RingCabinetLayouts.ContainsKey(Cabinet.Id))
        {
            throw new InvalidOperationException($"Ring cabinet layout '{Cabinet.Id}' already exists.");
        }

        _document.AddDevice(Cabinet);
        try
        {
            _runtimeLayout.AddRingCabinet(Layout);
            _selectionService?.Select(new SelectionTarget(
                ApplicationSelectionTargetKind.RingCabinet,
                Cabinet.Id));
        }
        catch
        {
            _document.RemoveDevice(Cabinet.Id);
            throw;
        }
    }

    public void Undo()
    {
        _ = _runtimeLayout.RingCabinetLayouts[Layout.CabinetId];
        _document.RemoveDevice(Cabinet.Id);
        _runtimeLayout.RemoveRingCabinet(Layout.CabinetId);
        if (_selectionService?.CurrentSelection is { } selection &&
            selection.TargetKind == ApplicationSelectionTargetKind.RingCabinet &&
            selection.TargetId == Cabinet.Id)
        {
            _selectionService.Clear();
        }
    }

    public void Redo() => Execute();
}
