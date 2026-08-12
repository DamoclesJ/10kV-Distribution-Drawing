using System.Windows;
using System.Windows.Media;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Canvas;

public sealed class CanvasViewTransform
{
    public const double MinimumScale = 0.2;
    public const double MaximumScale = 8.0;

    private readonly DocumentCoordinateSystem _coordinates = new();

    public double Scale { get; private set; } = 1.0;

    public Vector Translation { get; private set; }

    public Matrix Matrix => new(
        Scale,
        0,
        0,
        Scale,
        Translation.X,
        Translation.Y);

    public Point DocumentToView(DocumentPoint point)
    {
        EnsureFinite(point.XMillimeters, nameof(point));
        EnsureFinite(point.YMillimeters, nameof(point));
        return new Point(
            _coordinates.MillimetersToDip(point.XMillimeters) * Scale + Translation.X,
            _coordinates.MillimetersToDip(point.YMillimeters) * Scale + Translation.Y);
    }

    public DocumentPoint ViewToDocument(Point point)
    {
        EnsureFinite(point.X, nameof(point));
        EnsureFinite(point.Y, nameof(point));
        return new DocumentPoint(
            _coordinates.DipToMillimeters((point.X - Translation.X) / Scale),
            _coordinates.DipToMillimeters((point.Y - Translation.Y) / Scale));
    }

    public double ViewDistanceToDocument(double distanceDip)
    {
        EnsureNonNegativeFinite(distanceDip, nameof(distanceDip));
        return _coordinates.DipToMillimeters(distanceDip / Scale);
    }

    public double DocumentDistanceToView(double distanceMillimeters)
    {
        EnsureNonNegativeFinite(distanceMillimeters, nameof(distanceMillimeters));
        return _coordinates.MillimetersToDip(distanceMillimeters) * Scale;
    }

    public void ZoomAt(Point anchorDip, double targetScale)
    {
        EnsureFinite(anchorDip.X, nameof(anchorDip));
        EnsureFinite(anchorDip.Y, nameof(anchorDip));
        EnsureFinite(targetScale, nameof(targetScale));
        if (targetScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetScale));
        }

        DocumentPoint documentAnchor = ViewToDocument(anchorDip);
        double scale = Math.Clamp(targetScale, MinimumScale, MaximumScale);
        double baseX = _coordinates.MillimetersToDip(documentAnchor.XMillimeters);
        double baseY = _coordinates.MillimetersToDip(documentAnchor.YMillimeters);
        Set(
            scale,
            new Vector(
                anchorDip.X - baseX * scale,
                anchorDip.Y - baseY * scale));
    }

    public void Pan(Vector deltaDip)
    {
        EnsureFinite(deltaDip.X, nameof(deltaDip));
        EnsureFinite(deltaDip.Y, nameof(deltaDip));
        Set(Scale, Translation + deltaDip);
    }

    public bool Fit(DocumentRect documentBounds, Size viewportSize, double marginDip)
    {
        if (!IsPositiveFinite(viewportSize.Width) ||
            !IsPositiveFinite(viewportSize.Height) ||
            !IsNonNegativeFinite(marginDip) ||
            !IsFinite(documentBounds.XMillimeters) ||
            !IsFinite(documentBounds.YMillimeters) ||
            !IsNonNegativeFinite(documentBounds.WidthMillimeters) ||
            !IsNonNegativeFinite(documentBounds.HeightMillimeters))
        {
            return false;
        }

        double availableWidth = viewportSize.Width - marginDip * 2;
        double availableHeight = viewportSize.Height - marginDip * 2;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return false;
        }

        double widthDip = Math.Max(
            _coordinates.MillimetersToDip(documentBounds.WidthMillimeters),
            1.0);
        double heightDip = Math.Max(
            _coordinates.MillimetersToDip(documentBounds.HeightMillimeters),
            1.0);
        double scale = Math.Clamp(
            Math.Min(availableWidth / widthDip, availableHeight / heightDip),
            MinimumScale,
            MaximumScale);
        double centerX = _coordinates.MillimetersToDip(
            documentBounds.XMillimeters + documentBounds.WidthMillimeters / 2);
        double centerY = _coordinates.MillimetersToDip(
            documentBounds.YMillimeters + documentBounds.HeightMillimeters / 2);
        Set(
            scale,
            new Vector(
                viewportSize.Width / 2 - centerX * scale,
                viewportSize.Height / 2 - centerY * scale));
        return true;
    }

    public void Reset()
    {
        Set(1.0, new Vector());
    }

    private void Set(double scale, Vector translation)
    {
        if (!IsPositiveFinite(scale) ||
            scale < MinimumScale ||
            scale > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        EnsureFinite(translation.X, nameof(translation));
        EnsureFinite(translation.Y, nameof(translation));
        Scale = scale;
        Translation = translation;
    }

    private static void EnsureNonNegativeFinite(double value, string parameterName)
    {
        if (!IsNonNegativeFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (!IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static bool IsPositiveFinite(double value) =>
        value > 0 && IsFinite(value);

    private static bool IsNonNegativeFinite(double value) =>
        value >= 0 && IsFinite(value);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
