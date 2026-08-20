using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class LineVisualSemanticsTests
{
    [Fact]
    public void CableLine_IsDashedWithSharedConnectionThickness()
    {
        SceneLine line = Assert.Single(
            new SymbolLibrary().CreateCableLine(
                new DocumentPoint(0, 0),
                new DocumentPoint(50, 0))
                .OfType<SceneLine>());

        Assert.Equal(SceneStrokeStyle.Dashed, line.StrokeStyle);
        Assert.Equal(DrawingMetrics.Default.Line.ConnectionThickness, line.ThicknessMillimeters);
        Assert.Equal(4, DrawingMetrics.Default.Line.CableDashLength);
        Assert.Equal(3, DrawingMetrics.Default.Line.CableDashGap);
    }

    [Fact]
    public void OverheadLine_IsSolidWithSharedConnectionThickness()
    {
        var segment = new OverheadLineSegment(
            Guid.NewGuid(),
            new DocumentPoint(0, 0),
            new DocumentPoint(50, 0),
            Colors.Black,
            DrawingMetrics.Default.Line.ConnectionThickness);

        SceneLine line = Assert.Single(
            segment.CreateElements().OfType<SceneLine>());

        Assert.Equal(SceneStrokeStyle.Solid, line.StrokeStyle);
        Assert.Equal(DrawingMetrics.Default.Line.ConnectionThickness, line.ThicknessMillimeters);
    }

    [Fact]
    public void CableAndOverheadLineKeepStraightRouteSemantics()
    {
        SceneLine cable = Assert.Single(
            new SymbolLibrary().CreateCableLine(
                new DocumentPoint(0, 0),
                new DocumentPoint(50, 30))
                .OfType<SceneLine>());
        var overhead = new OverheadLineSegment(
            Guid.NewGuid(),
            new DocumentPoint(0, 0),
            new DocumentPoint(50, 30),
            Colors.Black,
            DrawingMetrics.Default.Line.ConnectionThickness);
        SceneLine overheadLine = Assert.Single(overhead.CreateElements().OfType<SceneLine>());

        Assert.Equal(cable.Start, overheadLine.Start);
        Assert.Equal(cable.End, overheadLine.End);
    }
}
