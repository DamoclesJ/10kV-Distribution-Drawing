using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetLayoutBuildResult
{
    public RingCabinetLayoutBuildResult(RingCabinetLayout layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public RingCabinetLayout Layout { get; }
}
