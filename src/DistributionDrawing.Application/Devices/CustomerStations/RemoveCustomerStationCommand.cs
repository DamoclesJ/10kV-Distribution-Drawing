using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.Devices.CustomerStations;

public sealed class RemoveCustomerStationCommand
{
    private readonly DrawingDocument _document;

    public RemoveCustomerStationCommand(
        DrawingDocument document,
        Guid customerStationId)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        CustomerStation = document.CustomerStations.SingleOrDefault(station =>
                station.Id == customerStationId)
            ?? throw new InvalidOperationException(
                $"Customer station '{customerStationId}' does not exist.");
    }

    public CustomerStation CustomerStation { get; }

    public void Execute() => _document.RemoveCustomerStation(CustomerStation.Id);

    public void Undo() => _document.AddCustomerStation(CustomerStation);

    public void Redo() => Execute();
}
