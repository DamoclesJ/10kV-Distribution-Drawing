using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record EnergizationVisualStyle(
    Color Color, SceneStrokeStyle StrokeStyle);

public static class EnergizationVisualStyleResolver
{
    public static EnergizationVisualStyle Resolve(
        ElectricalVisualState state,
        SceneStrokeStyle originalStyle = SceneStrokeStyle.Solid) => state switch
    {
        ElectricalVisualState.Energized => new(Colors.Red, originalStyle),
        ElectricalVisualState.Deenergized => new(Colors.Black, originalStyle),
        ElectricalVisualState.Unknown => new(Colors.Gray, SceneStrokeStyle.Dotted),
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
