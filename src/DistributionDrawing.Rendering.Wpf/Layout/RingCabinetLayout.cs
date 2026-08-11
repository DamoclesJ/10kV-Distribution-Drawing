using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed class RingCabinetLayout
{
    private readonly Dictionary<Guid, RingCabinetIntervalLayout> _intervals = [];

    public RingCabinetLayout(
        Guid cabinetId,
        DocumentPoint position,
        double widthMillimeters,
        double heightMillimeters,
        double mainBusYMillimeters,
        IEnumerable<RingCabinetIntervalLayout> intervalLayouts,
        DocumentPoint? labelOffset = null)
    {
        if (cabinetId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cabinet ID cannot be empty.",
                nameof(cabinetId));
        }

        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Cabinet layout dimensions must be greater than zero.");
        }

        if (mainBusYMillimeters < 0 || mainBusYMillimeters > heightMillimeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mainBusYMillimeters),
                "Main bus Y must be within the cabinet bounds.");
        }

        CabinetId = cabinetId;
        Position = position;
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        MainBusYMillimeters = mainBusYMillimeters;
        LabelOffset = labelOffset ?? new DocumentPoint(0, -8);

        ArgumentNullException.ThrowIfNull(intervalLayouts);
        foreach (RingCabinetIntervalLayout intervalLayout in intervalLayouts)
        {
            AddIntervalLayout(intervalLayout);
        }
    }

    public Guid CabinetId { get; }

    public DocumentPoint Position { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public double MainBusYMillimeters { get; }

    public DocumentPoint LabelOffset { get; }

    public IReadOnlyDictionary<Guid, RingCabinetIntervalLayout> IntervalLayouts => _intervals;

    public void AddIntervalLayout(RingCabinetIntervalLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_intervals.TryAdd(layout.IntervalId, layout))
        {
            throw new InvalidOperationException(
                $"An interval layout for '{layout.IntervalId}' already exists.");
        }
    }
}
