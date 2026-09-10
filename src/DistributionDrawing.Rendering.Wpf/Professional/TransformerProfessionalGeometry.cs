using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public sealed record TransformerLineSegment(DocumentPoint Start, DocumentPoint End);

public sealed record TransformerProfessionalGeometry(
    IReadOnlyList<DocumentRect> Circles,
    IReadOnlyList<TransformerLineSegment> Lines,
    IReadOnlyList<DocumentPoint> Polygon,
    DocumentRect Bounds,
    DocumentPoint HvAnchor,
    TerminalAnchorDirection HvDirection)
{
    public static TransformerProfessionalGeometry Create(
        Transformer transformer,
        TransformerLayout layout,
        TransformerDrawingMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(metrics);
        if (layout.TransformerId != transformer.Id)
        {
            throw new InvalidOperationException("Transformer layout identity does not match the Domain transformer.");
        }

        layout.ValidateFor(transformer.TransformerKind);
        return transformer.TransformerKind switch
        {
            TransformerKind.PublicPoleMounted => CreatePublicPoleMounted(layout.Position, metrics),
            TransformerKind.DedicatedPoleMounted => CreateDedicatedPoleMounted(layout.Position, metrics),
            TransformerKind.PublicIndoor => CreatePublicIndoor(layout.Position, layout.Orientation, metrics),
            _ => throw new ArgumentOutOfRangeException(nameof(transformer))
        };
    }

    private static TransformerProfessionalGeometry CreatePublicPoleMounted(
        DocumentPoint center,
        TransformerDrawingMetrics metrics)
    {
        DocumentRect main = Circle(center, metrics.MainRadius);
        double smallCircleY = Math.Sqrt(
            Math.Pow(metrics.MainRadius + metrics.SmallCircleRadius, 2) -
            Math.Pow(metrics.SmallCircleOffsetX, 2));
        DocumentRect left = Circle(
            Offset(center, -metrics.SmallCircleOffsetX, smallCircleY),
            metrics.SmallCircleRadius);
        DocumentRect right = Circle(
            Offset(center, metrics.SmallCircleOffsetX, smallCircleY),
            metrics.SmallCircleRadius);
        DocumentPoint teeCenter = Offset(center, 0, metrics.TeeTopY);
        DocumentPoint anchor = Offset(center, 0, metrics.MainRadius);
        TransformerLineSegment[] lines =
        [
            new(Offset(teeCenter, -metrics.TeeHalfWidth, 0), Offset(teeCenter, metrics.TeeHalfWidth, 0)),
            new(teeCenter, anchor)
        ];
        return new([main, left, right], lines, [], Union([main, left, right]), anchor, TerminalAnchorDirection.Down);
    }

    private static TransformerProfessionalGeometry CreateDedicatedPoleMounted(
        DocumentPoint center,
        TransformerDrawingMetrics metrics)
    {
        DocumentPoint apex = Offset(center, 0, metrics.TriangleApexY);
        DocumentPoint leftBase = Offset(center, -metrics.TriangleHalfWidth, metrics.TriangleBaseY);
        DocumentPoint rightBase = Offset(center, metrics.TriangleHalfWidth, metrics.TriangleBaseY);
        double smallCircleY = metrics.TriangleBaseY + metrics.SmallCircleRadius;
        DocumentRect left = Circle(
            Offset(center, -metrics.SmallCircleOffsetX, smallCircleY),
            metrics.SmallCircleRadius);
        DocumentRect right = Circle(
            Offset(center, metrics.SmallCircleOffsetX, smallCircleY),
            metrics.SmallCircleRadius);
        DocumentRect triangleBounds = FromPoints([apex, leftBase, rightBase]);
        return new([left, right], [], [apex, leftBase, rightBase], Union([triangleBounds, left, right]), apex, TerminalAnchorDirection.Up);
    }

    private static TransformerProfessionalGeometry CreatePublicIndoor(
        DocumentPoint center,
        TransformerOrientation orientation,
        TransformerDrawingMetrics metrics)
    {
        double halfSpacing = metrics.IndoorCoilCenterSpacing / 2;
        DocumentPoint firstCenter = orientation == TransformerOrientation.Horizontal
            ? Offset(center, -halfSpacing, 0)
            : Offset(center, 0, -halfSpacing);
        DocumentPoint secondCenter = orientation == TransformerOrientation.Horizontal
            ? Offset(center, halfSpacing, 0)
            : Offset(center, 0, halfSpacing);
        DocumentRect first = Circle(firstCenter, metrics.IndoorCoilRadius);
        DocumentRect second = Circle(secondCenter, metrics.IndoorCoilRadius);
        DocumentPoint anchor = orientation == TransformerOrientation.Horizontal
            ? Offset(firstCenter, -metrics.IndoorCoilRadius, 0)
            : Offset(firstCenter, 0, -metrics.IndoorCoilRadius);
        TerminalAnchorDirection direction = orientation == TransformerOrientation.Horizontal
            ? TerminalAnchorDirection.Left
            : TerminalAnchorDirection.Up;
        return new([first, second], [], [], Union([first, second]), anchor, direction);
    }

    private static DocumentRect Circle(DocumentPoint center, double radius) => new(
        center.XMillimeters - radius,
        center.YMillimeters - radius,
        radius * 2,
        radius * 2);

    private static DocumentPoint Offset(DocumentPoint point, double x, double y) =>
        new(point.XMillimeters + x, point.YMillimeters + y);

    private static DocumentRect FromPoints(IReadOnlyList<DocumentPoint> points)
    {
        double minX = points.Min(point => point.XMillimeters);
        double minY = points.Min(point => point.YMillimeters);
        double maxX = points.Max(point => point.XMillimeters);
        double maxY = points.Max(point => point.YMillimeters);
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static DocumentRect Union(IReadOnlyList<DocumentRect> bounds)
    {
        double minX = bounds.Min(item => item.XMillimeters);
        double minY = bounds.Min(item => item.YMillimeters);
        double maxX = bounds.Max(item => item.XMillimeters + item.WidthMillimeters);
        double maxY = bounds.Max(item => item.YMillimeters + item.HeightMillimeters);
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }
}
