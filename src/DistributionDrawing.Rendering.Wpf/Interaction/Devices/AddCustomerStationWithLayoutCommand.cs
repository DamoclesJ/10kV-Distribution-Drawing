using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddCustomerStationWithLayoutCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _layout;
    private readonly AddCustomerStationCommand _domainCommand;

    public AddCustomerStationWithLayoutCommand(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        CustomerStationCreation creation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
        _domainCommand = new AddCustomerStationCommand(document, creation.CustomerStation);
    }

    public CustomerStationCreation Creation { get; }

    public void Execute()
    {
        Creation.Layout.ValidateFor(Creation.CustomerStation);
        _domainCommand.Execute();
        try
        {
            _layout.AddCustomerStation(Creation.Layout, Creation.CustomerStation);
        }
        catch
        {
            _domainCommand.Undo();
            throw;
        }
    }

    public void Undo()
    {
        _domainCommand.Undo();
        try
        {
            _layout.RemoveCustomerStation(Creation.CustomerStation.Id);
        }
        catch
        {
            _document.AddCustomerStation(Creation.CustomerStation);
            throw;
        }
    }

    public void Redo() => Execute();
}
