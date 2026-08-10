using System.Windows;
using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Canvas;

public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly VisualCollection _visuals;

    public DrawingVisualHost()
    {
        _visuals = new VisualCollection(this);
    }

    public void Show(DrawingVisual visual)
    {
        _visuals.Clear();
        _visuals.Add(visual);
    }

    public void Clear()
    {
        _visuals.Clear();
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }
}
