using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class CustomerStationCreationFactory
{
    private readonly DistributionDrawing.Application.Devices.CustomerStations.CustomerStationCreationFactory
        _domainFactory = new();

    public CustomerStationCreation Create(
        StationKind stationKind,
        IReadOnlyList<string> feederDisplayNames,
        DocumentPoint position)
    {
        CustomerStation station = _domainFactory.Create(stationKind, feederDisplayNames);
        var layout = new CustomerStationLayout(
            station.Id,
            position,
            station.IncomingFeeders.Select(feeder =>
                new CustomerStationIncomingFeederLayout(
                    feeder.IncomingFeederId,
                    true)));
        return new CustomerStationCreation(station, layout);
    }
}
