using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class FrameSymbolDefinition : ISymbolDefinition
{
    public FrameSymbolDefinition(SymbolKind kind)
    {
        if (kind is not SymbolKind.RingCabinet and not SymbolKind.RingCabinetInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
    }

    public SymbolKind Kind { get; }

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
                context.Fill)
        };

        if (context.IncludeLabel && context.Label is not null)
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
