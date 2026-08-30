using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingSceneBitmapRendererTests
{
    [Fact]
    public void RenderPng_UsesSceneBoundsMarginAnd300Dpi()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneRectangle(new DocumentRect(10, 20, 25.4, 25.4), Colors.Black, 0.5)
            ]);
            using var stream = new MemoryStream();

            DrawingSceneBitmapResult result = new DrawingSceneBitmapRenderer().RenderPng(scene, stream);

            Assert.Equal(300, result.Dpi);
            Assert.Equal(10, result.ContentBounds.XMillimeters - result.ExportBounds.XMillimeters, 6);
            Assert.Equal(543, result.WidthPixels);
            Assert.Equal(543, result.HeightPixels);
            Assert.True(stream.Length > 0);
        });
    }

    [Fact]
    public void RenderPng_ProducesWhiteBackgroundAndAllSupportedPrimitives()
    {
        RunOnSta(() =>
        {
            IReadOnlyList<SceneElement> professionalSymbol = new SymbolLibrary().Create(
                SymbolKind.CircuitBreaker,
                new SymbolRenderContext(
                    new DocumentPoint(48, 3),
                    10,
                    16,
                    state: SymbolVisualState.Open,
                    includeLabel: false));
            var scene = new DrawingScene(
            [
                new SceneLine(new DocumentPoint(0, 0), new DocumentPoint(20, 0), Colors.Black, 0.8),
                new SceneLine(new DocumentPoint(0, 3), new DocumentPoint(20, 3), Colors.Black, 0.8, SceneStrokeStyle.Dashed),
                new ScenePolyline(
                    [new DocumentPoint(2, 8), new DocumentPoint(7, 15), new DocumentPoint(12, 8)],
                    true,
                    Colors.Black,
                    0.8,
                    Colors.White),
                new SceneArc(new DocumentPoint(18, 12), 4, 0, 180, Colors.Black, 0.8),
                new SceneRectangle(new DocumentRect(24, 4, 8, 8), Colors.Black, 0.8),
                new SceneEllipse(new DocumentRect(36, 4, 8, 8), Colors.Black, 0.8, Colors.White),
                new SceneText(new DocumentPoint(0, 20), "专业图元", Colors.Black, 4),
                .. professionalSymbol
            ]);
            using var stream = new MemoryStream();

            new DrawingSceneBitmapRenderer().RenderPng(scene, stream);
            stream.Position = 0;
            BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var pixel = new byte[4];
            frame.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);

            Assert.Equal(255, pixel[0]);
            Assert.Equal(255, pixel[1]);
            Assert.Equal(255, pixel[2]);
            Assert.Equal(255, pixel[3]);
        });
    }

    [Fact]
    public void RenderPng_IsViewportIndependentBecauseOnlySceneIsInput()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneLine(new DocumentPoint(10, 10), new DocumentPoint(50, 10), Colors.Black, 1)
            ]);
            var renderer = new DrawingSceneBitmapRenderer();
            using var first = new MemoryStream();
            using var second = new MemoryStream();

            DrawingSceneBitmapResult firstResult = renderer.RenderPng(scene, first);
            DrawingSceneBitmapResult secondResult = renderer.RenderPng(scene, second);

            Assert.Equal(firstResult, secondResult);
            Assert.Equal(first.ToArray(), second.ToArray());
        });
    }

    [Fact]
    public void RenderPng_RejectsEmptySceneAndOversizedBitmap()
    {
        RunOnSta(() =>
        {
            var renderer = new DrawingSceneBitmapRenderer();
            using var output = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = renderer.RenderPng(new DrawingScene([]), output);
            });

            var oversized = new DrawingScene(
            [
                new SceneLine(new DocumentPoint(0, 0), new DocumentPoint(1000, 0), Colors.Black, 1)
            ]);
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = renderer.RenderPng(
                    oversized,
                    output,
                    new DrawingSceneBitmapOptions(MaximumDimensionPixels: 100));
            });
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
