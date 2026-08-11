using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed class RingCabinetIntervalLayout
{
    private readonly Dictionary<Guid, RingCabinetSwitchLayout> _switches = [];

    public RingCabinetIntervalLayout(
        Guid intervalId,
        DocumentPoint relativePosition,
        double widthMillimeters = 42,
        double heightMillimeters = 90,
        DocumentPoint? sequenceLabelOffset = null,
        DocumentPoint? nameLabelOffset = null,
        IEnumerable<RingCabinetSwitchLayout>? switchLayouts = null)
    {
        if (intervalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Interval ID cannot be empty.",
                nameof(intervalId));
        }

        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Interval layout dimensions must be greater than zero.");
        }

        IntervalId = intervalId;
        RelativePosition = relativePosition;
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        SequenceLabelOffset = sequenceLabelOffset ?? new DocumentPoint(2, -8);
        NameLabelOffset = nameLabelOffset ?? new DocumentPoint(2, heightMillimeters + 5);

        if (switchLayouts is not null)
        {
            foreach (RingCabinetSwitchLayout switchLayout in switchLayouts)
            {
                AddSwitchLayout(switchLayout);
            }
        }
    }

    public Guid IntervalId { get; }

    public DocumentPoint RelativePosition { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint SequenceLabelOffset { get; }

    public DocumentPoint NameLabelOffset { get; }

    public IReadOnlyDictionary<Guid, RingCabinetSwitchLayout> SwitchLayouts => _switches;

    public void AddSwitchLayout(RingCabinetSwitchLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_switches.TryAdd(layout.SwitchDeviceId, layout))
        {
            throw new InvalidOperationException(
                $"A switch layout for '{layout.SwitchDeviceId}' already exists.");
        }
    }
}
