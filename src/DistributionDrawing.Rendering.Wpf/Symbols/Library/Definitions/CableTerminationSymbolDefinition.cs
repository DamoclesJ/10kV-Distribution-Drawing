using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class CableTerminationSymbolDefinition : ISymbolDefinition
{
    private readonly DrawingMetrics _metrics;

    public CableTerminationSymbolDefinition(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public SymbolKind Kind => SymbolKind.CableTermination;

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        double centerX = context.Origin.XMillimeters + context.WidthMillimeters / 2;
        double centerY = context.Origin.YMillimeters + context.HeightMillimeters / 2;
        double width = Math.Min(_metrics.CableTermination.TriangleWidth, context.WidthMillimeters);
        double height = Math.Min(_metrics.CableTermination.TriangleHeight, context.HeightMillimeters);
        DocumentPoint apex = new(centerX, centerY - height / 2);
        DocumentPoint left = new(centerX - width / 2, centerY + height / 2);
        DocumentPoint right = new(centerX + width / 2, centerY + height / 2);
        DocumentRect logicalBounds = new(
            centerX - width / 2 - _metrics.CableTermination.LogicalHitPadding,
            centerY - height / 2 - _metrics.CableTermination.LogicalHitPadding,
            width + _metrics.CableTermination.LogicalHitPadding * 2,
            height + _metrics.CableTermination.LogicalHitPadding * 2);
        var elements = new List<SceneElement>
        {
            new SceneLogicalBounds(logicalBounds),
            new ScenePolyline(
                [apex, right, left],
                isClosed: true,
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
                    _metrics.General.SmallFontSize));
        }

        return elements;
    }
}
