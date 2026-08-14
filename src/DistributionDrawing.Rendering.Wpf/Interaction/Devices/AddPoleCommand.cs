using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Application.Interaction;
using ApplicationSelectionTargetKind = DistributionDrawing.Application.Interaction.SelectionTargetKind;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddPoleCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly SelectionService? _selectionService;

    public AddPoleCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Pole pole,
        Terminal terminal,
        PoleLayout layout,
        SelectionService? selectionService = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        _selectionService = selectionService;
        Pole = pole ?? throw new ArgumentNullException(nameof(pole));
        Terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        if (Pole.Id != Layout.PoleId)
        {
            throw new ArgumentException("Pole and layout IDs must match.", nameof(layout));
        }
    }

    public Pole Pole { get; }

    public PoleLayout Layout { get; }

    public Terminal Terminal { get; }

    public void Execute()
    {
        if (_runtimeLayout.DrawingLayout.Poles.ContainsKey(Pole.Id))
        {
            throw new InvalidOperationException($"Pole layout '{Pole.Id}' already exists.");
        }

        _document.AddDevice(Pole);
        try
        {
            _document.AddTerminal(Terminal);
            _runtimeLayout.DrawingLayout.Add(Layout);
            _selectionService?.Select(new SelectionTarget(
                ApplicationSelectionTargetKind.Pole,
                Pole.Id));
        }
        catch
        {
            _document.RemoveDevice(Pole.Id);
            throw;
        }
    }

    public void Undo()
    {
        _ = _runtimeLayout.DrawingLayout.Poles[Layout.PoleId];
        _document.RemoveDevice(Pole.Id);
        _runtimeLayout.DrawingLayout.RemovePole(Layout.PoleId);
        if (_selectionService?.CurrentSelection is { } selection &&
            selection.TargetKind == ApplicationSelectionTargetKind.Pole &&
            selection.TargetId == Pole.Id)
        {
            _selectionService.Clear();
        }
    }

    public void Redo() => Execute();
}
