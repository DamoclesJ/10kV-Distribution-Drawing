using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;

public sealed class SwitchSymbolDefinition : ISymbolDefinition
{
    public SwitchSymbolDefinition(SymbolKind kind)
    {
        if (kind is not SymbolKind.CircuitBreaker and
            not SymbolKind.LoadSwitch and
            not SymbolKind.IsolationSwitch and
            not SymbolKind.GroundSwitch and
            not SymbolKind.DropoutFuse)
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

        if (context.State == SymbolVisualState.Open)
        {
            elements.Add(
                new SceneLine(
                    new DocumentPoint(
                        context.Origin.XMillimeters + 3,
                        context.Origin.YMillimeters + context.HeightMillimeters - 3),
                    new DocumentPoint(
                        context.Origin.XMillimeters + context.WidthMillimeters - 3,
                        context.Origin.YMillimeters + 3),
                    context.Stroke,
                    context.ThicknessMillimeters * 0.75));
        }
        else if (context.State == SymbolVisualState.Closed)
        {
            double centerY = context.Origin.YMillimeters + context.HeightMillimeters / 2;
            elements.Add(
                new SceneLine(
                    new DocumentPoint(
                        context.Origin.XMillimeters + 3,
                        centerY),
                    new DocumentPoint(
                        context.Origin.XMillimeters + context.WidthMillimeters - 3,
                        centerY),
                    context.Stroke,
                    context.ThicknessMillimeters * 0.75));
        }

        if (context.Label is not null)
        {
            elements.Add(
                new SceneText(
                    context.LabelOrigin,
                    context.Label,
                    context.Stroke,
                    3.5));
        }

        if (context.State is SymbolVisualState.Open or SymbolVisualState.Closed)
        {
            elements.Add(
                new SceneText(
                    new DocumentPoint(
                        context.Origin.XMillimeters + context.WidthMillimeters + 2,
                        context.Origin.YMillimeters + context.HeightMillimeters / 2),
                    context.State == SymbolVisualState.Closed ? "合" : "分",
                    context.Stroke,
                    3.5));
        }

        return elements;
    }
}
