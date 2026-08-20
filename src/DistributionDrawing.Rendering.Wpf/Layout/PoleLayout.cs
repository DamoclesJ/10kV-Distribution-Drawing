using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Metrics;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record PoleLayout
{
    public PoleLayout(
        Guid poleId,
        DocumentPoint position,
        double? widthMillimeters = null,
        double? heightMillimeters = null,
        DocumentPoint? labelOffset = null)
    {
        DrawingMetrics metrics = DrawingMetrics.Default;
        widthMillimeters ??= metrics.Pole.PoleRadius * 2;
        heightMillimeters ??= metrics.Pole.PoleRadius * 2;
        if (poleId == Guid.Empty)
        {
            throw new ArgumentException("Pole ID cannot be empty.", nameof(poleId));
        }

        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Pole layout dimensions must be greater than zero.");
        }

        PoleId = poleId;
        Position = position;
        WidthMillimeters = widthMillimeters.Value;
        HeightMillimeters = heightMillimeters.Value;
        LabelOffset = labelOffset ?? metrics.Pole.LabelOffset;
    }

    public Guid PoleId { get; }

    public DocumentPoint Position { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint LabelOffset { get; }

    public PoleLayout MoveTo(DocumentPoint position)
    {
        return new PoleLayout(
            PoleId,
            position,
            WidthMillimeters,
            HeightMillimeters,
            LabelOffset);
    }
}
