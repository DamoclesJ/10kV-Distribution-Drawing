using System.Windows;
using DistributionDrawing.Rendering.Wpf.Canvas;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Viewport;

public sealed class CanvasViewportController
{
    private const double ZoomFactor = 1.2;
    private const double FitMarginDip = 24;

    private Point? _lastPanPoint;
    private Size _viewportSize;

    public CanvasViewTransform Transform { get; } = new();

    public bool IsPanning => _lastPanPoint is not null;

    public event EventHandler? ViewChanged;

    public void SetViewportSize(Size size)
    {
        if (IsPositiveFinite(size.Width) && IsPositiveFinite(size.Height))
        {
            _viewportSize = size;
        }
    }

    public void ZoomIn() => ZoomAt(ViewportCenter(), Transform.Scale * ZoomFactor);

    public void ZoomOut() => ZoomAt(ViewportCenter(), Transform.Scale / ZoomFactor);

    public void ZoomFromWheel(Point anchorDip, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return;
        }

        double factor = wheelDelta > 0 ? ZoomFactor : 1 / ZoomFactor;
        ZoomAt(anchorDip, Transform.Scale * factor);
    }

    public void BeginPan(Point point)
    {
        EnsureFinite(point);
        _lastPanPoint = point;
    }

    public void UpdatePan(Point point)
    {
        EnsureFinite(point);
        if (_lastPanPoint is not Point previous)
        {
            return;
        }

        Vector delta = point - previous;
        _lastPanPoint = point;
        if (delta.X == 0 && delta.Y == 0)
        {
            return;
        }

        Transform.Pan(delta);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndPan()
    {
        _lastPanPoint = null;
    }

    public void CancelPan()
    {
        _lastPanPoint = null;
    }

    public void Fit(DrawingScene? scene)
    {
        if (scene is null ||
            !DrawingSceneBoundsCalculator.TryCalculate(scene, out DocumentRect bounds))
        {
            Reset();
            return;
        }

        if (Transform.Fit(bounds, _viewportSize, FitMarginDip))
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Reset()
    {
        _lastPanPoint = null;
        Transform.Reset();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public DocumentViewState CaptureState() => new(
        Transform.Scale,
        Transform.Translation.X,
        Transform.Translation.Y);

    public void RestoreState(DocumentViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _lastPanPoint = null;
        Transform.Restore(state.Zoom, state.PanX, state.PanY);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ZoomAt(Point anchorDip, double scale)
    {
        Transform.ZoomAt(anchorDip, scale);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private Point ViewportCenter()
    {
        return new Point(_viewportSize.Width / 2, _viewportSize.Height / 2);
    }

    private static void EnsureFinite(Point point)
    {
        if (double.IsNaN(point.X) || double.IsInfinity(point.X) ||
            double.IsNaN(point.Y) || double.IsInfinity(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
    }

    private static bool IsPositiveFinite(double value) =>
        value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
}
