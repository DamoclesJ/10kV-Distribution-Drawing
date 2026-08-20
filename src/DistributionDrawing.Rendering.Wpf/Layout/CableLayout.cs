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
        LabelPosition = labelPosition ?? PathMidpoint(points);
    }

    public Guid CableSegmentId { get; }

    public IReadOnlyList<DocumentPoint> Path { get; }

    public DocumentPoint Start => Path[0];

    public DocumentPoint End => Path[^1];

    public DocumentPoint LabelPosition { get; }

    private static DocumentPoint PathMidpoint(IReadOnlyList<DocumentPoint> path)
    {
        double totalLength = 0;
        for (var index = 1; index < path.Count; index++)
        {
            totalLength += Distance(path[index - 1], path[index]);
        }

        double remaining = totalLength / 2;
        for (var index = 1; index < path.Count; index++)
        {
            DocumentPoint start = path[index - 1];
            DocumentPoint end = path[index];
            double length = Distance(start, end);
            if (remaining <= length)
            {
                double ratio = length == 0 ? 0 : remaining / length;
                return new DocumentPoint(
                    start.XMillimeters + (end.XMillimeters - start.XMillimeters) * ratio,
                    start.YMillimeters + (end.YMillimeters - start.YMillimeters) * ratio);
            }

            remaining -= length;
        }

        return path[^1];
    }

    private static double Distance(DocumentPoint start, DocumentPoint end) =>
        Math.Abs(end.XMillimeters - start.XMillimeters) +
        Math.Abs(end.YMillimeters - start.YMillimeters);
}
