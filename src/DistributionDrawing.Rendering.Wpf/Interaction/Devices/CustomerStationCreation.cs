using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class CustomerStationCreation
{
    public CustomerStationCreation(
        CustomerStation customerStation,
        CustomerStationLayout layout)
    {
        CustomerStation = customerStation ?? throw new ArgumentNullException(nameof(customerStation));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Layout.ValidateFor(CustomerStation);
    }

    public CustomerStation CustomerStation { get; }

    public CustomerStationLayout Layout { get; }
}
