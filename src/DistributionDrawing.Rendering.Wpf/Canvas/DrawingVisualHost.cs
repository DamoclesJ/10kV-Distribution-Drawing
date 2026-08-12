using System.Windows;
using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Canvas;

public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly VisualCollection _visuals;
    private Matrix _viewMatrix = Matrix.Identity;

    public DrawingVisualHost()
    {
        _visuals = new VisualCollection(this);
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
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }
}
