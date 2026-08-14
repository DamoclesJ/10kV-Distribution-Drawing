using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class CableSymbol
{
    private readonly SymbolLibrary _library;

    public CableSymbol(SymbolLibrary? library = null)
    {
        _library = library ?? new SymbolLibrary();
    }

    public IReadOnlyList<SceneElement> CreateElements(CableLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var elements = new List<SceneElement>();
        for (var index = 1; index < layout.Path.Count; index++)
        {
            elements.AddRange(_library.CreateCableLine(
                layout.Path[index - 1],
                layout.Path[index]));
        }

        return elements;
    }
}
