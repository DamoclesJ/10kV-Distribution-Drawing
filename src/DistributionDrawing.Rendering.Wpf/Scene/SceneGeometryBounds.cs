namespace DistributionDrawing.Rendering.Wpf.Scene;

internal static class SceneGeometryBounds
{
    public static DocumentRect FromPoints(IReadOnlyList<DocumentPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("At least one point is required.", nameof(points));
        }

        double minimumX = points.Min(point => point.XMillimeters);
        double minimumY = points.Min(point => point.YMillimeters);
        double maximumX = points.Max(point => point.XMillimeters);
        double maximumY = points.Max(point => point.YMillimeters);
        return new DocumentRect(
            minimumX,
            minimumY,
            maximumX - minimumX,
            maximumY - minimumY);
    }

    public static DocumentRect Expand(DocumentRect bounds, double amount)
    {
        return new DocumentRect(
            bounds.XMillimeters - amount,
            bounds.YMillimeters - amount,
            bounds.WidthMillimeters + amount * 2,
            bounds.HeightMillimeters + amount * 2);
    }

    public static void ValidateBounds(DocumentRect bounds, string parameterName)
    {
        if (!IsFinite(bounds.XMillimeters) ||
            !IsFinite(bounds.YMillimeters) ||
            !IsFinite(bounds.WidthMillimeters) ||
            !IsFinite(bounds.HeightMillimeters) ||
            bounds.WidthMillimeters <= 0 ||
            bounds.HeightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Geometry bounds must be finite and have positive dimensions.");
        }
    }

    public static void ValidateThickness(double thicknessMillimeters)
    {
        if (!IsFinite(thicknessMillimeters) || thicknessMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thicknessMillimeters),
                "Stroke thickness must be finite and greater than zero.");
        }
    }

    public static void ValidatePoint(DocumentPoint point, string parameterName)
    {
        if (!IsFinite(point.XMillimeters) || !IsFinite(point.YMillimeters))
        {
            throw new ArgumentException(
                "Geometry points must use finite coordinates.",
                parameterName);
        }
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
