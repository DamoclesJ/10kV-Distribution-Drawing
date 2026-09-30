using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record EnergizationVisualStyle(
    Color Color, SceneStrokeStyle StrokeStyle, double ThicknessMillimeters);

public static class EnergizationVisualStyleResolver
{
    public static EnergizationVisualStyle Resolve(ElectricalVisualState state) => state switch
    {
        ElectricalVisualState.Energized => new(Colors.Red, SceneStrokeStyle.Solid, 0.9),
        ElectricalVisualState.Deenergized => new(Colors.Black, SceneStrokeStyle.Solid, 0.9),
        ElectricalVisualState.Unknown => new(Colors.Gray, SceneStrokeStyle.Dotted, 0.9),
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
