using DistributionDrawing.Domain.Devices.CustomerStations;

namespace DistributionDrawing.Application.Devices.CustomerStations;

public sealed class RenameIncomingFeederCommand
{
    private readonly CustomerStation _customerStation;
    private readonly Guid _incomingFeederId;
    private readonly string _displayName;
    private string? _originalDisplayName;

    public RenameIncomingFeederCommand(
        CustomerStation customerStation,
        Guid incomingFeederId,
        string displayName)
    {
        _customerStation = customerStation ??
            throw new ArgumentNullException(nameof(customerStation));
        _ = customerStation.GetIncomingFeeder(incomingFeederId);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Incoming feeder display name is required.",
                nameof(displayName));
        }

        _incomingFeederId = incomingFeederId;
        _displayName = displayName.Trim();
    }

    public void Execute()
    {
        IncomingFeeder feeder = _customerStation.GetIncomingFeeder(_incomingFeederId);
        _originalDisplayName ??= feeder.DisplayName;
        feeder.Rename(_displayName);
    }

    public void Undo()
    {
        if (_originalDisplayName is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        _customerStation.RenameIncomingFeeder(
            _incomingFeederId,
            _originalDisplayName);
    }

    public void Redo()
    {
        if (_originalDisplayName is null)
        {
            throw new InvalidOperationException("The command has not been executed.");
        }

        _customerStation.RenameIncomingFeeder(_incomingFeederId, _displayName);
    }
}
