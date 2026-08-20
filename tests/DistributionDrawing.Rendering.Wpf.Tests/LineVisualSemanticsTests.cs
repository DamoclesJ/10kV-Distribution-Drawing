using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Routing;
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
    public void RoutedCableAndOverheadShareOrthogonalGeometryAndKeepDistinctStyles()
    {
        Guid startId = Guid.NewGuid();
        Guid endId = Guid.NewGuid();
        OrthogonalRoute route = new OrthogonalRouter().Route(
            new ConnectionRouteRequest(
                Guid.NewGuid(),
                ConnectionType.Cable,
                startId,
                endId,
                new TerminalAnchor(startId, new DocumentPoint(0, 0)),
                new TerminalAnchor(endId, new DocumentPoint(50, 30))),
            []);
        var decorator = new LineJumpDecorator();
        SceneLine[] cable = decorator.Project(
                route, [], Colors.Black, SceneStrokeStyle.Dashed)
            .OfType<SceneLine>()
            .ToArray();
        SceneLine[] overhead = decorator.Project(
                route, [], Colors.Black, SceneStrokeStyle.Solid)
            .OfType<SceneLine>()
            .ToArray();

        Assert.Equal(cable.Select(line => (line.Start, line.End)),
            overhead.Select(line => (line.Start, line.End)));
        Assert.All(cable, line => Assert.Equal(SceneStrokeStyle.Dashed, line.StrokeStyle));
        Assert.All(overhead, line => Assert.Equal(SceneStrokeStyle.Solid, line.StrokeStyle));
        Assert.All(cable, line => Assert.True(
            line.Start.XMillimeters == line.End.XMillimeters ||
            line.Start.YMillimeters == line.End.YMillimeters));
    }
}
