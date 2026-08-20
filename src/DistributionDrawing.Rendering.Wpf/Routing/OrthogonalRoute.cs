using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public readonly record struct OrthogonalRouteSegment(
    DocumentPoint Start,
    DocumentPoint End,
    int Index)
{
    public bool IsHorizontal => Start.YMillimeters == End.YMillimeters;

    public bool IsVertical => Start.XMillimeters == End.XMillimeters;

    public double Length => Math.Abs(End.XMillimeters - Start.XMillimeters) +
                            Math.Abs(End.YMillimeters - Start.YMillimeters);
}

/// <summary>
/// A transient, deterministic rendering route. It is never persisted and
/// carries no electrical meaning beyond the referenced connection terminals.
/// </summary>
public sealed class OrthogonalRoute
{
    public OrthogonalRoute(
        Guid connectionId,
        ConnectionType connectionType,
        Guid startTerminalId,
        Guid endTerminalId,
        IEnumerable<DocumentPoint> points)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("Connection ID cannot be empty.", nameof(connectionId));
        }

        if (startTerminalId == Guid.Empty || endTerminalId == Guid.Empty)
        {
            throw new ArgumentException("Route terminal IDs are required.");
        }

        if (startTerminalId == endTerminalId)
        {
            throw new ArgumentException("A route requires two different terminals.");
        }

        ArgumentNullException.ThrowIfNull(points);
        DocumentPoint[] normalized = Normalize(points);
        if (normalized.Length < 2)
        {
            throw new ArgumentException("A route requires at least two distinct points.", nameof(points));
        }

        ConnectionId = connectionId;
        ConnectionType = connectionType;
        StartTerminalId = startTerminalId;
        EndTerminalId = endTerminalId;
        Points = Array.AsReadOnly(normalized);
        Segments = Array.AsReadOnly(normalized
            .Zip(normalized.Skip(1), (start, end) => (start, end))
            .Select((pair, index) => new OrthogonalRouteSegment(pair.start, pair.end, index))
            .ToArray());
        Bounds = CreateBounds(normalized);
        Length = Segments.Sum(segment => segment.Length);
        Midpoint = FindPointAtDistance(Length / 2);
    }

    public Guid ConnectionId { get; }

    public ConnectionType ConnectionType { get; }

    public Guid StartTerminalId { get; }

    public Guid EndTerminalId { get; }

    public IReadOnlyList<DocumentPoint> Points { get; }

    public IReadOnlyList<OrthogonalRouteSegment> Segments { get; }

    public DocumentRect Bounds { get; }

    public double Length { get; }

    public DocumentPoint Midpoint { get; }

    public bool SharesTerminalWith(OrthogonalRoute other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartTerminalId == other.StartTerminalId ||
               StartTerminalId == other.EndTerminalId ||
               EndTerminalId == other.StartTerminalId ||
               EndTerminalId == other.EndTerminalId;
    }

    private DocumentPoint FindPointAtDistance(double distance)
    {
        double remaining = distance;
        foreach (OrthogonalRouteSegment segment in Segments)
        {
            if (remaining <= segment.Length)
            {
                double ratio = segment.Length == 0 ? 0 : remaining / segment.Length;
                return new DocumentPoint(
                    segment.Start.XMillimeters +
                    (segment.End.XMillimeters - segment.Start.XMillimeters) * ratio,
                    segment.Start.YMillimeters +
                    (segment.End.YMillimeters - segment.Start.YMillimeters) * ratio);
            }

            remaining -= segment.Length;
        }

        return Points[^1];
    }

    private static DocumentPoint[] Normalize(IEnumerable<DocumentPoint> points)
    {
        var values = new List<DocumentPoint>();
        foreach (DocumentPoint point in points)
        {
            if (!double.IsFinite(point.XMillimeters) || !double.IsFinite(point.YMillimeters))
            {
                throw new ArgumentException("Route points must use finite coordinates.", nameof(points));
            }

            if (values.Count == 0 || values[^1] != point)
            {
                values.Add(point);
            }
        }

        for (var index = 1; index < values.Count; index++)
        {
            DocumentPoint previous = values[index - 1];
            DocumentPoint current = values[index];
            if (previous.XMillimeters != current.XMillimeters &&
                previous.YMillimeters != current.YMillimeters)
            {
                throw new ArgumentException("Every route segment must be horizontal or vertical.", nameof(points));
            }
        }

        var merged = new List<DocumentPoint>();
        foreach (DocumentPoint point in values)
        {
            merged.Add(point);
            while (merged.Count >= 3 && AreCollinear(
                       merged[^3],
                       merged[^2],
                       merged[^1]))
            {
                merged.RemoveAt(merged.Count - 2);
            }
        }

        return merged.ToArray();
    }

    private static bool AreCollinear(
        DocumentPoint first,
        DocumentPoint second,
        DocumentPoint third)
    {
        return first.XMillimeters == second.XMillimeters &&
               second.XMillimeters == third.XMillimeters ||
               first.YMillimeters == second.YMillimeters &&
               second.YMillimeters == third.YMillimeters;
    }

    private static DocumentRect CreateBounds(IReadOnlyList<DocumentPoint> points)
    {
        double minX = points.Min(point => point.XMillimeters);
        double minY = points.Min(point => point.YMillimeters);
        double maxX = points.Max(point => point.XMillimeters);
        double maxY = points.Max(point => point.YMillimeters);
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }
}
