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

        if (!transformer.IsLegacyNamingIncomplete &&
            !string.IsNullOrWhiteSpace(transformer.DisplayName))
        {
            elements.Add(CreateNameLabel(transformer, geometry));
        }

        return elements;
    }

    private SceneText CreateNameLabel(
        Transformer transformer,
        TransformerProfessionalGeometry geometry)
    {
        double fontSize = _metrics.Typography.TransformerNameFontSize;
        double gap = _metrics.Transformer.NameLabelGap;
        DocumentRect bounds = geometry.Bounds;
        var origin = new DocumentPoint(
            bounds.XMillimeters + bounds.WidthMillimeters / 2,
            bounds.YMillimeters + bounds.HeightMillimeters + gap);
        const SceneTextHorizontalAlignment alignment =
            SceneTextHorizontalAlignment.Center;
        DocumentRect hitBounds = EstimateTextBounds(
            origin,
            transformer.DisplayName!,
            fontSize,
            alignment);
        return new SceneText(
            origin,
            transformer.DisplayName!,
            Colors.Black,
            fontSize,
            alignment)
        {
            HitTestBounds = hitBounds
        };
    }

    private static DocumentRect EstimateTextBounds(
        DocumentPoint origin,
        string text,
        double fontSize,
        SceneTextHorizontalAlignment alignment)
    {
        double width = Math.Max(fontSize, text.Length * fontSize);
        double height = Math.Max(1, fontSize * 1.3);
        double x = alignment == SceneTextHorizontalAlignment.Center
            ? origin.XMillimeters - width / 2
            : origin.XMillimeters;
        return new DocumentRect(x, origin.YMillimeters, width, height);
    }
}
