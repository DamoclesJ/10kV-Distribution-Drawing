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

    [Fact]
    public void RenderPng_HandlesNegativeFarAndVerySmallSceneCoordinates()
    {
        RunOnSta(() =>
        {
            var renderer = new DrawingSceneBitmapRenderer();
            var negative = new DrawingScene(
            [
                new SceneRectangle(new DocumentRect(-120, -80, 5, 3), Colors.Black, 0.5)
            ]);
            var farPositive = new DrawingScene(
            [
                new SceneRectangle(new DocumentRect(1_000_000, 2_000_000, 5, 3), Colors.Black, 0.5)
            ]);
            var tiny = new DrawingScene(
            [
                new SceneLine(
                    new DocumentPoint(0, 0),
                    new DocumentPoint(0.001, 0),
                    Colors.Black,
                    0.001)
            ]);
            using var negativeOutput = new MemoryStream();
            using var farOutput = new MemoryStream();
            using var tinyOutput = new MemoryStream();

            DrawingSceneBitmapResult negativeResult = renderer.RenderPng(negative, negativeOutput);
            DrawingSceneBitmapResult farResult = renderer.RenderPng(farPositive, farOutput);
            DrawingSceneBitmapResult tinyResult = renderer.RenderPng(tiny, tinyOutput);

            Assert.Equal(negativeResult.WidthPixels, farResult.WidthPixels);
            Assert.Equal(negativeResult.HeightPixels, farResult.HeightPixels);
            Assert.True(tinyResult.WidthPixels > 0);
            Assert.True(tinyResult.HeightPixels > 0);
        });
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RenderPng_RejectsNonFiniteSceneCoordinates(double coordinate)
    {
        var scene = new DrawingScene(
        [
            new SceneLine(
                new DocumentPoint(coordinate, 0),
                new DocumentPoint(10, 0),
                Colors.Black,
                1)
        ]);
        using var output = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new DrawingSceneBitmapRenderer().RenderPng(scene, output);
        });
    }

    [Fact]
    public void RenderPng_ContentBoundsIncludeTextAndThickStrokeEdges()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneLine(
                    new DocumentPoint(0, 0),
                    new DocumentPoint(1, 0),
                    Colors.Black,
                    10),
                new SceneText(new DocumentPoint(10, 0), "边界文字", Colors.Black, 4)
            ]);
            using var output = new MemoryStream();

            DrawingSceneBitmapResult result = new DrawingSceneBitmapRenderer().RenderPng(scene, output);

            Assert.True(result.ContentBounds.XMillimeters <= -5);
            Assert.True(
                result.ContentBounds.XMillimeters + result.ContentBounds.WidthMillimeters >= 26);
        });
    }

    [Fact]
    public void RenderPng_RejectsExtremePixelBudgetWithoutOverflow()
    {
        var scene = new DrawingScene(
        [
            new SceneRectangle(
                new DocumentRect(0, 0, 169_000_000, 169_000_000),
                Colors.Black,
                1)
        ]);
        using var output = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new DrawingSceneBitmapRenderer().RenderPng(
                scene,
                output,
                new DrawingSceneBitmapOptions(
                    Dpi: 300,
                    MarginMillimeters: 0,
                    MaximumDimensionPixels: int.MaxValue,
                    MaximumPixelCount: long.MaxValue,
                    MaximumEstimatedBytes: long.MaxValue));
        });
    }

    [Fact]
    public void RenderPng_PropagatesOutputStreamWriteFailure()
    {
        RunOnSta(() =>
        {
            var scene = new DrawingScene(
            [
                new SceneRectangle(new DocumentRect(0, 0, 10, 10), Colors.Black, 1)
            ]);
            using var output = new ThrowingWriteStream();

            Assert.Throws<IOException>(() =>
            {
                _ = new DrawingSceneBitmapRenderer().RenderPng(scene, output);
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

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("Simulated write failure.");

        public override void Write(ReadOnlySpan<byte> buffer) =>
            throw new IOException("Simulated write failure.");
    }
}
