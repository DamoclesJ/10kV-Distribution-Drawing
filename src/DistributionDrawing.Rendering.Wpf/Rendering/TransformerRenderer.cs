using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class TransformerRenderer
{
    private readonly DrawingMetrics _metrics;

    public TransformerRenderer(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public IReadOnlyList<SceneElement> Render(Transformer transformer, TransformerLayout layout)
    {
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            transformer,
            layout,
            _metrics.Transformer);
        var elements = geometry.Circles
            .Select(bounds => (SceneElement)new SceneEllipse(
                bounds,
                Colors.Black,
                _metrics.General.StandardStrokeThickness))
            .ToList();
        elements.AddRange(geometry.Lines.Select(line => (SceneElement)new SceneLine(
            line.Start,
            line.End,
            Colors.Black,
            _metrics.General.StandardStrokeThickness)));
        if (geometry.Polygon.Count > 0)
        {
            elements.Add(new ScenePolyline(
                geometry.Polygon,
                isClosed: true,
                Colors.Black,
                _metrics.General.StandardStrokeThickness));
        }

        return elements;
    }
}
