using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Devices.CustomerStations;

public sealed class AddCustomerStationCommand
{
    private readonly DrawingDocument _document;

    public AddCustomerStationCommand(
        DrawingDocument document,
        CustomerStation customerStation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        CustomerStation = customerStation ??
            throw new ArgumentNullException(nameof(customerStation));
    }

    public CustomerStation CustomerStation { get; }

    public void Execute() => _document.AddCustomerStation(CustomerStation);

    public void Undo() => _document.RemoveCustomerStation(CustomerStation.Id);

    public void Redo() => Execute();
}
