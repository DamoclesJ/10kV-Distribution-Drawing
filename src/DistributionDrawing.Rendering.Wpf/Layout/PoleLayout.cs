using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record PoleLayout
{
    public PoleLayout(
        Guid poleId,
        DocumentPoint position,
        double widthMillimeters = 4,
        double heightMillimeters = 42,
        DocumentPoint? labelOffset = null)
    {
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
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        LabelOffset = labelOffset ?? new DocumentPoint(5, -5);
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
