using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class PoleSymbolDefinition : ISymbolDefinition
{
    private readonly DrawingMetrics _metrics;

    public PoleSymbolDefinition(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public SymbolKind Kind => SymbolKind.Pole;

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        double diameter = _metrics.Pole.PoleRadius * 2;
        DocumentRect bounds = new(
            context.Origin.XMillimeters,
            context.Origin.YMillimeters,
            diameter,
            diameter);

        var elements = new List<SceneElement>
        {
            new SceneLogicalBounds(bounds),
            new SceneEllipse(
                bounds,
                context.Stroke,
                context.ThicknessMillimeters,
                context.Fill)
        };

        if (context.IncludeLabel && context.Label is not null)
        {
            elements.Add(
                new SceneText(
                    context.LabelOrigin,
                    context.Label,
                    context.Stroke,
                    _metrics.General.StandardFontSize));
        }

        return elements;
    }
}
