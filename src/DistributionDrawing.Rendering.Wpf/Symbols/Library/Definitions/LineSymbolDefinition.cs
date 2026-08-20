using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class LineSymbolDefinition : ISymbolDefinition
{
    public LineSymbolDefinition(SymbolKind kind)
    {
        if (kind is not SymbolKind.OverheadLine and
            not SymbolKind.CableLine and
            not SymbolKind.GroundingLine)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
    }

    public SymbolKind Kind { get; }

    public IReadOnlyList<SceneElement> Create(SymbolRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.End is not DocumentPoint end)
        {
            throw new ArgumentException(
                "A line symbol requires an end point.",
                nameof(context));
        }

        var elements = new List<SceneElement>
        {
            new SceneLine(
                context.Origin,
                end,
                context.Stroke,
                context.ThicknessMillimeters,
                Kind == SymbolKind.CableLine
                    ? SceneStrokeStyle.Dashed
                    : SceneStrokeStyle.Solid)
        };

        if (context.Label is not null)
        {
            elements.Add(
                new SceneText(
                    new DocumentPoint(
                        (context.Origin.XMillimeters + end.XMillimeters) / 2,
                        (context.Origin.YMillimeters + end.YMillimeters) / 2 - 3),
                    context.Label,
                    context.Stroke,
                    3.5));
        }

        return elements;
    }
}
