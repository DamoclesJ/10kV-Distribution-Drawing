using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class CableTerminationSymbolDefinition : ISymbolDefinition
{
    public SymbolKind Kind => SymbolKind.CableTermination;

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var elements = new List<SceneElement>
        {
            new SceneRectangle(
                new DocumentRect(
                    context.Origin.XMillimeters,
                    context.Origin.YMillimeters,
                    context.WidthMillimeters,
                    context.HeightMillimeters),
                context.Stroke,
                context.ThicknessMillimeters,
                context.Fill),
            new SceneLine(
                new DocumentPoint(
                    context.Origin.XMillimeters + context.WidthMillimeters / 2,
                    context.Origin.YMillimeters),
                new DocumentPoint(
                    context.Origin.XMillimeters + context.WidthMillimeters / 2,
                    context.Origin.YMillimeters - 4),
                context.Stroke,
                context.ThicknessMillimeters)
        };

        if (context.Label is not null)
        {
            elements.Add(
                new SceneText(
                    context.LabelOrigin,
                    context.Label,
                    context.Stroke,
                    3.5));
        }

        return elements;
    }
}
