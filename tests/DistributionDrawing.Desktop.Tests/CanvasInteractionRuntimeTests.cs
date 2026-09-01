using System.Windows;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Desktop.Clipboard;
using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Canvas;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CanvasInteractionRuntimeTests
{
    [Fact]
    public void PasteTarget_MouseInsideCanvasResolvesCurrentWorldPoint()
    {
        var transform = new CanvasViewTransform();
        var viewPoint = new Point(240, 160);

        bool resolved = CanvasPasteTargetResolver.TryResolve(
            isMouseOverCanvas: true,
            viewPoint,
            new Size(800, 600),
            transform,
            out DocumentPoint worldPoint);

        Assert.True(resolved);
        Assert.Equal(transform.ViewToDocument(viewPoint), worldPoint);
    }

    [Fact]
    public void PasteTarget_UsesZoomAndPanWhenResolvingWorldPoint()
    {
        var transform = new CanvasViewTransform();
        transform.Restore(2.5, 80, -35);
        var viewPoint = new Point(420, 275);

        bool resolved = CanvasPasteTargetResolver.TryResolve(
            isMouseOverCanvas: true,
            viewPoint,
            new Size(900, 700),
            transform,
            out DocumentPoint worldPoint);

        Assert.True(resolved);
        Assert.Equal(transform.ViewToDocument(viewPoint), worldPoint);
        Point roundTripped = transform.DocumentToView(worldPoint);
        Assert.Equal(viewPoint.X, roundTripped.X, 10);
        Assert.Equal(viewPoint.Y, roundTripped.Y, 10);
    }

    [Theory]
    [InlineData(false, 200, 150)]
    [InlineData(true, -1, 150)]
    [InlineData(true, 801, 150)]
    [InlineData(true, 200, -1)]
    [InlineData(true, 200, 601)]
    public void PasteTarget_MouseOutsideCanvasUsesExistingPasteFallback(
        bool isMouseOverCanvas,
        double x,
        double y)
    {
        bool resolved = CanvasPasteTargetResolver.TryResolve(
            isMouseOverCanvas,
            new Point(x, y),
            new Size(800, 600),
            new CanvasViewTransform(),
            out _);

        Assert.False(resolved);
    }

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

    [Fact]
    public void RenderingPrimitivesAndMetrics_AreNotPersistenceOrDomainContracts()
    {
        Type[] renderingTypes =
        [
            typeof(DrawingMetrics),
            typeof(SceneStrokeStyle),
            typeof(SceneEllipse),
            typeof(ScenePolyline)
        ];
        Type[] persistenceTypes = typeof(ProjectLayoutDto).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ProjectLayoutDto).Namespace)
            .ToArray();

        AssertContractsDoNotExpose(
            typeof(DrawingDocument).Assembly.GetTypes(),
            renderingTypes);
        AssertContractsDoNotExpose(
            [typeof(RuntimeLayoutDocument)],
            renderingTypes);
        AssertContractsDoNotExpose(persistenceTypes, renderingTypes);
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

    private static void AssertContractsDoNotExpose(
        IEnumerable<Type> contractTypes,
        IReadOnlyCollection<Type> forbiddenTypes)
    {
        Assert.DoesNotContain(
            contractTypes.SelectMany(type => type.GetProperties()),
            property => ContainsAny(property.PropertyType, forbiddenTypes));
        Assert.DoesNotContain(
            contractTypes.SelectMany(type => type.GetConstructors())
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => ContainsAny(parameter.ParameterType, forbiddenTypes));
    }

    private static bool ContainsAny(
        Type contractType,
        IReadOnlyCollection<Type> forbiddenTypes)
    {
        if (forbiddenTypes.Contains(contractType))
        {
            return true;
        }

        return contractType.IsArray
            ? ContainsAny(contractType.GetElementType()!, forbiddenTypes)
            : contractType.IsGenericType && contractType.GetGenericArguments()
                .Any(argument => ContainsAny(argument, forbiddenTypes));
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
