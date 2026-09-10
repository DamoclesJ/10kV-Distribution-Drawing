using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemoveCustomerStationWithLayoutCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _layout;
    private readonly RemoveCustomerStationCommand _domainCommand;

    public RemoveCustomerStationWithLayoutCommand(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid customerStationId)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        CustomerStation = document.CustomerStations.SingleOrDefault(station =>
                station.Id == customerStationId)
            ?? throw new InvalidOperationException(
                $"Customer station '{customerStationId}' does not exist.");
        Layout = layout.CustomerStationLayouts.GetValueOrDefault(customerStationId)
            ?? throw new InvalidOperationException(
                $"Customer station layout '{customerStationId}' does not exist.");
        _domainCommand = new RemoveCustomerStationCommand(document, customerStationId);
    }

    public CustomerStation CustomerStation { get; }

    public CustomerStationLayout Layout { get; }

    public void Execute()
    {
        _domainCommand.Execute();
        try
        {
            _layout.RemoveCustomerStation(CustomerStation.Id);
        }
        catch
        {
            _document.AddCustomerStation(CustomerStation);
            throw;
        }
    }

    public void Undo()
    {
        _domainCommand.Undo();
        try
        {
            _layout.AddCustomerStation(Layout, CustomerStation);
        }
        catch
        {
            _document.RemoveCustomerStation(CustomerStation.Id);
            throw;
        }
    }

    public void Redo() => Execute();
}
