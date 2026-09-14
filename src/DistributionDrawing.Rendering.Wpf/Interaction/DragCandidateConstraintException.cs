using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Identifies candidate-local geometry that cannot become a valid drag preview.
/// </summary>
public sealed class DragCandidateConstraintException : InvalidOperationException
{
    public DragCandidateConstraintException(string message)
        : base(message)
    {
    }
}

internal static class DragCandidateGuard
{
    public static void EnsureFinite(DocumentPoint point)
    {
        if (!double.IsFinite(point.XMillimeters) ||
            !double.IsFinite(point.YMillimeters))
        {
            throw new DragCandidateConstraintException(
                "Drag candidate coordinates must be finite.");
        }
    }

    public static void EnsureFinite(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new DragCandidateConstraintException(
                "Drag candidate coordinates must be finite.");
        }
    }
}
