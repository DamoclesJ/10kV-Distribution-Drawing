using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemovePoleCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly IReadOnlyList<ElectricalNode> _nodes;
    private readonly IReadOnlyList<Terminal> _terminals;

    public RemovePoleCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Pole pole,
        PoleLayout layout)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Pole = pole ?? throw new ArgumentNullException(nameof(pole));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _nodes = document.ElectricalNodes.Where(node => node.OwnerId == pole.Id).ToArray();
        _terminals = document.Terminals.Where(terminal => terminal.OwnerId == pole.Id).ToArray();
    }

    public Pole Pole { get; }

    public PoleLayout Layout { get; }

    public void Execute()
    {
        _ = _runtimeLayout.DrawingLayout.Poles[Pole.Id];
        _document.RemoveDevice(Pole.Id);
        _runtimeLayout.DrawingLayout.RemovePole(Pole.Id);
    }

    public void Undo()
    {
        _document.AddDevice(Pole);
        try
        {
            foreach (ElectricalNode node in _nodes) _document.AddElectricalNode(node);
            foreach (Terminal terminal in _terminals) _document.AddTerminal(terminal);
            _runtimeLayout.DrawingLayout.Add(Layout);
        }
        catch
        {
            if (_document.Devices.Any(device => device.Id == Pole.Id))
            {
                _document.RemoveDevice(Pole.Id);
            }
            throw;
        }
    }

    public void Redo() => Execute();
}
