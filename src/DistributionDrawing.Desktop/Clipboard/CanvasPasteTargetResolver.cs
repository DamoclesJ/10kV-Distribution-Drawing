using System.Windows;
using DistributionDrawing.Rendering.Wpf.Canvas;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Clipboard;

internal static class CanvasPasteTargetResolver
{
    public static bool TryResolve(
        bool isMouseOverCanvas,
        Point viewPoint,
        Size canvasSize,
        CanvasViewTransform transform,
        out DocumentPoint worldPoint)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (!isMouseOverCanvas ||
            !IsFinite(viewPoint.X) ||
            !IsFinite(viewPoint.Y) ||
            !IsPositiveFinite(canvasSize.Width) ||
            !IsPositiveFinite(canvasSize.Height) ||
            viewPoint.X < 0 ||
            viewPoint.Y < 0 ||
            viewPoint.X > canvasSize.Width ||
            viewPoint.Y > canvasSize.Height)
        {
            worldPoint = default;
            return false;
        }

        worldPoint = transform.ViewToDocument(viewPoint);
        return IsFinite(worldPoint.XMillimeters) &&
               IsFinite(worldPoint.YMillimeters);
    }

    private static bool IsPositiveFinite(double value) =>
        value > 0 && IsFinite(value);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
