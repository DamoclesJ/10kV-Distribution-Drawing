using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record CustomerStationIncomingFeederLayout
{
    public CustomerStationIncomingFeederLayout(
        Guid incomingFeederId,
        bool showIncomingSwitch)
    {
        if (incomingFeederId == Guid.Empty)
        {
            throw new ArgumentException(
                "Incoming feeder ID cannot be empty.",
                nameof(incomingFeederId));
        }

        IncomingFeederId = incomingFeederId;
        ShowIncomingSwitch = showIncomingSwitch;
    }

    public Guid IncomingFeederId { get; }

    public bool ShowIncomingSwitch { get; }
}

public sealed record CustomerStationLayout
{
    private readonly IReadOnlyDictionary<Guid, CustomerStationIncomingFeederLayout>
        _incomingFeeders;

    public CustomerStationLayout(
        Guid customerStationId,
        DocumentPoint position,
        IEnumerable<CustomerStationIncomingFeederLayout> incomingFeeders)
    {
        if (customerStationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Customer station ID cannot be empty.",
                nameof(customerStationId));
        }

        if (!double.IsFinite(position.XMillimeters) ||
            !double.IsFinite(position.YMillimeters))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Customer station position must be finite.");
        }

        CustomerStationIncomingFeederLayout[] feederLayouts = incomingFeeders?.ToArray()
            ?? throw new ArgumentNullException(nameof(incomingFeeders));
        if (feederLayouts.Select(layout => layout.IncomingFeederId).Distinct().Count() !=
            feederLayouts.Length)
        {
            throw new ArgumentException(
                "Customer station feeder layout IDs must be unique.",
                nameof(incomingFeeders));
        }

        CustomerStationId = customerStationId;
        Position = position;
        _incomingFeeders = feederLayouts.ToDictionary(
            layout => layout.IncomingFeederId,
            layout => layout);
    }

    public Guid CustomerStationId { get; }

    public DocumentPoint Position { get; }

    public IReadOnlyDictionary<Guid, CustomerStationIncomingFeederLayout> IncomingFeeders =>
        _incomingFeeders;

    public void ValidateFor(CustomerStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (CustomerStationId != station.Id)
        {
            throw new InvalidOperationException(
                "Customer station layout identity does not match its Domain aggregate.");
        }

        HashSet<Guid> feederIds = station.IncomingFeeders
            .Select(feeder => feeder.IncomingFeederId)
            .ToHashSet();
        if (!_incomingFeeders.Keys.ToHashSet().SetEquals(feederIds))
        {
            throw new InvalidOperationException(
                $"Customer station '{station.Id}' layout coverage does not match its incoming feeders.");
        }

        if (station.StationKind == StationKind.BoxStation &&
            _incomingFeeders.Values.Any(layout => !layout.ShowIncomingSwitch))
        {
            throw new InvalidOperationException(
                $"Box station '{station.Id}' requires its incoming switch to be visible.");
        }
    }
}
