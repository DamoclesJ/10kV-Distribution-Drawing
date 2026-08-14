using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class JointSymbolDefinition : ISymbolDefinition
{
    public SymbolKind Kind => SymbolKind.Joint;

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var elements = new List<SceneElement>
        {
            new SceneRectangle(
                new DocumentRect(
                    context.Origin.XMillimeters - context.WidthMillimeters / 2,
                    context.Origin.YMillimeters - context.HeightMillimeters / 2,
                    context.WidthMillimeters,
                    context.HeightMillimeters),
                context.Stroke,
                context.ThicknessMillimeters,
                context.Fill)
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
