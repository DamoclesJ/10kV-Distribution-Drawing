using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols.Library;

public interface ISymbolDefinition
{
    SymbolKind Kind { get; }

    IReadOnlyList<SceneElement> Create(SymbolRenderContext context);
}
