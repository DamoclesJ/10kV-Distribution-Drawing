using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed record SceneEllipse : SceneElement
{
    public SceneEllipse(
        DocumentRect bounds,
        Color stroke,
        double thicknessMillimeters,
        Color? fill = null,
        SceneStrokeStyle strokeStyle = SceneStrokeStyle.Solid)
    {
        SceneGeometryBounds.ValidateBounds(bounds, nameof(bounds));
        SceneGeometryBounds.ValidateThickness(thicknessMillimeters);
        if (!Enum.IsDefined(strokeStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(strokeStyle));
        }

        Bounds = bounds;
        Stroke = stroke;
        ThicknessMillimeters = thicknessMillimeters;
        Fill = fill;
        StrokeStyle = strokeStyle;
        HitTestBounds = SceneGeometryBounds.Expand(bounds, thicknessMillimeters / 2);
    }

    public DocumentRect Bounds { get; }

    public Color Stroke { get; }

    public double ThicknessMillimeters { get; }

    public Color? Fill { get; }

    public SceneStrokeStyle StrokeStyle { get; }
}
