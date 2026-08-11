using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record RingCabinetSwitchLayout
{
    public RingCabinetSwitchLayout(
        Guid switchDeviceId,
        DocumentPoint relativePosition,
        double widthMillimeters = 14,
        double heightMillimeters = 8,
        DocumentPoint? labelOffset = null)
    {
        if (switchDeviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Switch device ID cannot be empty.",
                nameof(switchDeviceId));
        }

        if (widthMillimeters <= 0 || heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "Switch layout dimensions must be greater than zero.");
        }

        SwitchDeviceId = switchDeviceId;
        RelativePosition = relativePosition;
        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        LabelOffset = labelOffset ?? new DocumentPoint(0, -4);
    }

    public Guid SwitchDeviceId { get; }

    public DocumentPoint RelativePosition { get; }

    public double WidthMillimeters { get; }

    public double HeightMillimeters { get; }

    public DocumentPoint LabelOffset { get; }
}
