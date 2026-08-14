using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record JointLayout
{
    public JointLayout(
        Guid intermediateTerminalId,
        DocumentPoint position,
        double sizeMillimeters = 4,
        DocumentPoint? labelOffset = null)
    {
        if (intermediateTerminalId == Guid.Empty)
        {
            throw new ArgumentException(
                "Intermediate terminal ID cannot be empty.",
                nameof(intermediateTerminalId));
        }

        if (!double.IsFinite(sizeMillimeters) || sizeMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeMillimeters),
                "Joint size must be greater than zero.");
        }

        IntermediateTerminalId = intermediateTerminalId;
        Position = position;
        SizeMillimeters = sizeMillimeters;
        LabelPosition = new DocumentPoint(
            position.XMillimeters + (labelOffset?.XMillimeters ?? 3),
            position.YMillimeters + (labelOffset?.YMillimeters ?? -3));
    }

    public Guid IntermediateTerminalId { get; }

    public DocumentPoint Position { get; }

    public double SizeMillimeters { get; }

    public DocumentPoint LabelPosition { get; }
}
