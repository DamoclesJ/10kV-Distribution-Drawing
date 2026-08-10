using System.Windows;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public readonly record struct DocumentPoint(double XMillimeters, double YMillimeters);

public readonly record struct DocumentRect(
    double XMillimeters,
    double YMillimeters,
    double WidthMillimeters,
    double HeightMillimeters);

public sealed class DocumentCoordinateSystem
{
    private const double MillimetersPerInch = 25.4;
    private const double DipsPerInch = 96.0;

    public double MillimetersToDip(double millimeters)
    {
        return millimeters / MillimetersPerInch * DipsPerInch;
    }

    public Point ToPoint(DocumentPoint point)
    {
        return new Point(
            MillimetersToDip(point.XMillimeters),
            MillimetersToDip(point.YMillimeters));
    }

    public Rect ToRect(DocumentRect rect)
    {
        return new Rect(
            MillimetersToDip(rect.XMillimeters),
            MillimetersToDip(rect.YMillimeters),
            MillimetersToDip(rect.WidthMillimeters),
            MillimetersToDip(rect.HeightMillimeters));
    }
}
