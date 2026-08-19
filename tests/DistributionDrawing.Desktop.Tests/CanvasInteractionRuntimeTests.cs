using System.Windows;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Canvas;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CanvasInteractionRuntimeTests
{
    [Fact]
    public void MouseWheelZoom_ChangesOnlyViewTransform()
    {
        var viewport = new CanvasViewportController();
        double initialScale = viewport.Transform.Scale;
        Point anchor = new(100, 100);
        DocumentPoint documentAtAnchor = viewport.Transform.ViewToDocument(anchor);

        viewport.ZoomFromWheel(anchor, 120);

        Assert.True(viewport.Transform.Scale > initialScale);
        Assert.Equal(
            documentAtAnchor,
            viewport.Transform.ViewToDocument(anchor));
        Assert.NotEqual(new Vector(), viewport.Transform.Translation);
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
        RunOnSta(() =>
        {
            var host = new DrawingVisualHost();

            Assert.False(host.ShowGrid);
            host.ShowGrid = true;
            Assert.True(host.ShowGrid);
            host.ShowGrid = false;
            Assert.False(host.ShowGrid);
        });
    }

    [Fact]
    public void ViewTransform_IsNotExposedByDomainRuntimeLayoutOrPersistenceContracts()
    {
        AssertDoesNotExposeViewTransform(typeof(DrawingDocument));
        AssertDoesNotExposeViewTransform(typeof(RuntimeLayoutDocument));
        AssertDoesNotExposeViewTransform(typeof(ProjectLayoutDto));
        AssertDoesNotExposeViewTransform(typeof(ProjectLayoutSnapshot));
    }

    private static void AssertDoesNotExposeViewTransform(Type contractType)
    {
        Assert.DoesNotContain(
            contractType.GetProperties(),
            property => property.PropertyType == typeof(CanvasViewTransform));
        Assert.DoesNotContain(
            contractType.GetConstructors().SelectMany(constructor =>
                constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(CanvasViewTransform));
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
