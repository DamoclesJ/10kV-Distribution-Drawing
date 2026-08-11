using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class PoleSymbolDefinition : ISymbolDefinition
{
    public SymbolKind Kind => SymbolKind.Pole;

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        double centerX = context.Origin.XMillimeters + context.WidthMillimeters / 2;
        double topY = context.Origin.YMillimeters;
        double bottomY = topY + context.HeightMillimeters;

        var elements = new List<SceneElement>
        {
            new SceneLine(
                new DocumentPoint(centerX, topY),
                new DocumentPoint(centerX, bottomY),
                context.Stroke,
                context.ThicknessMillimeters),
            new SceneLine(
                new DocumentPoint(centerX - 7, topY + 5),
                new DocumentPoint(centerX + 7, topY + 5),
                context.Stroke,
                context.ThicknessMillimeters * 0.7)
        };

        if (context.Label is not null)
        {
            elements.Add(
                new SceneText(
                    context.LabelOrigin,
                    context.Label,
                    context.Stroke,
                    4));
        }

        return elements;
    }
}
