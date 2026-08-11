using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record OverheadLineSegment(
    Guid ConnectionId,
    DocumentPoint Start,
    DocumentPoint End,
    Color Stroke,
    double ThicknessMillimeters,
    bool IsContinued = false,
    DocumentPoint? ContinuationOffset = null)
{
    public static OverheadLineSegment From(
        OverheadLine overheadLine,
        OverheadLineLayout layout)
    {
        ArgumentNullException.ThrowIfNull(overheadLine);
        ArgumentNullException.ThrowIfNull(layout);

        if (overheadLine.ConnectionId != layout.ConnectionId)
        {
            throw new InvalidOperationException(
                "Overhead line and overhead line layout IDs must match.");
        }

        return new OverheadLineSegment(
            overheadLine.ConnectionId,
            layout.Start,
            layout.End,
            Colors.Black,
            0.8,
            layout.IsContinued,
            layout.ContinuationOffset);
    }

    public IReadOnlyList<SceneElement> CreateElements()
    {
        var elements = new List<SceneElement>
        {
            new SceneLine(Start, End, Stroke, ThicknessMillimeters)
        };

        if (IsContinued)
        {
            DocumentPoint offset = ContinuationOffset ?? new DocumentPoint(4, 0);
            elements.Add(
                new SceneLine(
                    new DocumentPoint(End.XMillimeters, End.YMillimeters),
                    new DocumentPoint(
                        End.XMillimeters + offset.XMillimeters,
                        End.YMillimeters + offset.YMillimeters),
                    Stroke,
                    ThicknessMillimeters));
        }

        return elements;
    }
}
