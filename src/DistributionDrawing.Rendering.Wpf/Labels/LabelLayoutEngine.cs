using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Labels;

public sealed class LabelLayoutEngine
{
    private const double CharacterWidthFactor = 0.6;
    private const double MinimumCandidateOffset = 4;
    private const int CandidateRings = 6;

    public IReadOnlyList<LabelLayoutResult> Layout(IEnumerable<LabelRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        LabelRequest[] orderedRequests = requests.ToArray();
        List<LabelLayoutResult> results = [];

        foreach (LabelRequest request in orderedRequests
                     .Select((value, index) => (value, index))
                     .OrderByDescending(item => item.value.Priority)
                     .ThenBy(item => item.value.TargetId)
                     .ThenBy(item => item.index)
            .Select(item => item.value))
        {
            DocumentPoint preferredPosition = Add(request.Anchor, request.Offset);
            DocumentRect preferredBounds = Measure(request, preferredPosition);
            (DocumentPoint position, DocumentRect bounds, bool adjusted, bool collision) =
                request.AllowCollisionAdjustment
                    ? FindPlacement(request, preferredPosition, preferredBounds, results)
                    : (preferredPosition, preferredBounds, false, false);

            results.Add(new LabelLayoutResult(
                request,
                position,
                bounds,
                request.PreferredAlignment,
                adjusted,
                collision));
        }

        return results;
    }

    private static (DocumentPoint Position, DocumentRect Bounds, bool Adjusted, bool Collision)
        FindPlacement(
            LabelRequest request,
            DocumentPoint preferredPosition,
            DocumentRect preferredBounds,
            IReadOnlyList<LabelLayoutResult> placed)
    {
        if (!OverlapsAny(preferredBounds, placed))
        {
            return (preferredPosition, preferredBounds, false, false);
        }

        double step = Math.Max(
            MinimumCandidateOffset,
            request.FontSizeMillimeters / 2);
        foreach (DocumentPoint candidate in CreateCandidates(preferredPosition, step))
        {
            DocumentRect bounds = Measure(request, candidate);
            if (!OverlapsAny(bounds, placed))
            {
                return (candidate, bounds, true, false);
            }
        }

        return (preferredPosition, preferredBounds, false, true);
    }

    private static IEnumerable<DocumentPoint> CreateCandidates(
        DocumentPoint preferred,
        double step)
    {
        for (int ring = 1; ring <= CandidateRings; ring++)
        {
            double distance = step * ring;
            yield return new DocumentPoint(
                preferred.XMillimeters,
                preferred.YMillimeters - distance);
            yield return new DocumentPoint(
                preferred.XMillimeters,
                preferred.YMillimeters + distance);
            yield return new DocumentPoint(
                preferred.XMillimeters - distance,
                preferred.YMillimeters);
            yield return new DocumentPoint(
                preferred.XMillimeters + distance,
                preferred.YMillimeters);
            yield return new DocumentPoint(
                preferred.XMillimeters - distance,
                preferred.YMillimeters - distance);
            yield return new DocumentPoint(
                preferred.XMillimeters + distance,
                preferred.YMillimeters - distance);
            yield return new DocumentPoint(
                preferred.XMillimeters - distance,
                preferred.YMillimeters + distance);
            yield return new DocumentPoint(
                preferred.XMillimeters + distance,
                preferred.YMillimeters + distance);
        }
    }

    private static DocumentRect Measure(LabelRequest request, DocumentPoint position)
    {
        double width = request.MeasuredWidthMillimeters ?? Math.Max(
            request.FontSizeMillimeters,
            request.Text.Length * request.FontSizeMillimeters * CharacterWidthFactor);

        double x = request.PreferredAlignment switch
        {
            LabelAlignment.Left => position.XMillimeters,
            LabelAlignment.Right => position.XMillimeters - width,
            _ => position.XMillimeters - width / 2
        };

        return new DocumentRect(
            x,
            position.YMillimeters - request.FontSizeMillimeters,
            width,
            request.FontSizeMillimeters);
    }

    private static bool OverlapsAny(
        DocumentRect candidate,
        IReadOnlyList<LabelLayoutResult> placed)
    {
        return placed.Any(result => Overlaps(candidate, result.Bounds));
    }

    private static bool Overlaps(DocumentRect left, DocumentRect right)
    {
        return left.XMillimeters < right.XMillimeters + right.WidthMillimeters &&
               left.XMillimeters + left.WidthMillimeters > right.XMillimeters &&
               left.YMillimeters < right.YMillimeters + right.HeightMillimeters &&
               left.YMillimeters + left.HeightMillimeters > right.YMillimeters;
    }

    private static DocumentPoint Add(DocumentPoint left, DocumentPoint right)
    {
        return new DocumentPoint(
            left.XMillimeters + right.XMillimeters,
            left.YMillimeters + right.YMillimeters);
    }
}
