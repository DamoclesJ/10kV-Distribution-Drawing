namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Runtime layout graph reconstructed from persistence data. It contains only
/// document millimeter coordinates and no WPF visuals or editor state.
/// </summary>
public sealed class RuntimeLayoutDocument
{
    public RuntimeLayoutDocument(
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts)
    {
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        DrawingLayout = drawingLayout;
        RingCabinetLayouts = ringCabinetLayouts;
    }

    public DrawingLayout DrawingLayout { get; }

    public IReadOnlyDictionary<Guid, RingCabinetLayout> RingCabinetLayouts { get; }
}
