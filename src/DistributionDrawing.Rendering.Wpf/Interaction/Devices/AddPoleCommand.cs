using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddPoleCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public AddPoleCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Pole pole,
        Terminal terminal,
        PoleLayout layout)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
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
    }

    public void Redo() => Execute();
}
