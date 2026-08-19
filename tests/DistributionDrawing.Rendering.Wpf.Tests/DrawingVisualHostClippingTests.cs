using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Canvas;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class DrawingVisualHostClippingTests
{
    [Fact]
    public void Arrange_ClipsDrawingVisualHostToCurrentRenderSizeAndUpdatesOnResize()
    {
        RunOnSta(() =>
        {
            var host = new DrawingVisualHost();

            Arrange(host, new Size(640, 480));

            Assert.True(host.ClipToBounds);
            Assert.Equal(
                new Rect(0, 0, 640, 480),
                Assert.IsType<RectangleGeometry>(host.Clip).Rect);

            Arrange(host, new Size(320, 240));

            Assert.Equal(
                new Rect(0, 0, 320, 240),
                Assert.IsType<RectangleGeometry>(host.Clip).Rect);
        });
    }

    [Fact]
    public void ZoomAndPan_DoNotChangeViewportClip()
    {
        RunOnSta(() =>
        {
            var host = new DrawingVisualHost();
            Arrange(host, new Size(500, 360));
            Rect viewport = Assert.IsType<RectangleGeometry>(host.Clip).Rect;
            var transform = new CanvasViewTransform();

            transform.ZoomAt(new Point(120, 80), 2.5);
            host.SetViewTransform(transform);
            transform.Pan(new Vector(-700, 240));
            host.SetViewTransform(transform);

            Assert.Equal(viewport, Assert.IsType<RectangleGeometry>(host.Clip).Rect);
            Assert.Equal(new Rect(0, 0, 500, 360), viewport);
        });
    }

    private static void Arrange(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(new Point(), size));
        element.UpdateLayout();
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
