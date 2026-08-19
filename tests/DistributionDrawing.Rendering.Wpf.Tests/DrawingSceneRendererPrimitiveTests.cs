using System.Runtime.ExceptionServices;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingSceneRendererPrimitiveTests
{
    [Fact]
    public void Renderer_MapsSolidAndDashedStrokeStylesToPens()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneLine(
                    new DocumentPoint(0, 0),
                    new DocumentPoint(10, 0),
                    Colors.Black,
                    0.8),
                new SceneLine(
                    new DocumentPoint(0, 5),
                    new DocumentPoint(10, 5),
                    Colors.Black,
                    0.8,
                    SceneStrokeStyle.Dashed)
            ]);

            DrawingGroup drawing = new DrawingSceneRenderer().Render(scene, 1).Drawing;
            GeometryDrawing[] geometries = drawing.Children.OfType<GeometryDrawing>().ToArray();

            Assert.Equal(2, geometries.Length);
            Assert.Empty(geometries[0].Pen!.DashStyle.Dashes);
            Assert.Equal(
                new double[]
                {
                    DrawingMetrics.Default.Line.CableDashLength / 0.8,
                    DrawingMetrics.Default.Line.CableDashGap / 0.8
                },
                geometries[1].Pen!.DashStyle.Dashes);
        });
    }

    [Fact]
    public void Renderer_CreatesEllipseAndOpenPolylineGeometry()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneEllipse(
                    new DocumentRect(0, 0, 10, 8),
                    Colors.Black,
                    0.8,
                    Colors.White),
                new ScenePolyline(
                    [
                        new DocumentPoint(20, 0),
                        new DocumentPoint(25, 5),
                        new DocumentPoint(30, 0)
                    ],
                    isClosed: false,
                    Colors.Black,
                    0.8)
            ]);

            DrawingGroup drawing = new DrawingSceneRenderer().Render(scene, 1).Drawing;
            GeometryDrawing[] geometries = drawing.Children.OfType<GeometryDrawing>().ToArray();

            Assert.Equal(2, geometries.Length);
            Assert.IsType<EllipseGeometry>(geometries[0].Geometry);
            Assert.IsType<StreamGeometry>(geometries[1].Geometry);
            Assert.NotNull(geometries[0].Brush);
            Assert.Null(geometries[1].Brush);
        });
    }

    [Fact]
    public void Renderer_FillsClosedPolylineGeometry()
    {
        RunOnSta(() =>
        {
            var polygon = new ScenePolyline(
                [
                    new DocumentPoint(0, 0),
                    new DocumentPoint(10, 20),
                    new DocumentPoint(20, 0)
                ],
                isClosed: true,
                Colors.Black,
                0.8,
                Colors.White);

            DrawingGroup drawing = new DrawingSceneRenderer()
                .Render(new DrawingScene([polygon]), 1)
                .Drawing;
            GeometryDrawing geometry = Assert.IsType<GeometryDrawing>(Assert.Single(drawing.Children));

            Assert.NotNull(geometry.Brush);
            Assert.IsType<StreamGeometry>(geometry.Geometry);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
