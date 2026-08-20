using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed record SceneArc : SceneElement
{
    public SceneArc(
        DocumentPoint center,
        double radiusMillimeters,
        double startAngleDegrees,
        double sweepAngleDegrees,
        Color stroke,
        double thicknessMillimeters,
        SceneStrokeStyle strokeStyle = SceneStrokeStyle.Solid)
    {
        SceneGeometryBounds.ValidatePoint(center, nameof(center));
        if (!double.IsFinite(radiusMillimeters) || radiusMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMillimeters),
                "Arc radius must be finite and greater than zero.");
        }

        if (!double.IsFinite(startAngleDegrees) ||
            !double.IsFinite(sweepAngleDegrees) ||
            sweepAngleDegrees == 0 ||
            Math.Abs(sweepAngleDegrees) > 360)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sweepAngleDegrees),
                "Arc angles must be finite and sweep no more than 360 degrees.");
        }

        SceneGeometryBounds.ValidateThickness(thicknessMillimeters);
        if (!Enum.IsDefined(strokeStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(strokeStyle));
        }

        Center = center;
        RadiusMillimeters = radiusMillimeters;
        StartAngleDegrees = startAngleDegrees;
        SweepAngleDegrees = sweepAngleDegrees;
        Stroke = stroke;
        ThicknessMillimeters = thicknessMillimeters;
        StrokeStyle = strokeStyle;
        Bounds = new DocumentRect(
            center.XMillimeters - radiusMillimeters,
            center.YMillimeters - radiusMillimeters,
            radiusMillimeters * 2,
            radiusMillimeters * 2);
        HitTestBounds = SceneGeometryBounds.Expand(Bounds, thicknessMillimeters / 2);
    }

    public DocumentPoint Center { get; }

    public double RadiusMillimeters { get; }

    public double StartAngleDegrees { get; }

    public double SweepAngleDegrees { get; }

    public Color Stroke { get; }

    public double ThicknessMillimeters { get; }

    public SceneStrokeStyle StrokeStyle { get; }

    public DocumentRect Bounds { get; }
}
