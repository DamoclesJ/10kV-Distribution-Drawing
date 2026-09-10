namespace DistributionDrawing.Domain.Devices.CustomerStations;

public sealed class CustomerStation : Device
{
    private readonly IReadOnlyList<IncomingFeeder> _incomingFeeders;

    public CustomerStation(
        Guid id,
        StationKind stationKind,
        IEnumerable<IncomingFeeder> incomingFeeders)
        : base(id, DeviceType.CustomerStation, voltageLevel: IncomingFeeder.TenKilovolts)
    {
        if (!Enum.IsDefined(stationKind))
        {
            throw new ArgumentOutOfRangeException(nameof(stationKind));
        }

        StationKind = stationKind;
        IncomingFeeder[] feeders = incomingFeeders?
            .OrderBy(feeder => feeder.Sequence)
            .ToArray() ?? throw new ArgumentNullException(nameof(incomingFeeders));
        _incomingFeeders = Array.AsReadOnly(feeders);
        ValidateStructure();
    }

    public StationKind StationKind { get; }

    public IReadOnlyList<IncomingFeeder> IncomingFeeders => _incomingFeeders;

    public IncomingFeeder GetIncomingFeeder(Guid incomingFeederId)
    {
        return _incomingFeeders.SingleOrDefault(feeder =>
                feeder.IncomingFeederId == incomingFeederId)
            ?? throw new InvalidOperationException(
                $"Incoming feeder '{incomingFeederId}' does not exist in customer station '{Id}'.");
    }

    public override void Rename(string? displayName)
    {
        throw new InvalidOperationException(
            "A customer station does not have a customer-station-level business name.");
    }

    public void RenameIncomingFeeder(Guid incomingFeederId, string displayName)
    {
        GetIncomingFeeder(incomingFeederId).Rename(displayName);
    }

    internal void ValidateStructure()
    {
        int expectedCount = StationKind switch
        {
            StationKind.BoxStation => 1,
            StationKind.IndoorStation when _incomingFeeders.Count is 1 or 2 =>
                _incomingFeeders.Count,
            StationKind.IndoorStation => throw new InvalidOperationException(
                "An indoor station requires one or two incoming feeders."),
            _ => throw new InvalidOperationException(
                $"Unsupported customer-station kind '{StationKind}'.")
        };

        if (_incomingFeeders.Count != expectedCount)
        {
            throw new InvalidOperationException(
                "A box station requires exactly one incoming feeder.");
        }

        foreach (IncomingFeeder feeder in _incomingFeeders)
        {
            feeder.ValidateStructure();
        }

        int[] expectedSequences = Enumerable.Range(1, _incomingFeeders.Count).ToArray();
        if (!_incomingFeeders.Select(feeder => feeder.Sequence).SequenceEqual(expectedSequences))
        {
            throw new InvalidOperationException(
                "Incoming feeder sequences must be unique, continuous, and start at one.");
        }

        Guid[] aggregateIds = [
            Id,
            .. _incomingFeeders.SelectMany(feeder => new[]
            {
                feeder.IncomingFeederId,
                feeder.IsolationSwitch.Id,
                feeder.CableTerminalId,
                feeder.StationTerminalId,
                feeder.ElectricalNodeId
            })
        ];
        if (aggregateIds.Distinct().Count() != aggregateIds.Length)
        {
            throw new InvalidOperationException(
                "Customer-station aggregate IDs must be unique.");
        }
    }
}
