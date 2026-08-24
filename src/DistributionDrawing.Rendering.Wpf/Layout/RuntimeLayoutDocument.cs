namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Runtime layout graph reconstructed from persistence data. It contains only
/// document millimeter coordinates and no WPF visuals or editor state.
/// </summary>
public sealed class RuntimeLayoutDocument
{
    private readonly Dictionary<Guid, RingCabinetLayout> _ringCabinetLayouts;
    private readonly Dictionary<Guid, CableRouteGuide> _cableRouteGuides;

    public RuntimeLayoutDocument(
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts,
        IReadOnlyDictionary<Guid, CableRouteGuide>? cableRouteGuides = null)
    {
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        DrawingLayout = drawingLayout;
        _ringCabinetLayouts = ringCabinetLayouts.ToDictionary(pair => pair.Key, pair => pair.Value);
        _cableRouteGuides = cableRouteGuides?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
    }

    public DrawingLayout DrawingLayout { get; }

    public IReadOnlyDictionary<Guid, RingCabinetLayout> RingCabinetLayouts =>
        _ringCabinetLayouts;

    public IReadOnlyDictionary<Guid, CableRouteGuide> CableRouteGuides => _cableRouteGuides;

    public void SetCableRouteGuide(CableRouteGuide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);
        _cableRouteGuides[guide.CableSegmentId] = guide;
    }

    public bool RemoveCableRouteGuide(Guid cableSegmentId) =>
        _cableRouteGuides.Remove(cableSegmentId);

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

    public void ReplaceRingCabinet(RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!_ringCabinetLayouts.ContainsKey(layout.CabinetId))
        {
            throw new InvalidOperationException(
                $"No layout exists for ring cabinet '{layout.CabinetId}'.");
        }

        _ringCabinetLayouts[layout.CabinetId] = layout;
    }
}

public sealed record CableRouteGuide
{
    public CableRouteGuide(Guid cableSegmentId, double horizontalYMillimeters)
    {
        if (cableSegmentId == Guid.Empty)
        {
            throw new ArgumentException("Cable segment ID cannot be empty.", nameof(cableSegmentId));
        }

        if (!double.IsFinite(horizontalYMillimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalYMillimeters));
        }

        CableSegmentId = cableSegmentId;
        HorizontalYMillimeters = horizontalYMillimeters;
    }

    public Guid CableSegmentId { get; }

    public double HorizontalYMillimeters { get; }
}
