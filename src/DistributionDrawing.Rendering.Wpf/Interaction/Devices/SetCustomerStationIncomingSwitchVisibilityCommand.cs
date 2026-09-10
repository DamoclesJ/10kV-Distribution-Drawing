using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class SetCustomerStationIncomingSwitchVisibilityCommand : ICommand
{
    private readonly RuntimeLayoutDocument _layout;
    private readonly CustomerStation _station;
    private readonly CustomerStationLayout _before;
    private readonly CustomerStationLayout _after;

    public SetCustomerStationIncomingSwitchVisibilityCommand(
        RuntimeLayoutDocument layout,
        CustomerStation station,
        Guid incomingFeederId,
        bool showIncomingSwitch)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _station = station ?? throw new ArgumentNullException(nameof(station));
        if (station.StationKind != StationKind.IndoorStation)
        {
            throw new InvalidOperationException(
                "BoxStation incoming switch visibility is fixed.");
        }
        _before = layout.CustomerStationLayouts.GetValueOrDefault(station.Id)
            ?? throw new InvalidOperationException(
                $"No layout exists for customer station '{station.Id}'.");
        if (!_before.IncomingFeeders.ContainsKey(incomingFeederId))
        {
            throw new InvalidOperationException(
                $"Incoming feeder '{incomingFeederId}' has no layout.");
        }
        _after = new CustomerStationLayout(
            station.Id,
            _before.Position,
            _before.IncomingFeeders.Values.Select(item =>
                item.IncomingFeederId == incomingFeederId
                    ? new CustomerStationIncomingFeederLayout(
                        item.IncomingFeederId,
                        showIncomingSwitch)
                    : item));
        _after.ValidateFor(station);
    }

    public void Execute() => _layout.ReplaceCustomerStation(_after, _station);

    public void Undo() => _layout.ReplaceCustomerStation(_before, _station);

    public void Redo() => Execute();
}
