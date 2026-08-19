using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Canvas;

public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly VisualCollection _visuals;
    private Matrix _viewMatrix = Matrix.Identity;
    private CanvasViewTransform? _viewTransform;
    private bool _showGrid;

    public DrawingVisualHost()
    {
        ClipToBounds = true;
        _visuals = new VisualCollection(this);
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (_showGrid == value)
            {
                return;
            }

            _showGrid = value;
            InvalidateVisual();
        }
    }

    public void Show(DrawingVisual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        visual.Transform = new MatrixTransform(_viewMatrix);
        _visuals.Clear();
        _visuals.Add(visual);
    }

    public void SetViewTransform(CanvasViewTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        _viewTransform = transform;
        _viewMatrix = transform.Matrix;
        if (_visuals.Count == 1 && _visuals[0] is DrawingVisual visual)
        {
            visual.Transform = new MatrixTransform(_viewMatrix);
        }

        InvalidateVisual();
    }

    public void Clear()
    {
        _visuals.Clear();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (ShowGrid && _viewTransform is not null)
        {
            DrawGrid(drawingContext, _viewTransform);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        var clip = new RectangleGeometry(new Rect(RenderSize));
        clip.Freeze();
        Clip = clip;
    }

    private void DrawGrid(DrawingContext drawingContext, CanvasViewTransform transform)
    {
        DocumentPoint topLeft = transform.ViewToDocument(new Point(0, 0));
        DocumentPoint bottomRight = transform.ViewToDocument(
            new Point(RenderSize.Width, RenderSize.Height));
        const double spacingMillimeters = 100;
        int firstX = (int)Math.Floor(Math.Min(topLeft.XMillimeters, bottomRight.XMillimeters) / spacingMillimeters) - 1;
        int lastX = (int)Math.Ceiling(Math.Max(topLeft.XMillimeters, bottomRight.XMillimeters) / spacingMillimeters) + 1;
        int firstY = (int)Math.Floor(Math.Min(topLeft.YMillimeters, bottomRight.YMillimeters) / spacingMillimeters) - 1;
        int lastY = (int)Math.Ceiling(Math.Max(topLeft.YMillimeters, bottomRight.YMillimeters) / spacingMillimeters) + 1;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(35, 80, 80, 80)), 0.5);
        pen.Freeze();

        for (int index = firstX; index <= lastX; index++)
        {
            double coordinate = index * spacingMillimeters;
            drawingContext.DrawLine(
                pen,
                transform.DocumentToView(new DocumentPoint(coordinate, topLeft.YMillimeters)),
                transform.DocumentToView(new DocumentPoint(coordinate, bottomRight.YMillimeters)));
        }

        for (int index = firstY; index <= lastY; index++)
        {
            double coordinate = index * spacingMillimeters;
            drawingContext.DrawLine(
                pen,
                transform.DocumentToView(new DocumentPoint(topLeft.XMillimeters, coordinate)),
                transform.DocumentToView(new DocumentPoint(bottomRight.XMillimeters, coordinate)));
        }
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }
}
