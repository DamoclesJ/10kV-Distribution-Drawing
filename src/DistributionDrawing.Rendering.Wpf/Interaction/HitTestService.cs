using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class HitTestService
{
    public HitTestResult? HitTest(
        IEnumerable<SceneElement> elements,
        DocumentPoint point,
        double toleranceMillimeters = 0)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (toleranceMillimeters < 0 ||
            double.IsNaN(toleranceMillimeters) ||
            double.IsInfinity(toleranceMillimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMillimeters));
        }

        SceneElement? hit = elements
            .Reverse()
            .FirstOrDefault(element =>
                element.TargetKind is not null &&
                element.TargetId is not null &&
                element.TargetId != Guid.Empty &&
                element.HitTestBounds is DocumentRect bounds &&
                Contains(bounds, point, toleranceMillimeters));

        return hit is null
            ? null
            : new HitTestResult(
                new SelectionTarget(hit.TargetKind!.Value, hit.TargetId!.Value),
                point);
    }

    public HitTestResult? HitTestAndSelect(
        IEnumerable<SceneElement> elements,
        DocumentPoint point,
        SelectionService selectionService,
        double toleranceMillimeters = 0)
    {
        ArgumentNullException.ThrowIfNull(selectionService);

        HitTestResult? result = HitTest(elements, point, toleranceMillimeters);
        if (result is not null)
        {
            selectionService.Select(result.Target);
        }

        return result;
    }

    private static bool Contains(
        DocumentRect bounds,
        DocumentPoint point,
        double toleranceMillimeters)
    {
        return point.XMillimeters >= bounds.XMillimeters - toleranceMillimeters &&
               point.XMillimeters <= bounds.XMillimeters + bounds.WidthMillimeters + toleranceMillimeters &&
               point.YMillimeters >= bounds.YMillimeters - toleranceMillimeters &&
               point.YMillimeters <= bounds.YMillimeters + bounds.HeightMillimeters + toleranceMillimeters;
    }
}
