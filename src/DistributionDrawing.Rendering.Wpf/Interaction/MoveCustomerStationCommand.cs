using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class MoveCustomerStationCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;
    private readonly CustomerStation _station;

    public MoveCustomerStationCommand(
        RuntimeLayoutDocument layout,
        CustomerStation station,
        DocumentPoint before,
        DocumentPoint after)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _station = station ?? throw new ArgumentNullException(nameof(station));
        CustomerStationId = station.Id;
        Before = before;
        After = after;
    }

    public Guid CustomerStationId { get; }

    public DocumentPoint Before { get; }

    public DocumentPoint After { get; }

    public void Execute() => Apply(After);

    public void Undo() => Apply(Before);

    public void Redo() => Execute();

    private void Apply(DocumentPoint position)
    {
        if (!_layout.CustomerStationLayouts.TryGetValue(
                CustomerStationId,
                out CustomerStationLayout? current))
        {
            throw new InvalidOperationException(
                $"No layout exists for customer station '{CustomerStationId}'.");
        }

        _layout.ReplaceCustomerStation(
            current.MoveTo(position, _station),
            _station);
    }
}
