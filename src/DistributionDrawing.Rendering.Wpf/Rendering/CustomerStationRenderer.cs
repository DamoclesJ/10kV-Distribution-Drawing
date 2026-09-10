using System.Windows.Media;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class CustomerStationRenderer
{
    private readonly DrawingMetrics _metrics;

    public CustomerStationRenderer(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public IReadOnlyList<SceneElement> Render(
        CustomerStation station,
        CustomerStationLayout layout)
    {
        CustomerStationProfessionalGeometry geometry =
            CustomerStationProfessionalGeometry.Create(
                station,
                layout,
                _metrics.CustomerStation);
        var elements = new List<SceneElement>();
        foreach (CustomerStationUnitGeometry unit in geometry.Units)
        {
            elements.Add(new SceneRectangle(
                unit.Body,
                Colors.Black,
                _metrics.General.StandardStrokeThickness));
            elements.Add(new ScenePolyline(
                unit.Triangle,
                true,
                Colors.Black,
                _metrics.General.StandardStrokeThickness));
            elements.Add(new SceneText(
                unit.LabelOrigin,
                unit.DisplayName,
                Colors.Black,
                _metrics.General.StandardFontSize,
                SceneTextHorizontalAlignment.Center));
        }

        if (geometry.Roof.Count > 0)
        {
            elements.Add(new ScenePolyline(
                geometry.Roof,
                false,
                Colors.Black,
                _metrics.General.StandardStrokeThickness));
        }

        foreach (CustomerStationSwitchGeometry switchGeometry in geometry.Switches)
        {
            double radius = _metrics.CustomerStation.ContactRadius;
            elements.Add(Contact(switchGeometry.CableContact, radius));
            elements.Add(Contact(switchGeometry.StationContact, radius));
            elements.Add(new SceneLine(
                switchGeometry.CableContact,
                switchGeometry.BladeEnd,
                Colors.Black,
                _metrics.General.StandardStrokeThickness));
        }

        return elements;
    }

    private SceneEllipse Contact(DocumentPoint center, double radius) => new(
        new DocumentRect(
            center.XMillimeters - radius,
            center.YMillimeters - radius,
            radius * 2,
            radius * 2),
        Colors.Black,
        _metrics.General.StandardStrokeThickness,
        Colors.White);
}
