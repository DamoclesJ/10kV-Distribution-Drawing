using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemoveRingCabinetCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public RemoveRingCabinetCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public RingCabinet Cabinet { get; }

    public RingCabinetLayout Layout { get; }

    public void Execute()
    {
        _ = _runtimeLayout.RingCabinetLayouts[Cabinet.Id];
        _document.RemoveDevice(Cabinet.Id);
        _runtimeLayout.RemoveRingCabinet(Cabinet.Id);
    }

    public void Undo()
    {
        _document.AddDevice(Cabinet);
        try
        {
            _runtimeLayout.AddRingCabinet(Layout);
        }
        catch
        {
            _document.RemoveDevice(Cabinet.Id);
            throw;
        }
    }

    public void Redo() => Execute();
}
