using DistributionDrawing.Domain.Devices.CustomerStations;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RenameIncomingFeederCommandAdapter : ICommand
{
    private readonly DistributionDrawing.Application.Devices.CustomerStations.RenameIncomingFeederCommand
        _command;

    public RenameIncomingFeederCommandAdapter(
        CustomerStation customerStation,
        Guid incomingFeederId,
        string displayName)
    {
        _command = new(
            customerStation ?? throw new ArgumentNullException(nameof(customerStation)),
            incomingFeederId,
            displayName);
    }

    public void Execute() => _command.Execute();

    public void Undo() => _command.Undo();

    public void Redo() => _command.Redo();
}
