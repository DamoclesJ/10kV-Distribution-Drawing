using System.Windows;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Rendering.Wpf.Canvas;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CanvasInteractionRuntimeTests
{
    [Fact]
    public void MouseWheelZoom_ChangesOnlyViewTransform()
    {
        var viewport = new CanvasViewportController();
        double initialScale = viewport.Transform.Scale;

        viewport.ZoomFromWheel(new Point(100, 100), 120);

        Assert.True(viewport.Transform.Scale > initialScale);
        Assert.Equal(new Vector(), viewport.Transform.Translation);
    }

    [Fact]
    public void MiddlePan_ChangesOnlyViewTranslation()
    {
        var viewport = new CanvasViewportController();

        viewport.BeginPan(new Point(10, 20));
        viewport.UpdatePan(new Point(35, 50));
        viewport.EndPan();

        Assert.Equal(new Vector(25, 30), viewport.Transform.Translation);
        Assert.False(viewport.IsPanning);
    }

    [Fact]
    public void GridVisibility_IsCanvasDisplayState()
    {
        var host = new DrawingVisualHost();

        Assert.False(host.ShowGrid);
        host.ShowGrid = true;
        Assert.True(host.ShowGrid);
        host.ShowGrid = false;
        Assert.False(host.ShowGrid);
    }
}
