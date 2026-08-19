using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed record ScenePolyline : SceneElement
{
    public ScenePolyline(
        IEnumerable<DocumentPoint> points,
        bool isClosed,
        Color stroke,
        double thicknessMillimeters,
        Color? fill = null,
        SceneStrokeStyle strokeStyle = SceneStrokeStyle.Solid)
    {
        ArgumentNullException.ThrowIfNull(points);
        DocumentPoint[] pointArray = points.ToArray();
        int minimumPointCount = isClosed ? 3 : 2;
        if (pointArray.Length < minimumPointCount)
        {
            throw new ArgumentException(
                $"A {(isClosed ? "closed" : "open")} polyline requires at least {minimumPointCount} points.",
                nameof(points));
        }

        foreach (DocumentPoint point in pointArray)
        {
            SceneGeometryBounds.ValidatePoint(point, nameof(points));
        }

        if (!isClosed && fill is not null)
        {
            throw new ArgumentException(
                "Only a closed polyline can have a fill.",
                nameof(fill));
        }

        SceneGeometryBounds.ValidateThickness(thicknessMillimeters);
        if (!Enum.IsDefined(strokeStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(strokeStyle));
        }

        Points = Array.AsReadOnly(pointArray);
        IsClosed = isClosed;
        Stroke = stroke;
        ThicknessMillimeters = thicknessMillimeters;
        Fill = fill;
        StrokeStyle = strokeStyle;
        Bounds = SceneGeometryBounds.FromPoints(Points);
        HitTestBounds = SceneGeometryBounds.Expand(Bounds, thicknessMillimeters / 2);
    }

    public IReadOnlyList<DocumentPoint> Points { get; }

    public bool IsClosed { get; }

    public DocumentRect Bounds { get; }

    public Color Stroke { get; }

    public double ThicknessMillimeters { get; }

    public Color? Fill { get; }

    public SceneStrokeStyle StrokeStyle { get; }
}
