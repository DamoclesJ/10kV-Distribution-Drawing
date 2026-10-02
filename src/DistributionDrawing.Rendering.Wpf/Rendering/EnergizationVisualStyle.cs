using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record EnergizationVisualStyle(
    Color Color, SceneStrokeStyle StrokeStyle);

public static class EnergizationVisualStyleResolver
{
    // Thickness belongs to the original professional geometry and is never overridden here.
    public static EnergizationVisualStyle? Resolve(ElectricalVisualState state,
        SceneStrokeStyle normalStyle = SceneStrokeStyle.Solid) => state switch
    {
        ElectricalVisualState.Energized => new(Colors.Red, normalStyle),
        _ => null
    };
}
