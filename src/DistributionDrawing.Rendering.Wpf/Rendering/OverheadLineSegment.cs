using System.Windows.Media;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

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

    public IReadOnlyList<SceneElement> CreateElements(SymbolLibrary? library = null)
    {
        return (library ?? new SymbolLibrary()).CreateOverheadLineSegment(this);
    }
}
