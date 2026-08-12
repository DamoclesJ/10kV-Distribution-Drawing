namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Runtime layout graph reconstructed from persistence data. It contains only
/// document millimeter coordinates and no WPF visuals or editor state.
/// </summary>
public sealed class RuntimeLayoutDocument
{
    private readonly Dictionary<Guid, RingCabinetLayout> _ringCabinetLayouts;

    public RuntimeLayoutDocument(
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts)
    {
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        DrawingLayout = drawingLayout;
        _ringCabinetLayouts = ringCabinetLayouts.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public DrawingLayout DrawingLayout { get; }

    public IReadOnlyDictionary<Guid, RingCabinetLayout> RingCabinetLayouts =>
        _ringCabinetLayouts;

    public void AddRingCabinet(RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!_ringCabinetLayouts.TryAdd(layout.CabinetId, layout))
        {
            throw new InvalidOperationException(
                $"A layout for ring cabinet '{layout.CabinetId}' already exists.");
        }
    }

    public RingCabinetLayout RemoveRingCabinet(Guid cabinetId)
    {
        if (!_ringCabinetLayouts.Remove(cabinetId, out RingCabinetLayout? layout))
        {
            throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{cabinetId}'.");
        }

        return layout;
    }
}
