using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed record CableLayout
{
    public CableLayout(
        Guid cableSegmentId,
        IEnumerable<DocumentPoint> path,
        DocumentPoint? labelPosition = null)
    {
        if (cableSegmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Cable segment ID cannot be empty.",
                nameof(cableSegmentId));
        }

        ArgumentNullException.ThrowIfNull(path);

        DocumentPoint[] points = path.ToArray();
        if (points.Length < 2)
        {
            throw new ArgumentException(
                "Cable layout path requires at least two points.",
                nameof(path));
        }

        CableSegmentId = cableSegmentId;
        Path = points;
        LabelPosition = labelPosition ?? Midpoint(points[0], points[^1]);
    }

    public Guid CableSegmentId { get; }

    public IReadOnlyList<DocumentPoint> Path { get; }

    public DocumentPoint Start => Path[0];

    public DocumentPoint End => Path[^1];

    public DocumentPoint LabelPosition { get; }

    private static DocumentPoint Midpoint(DocumentPoint start, DocumentPoint end)
    {
        return new DocumentPoint(
            (start.XMillimeters + end.XMillimeters) / 2,
            (start.YMillimeters + end.YMillimeters) / 2);
    }
}
