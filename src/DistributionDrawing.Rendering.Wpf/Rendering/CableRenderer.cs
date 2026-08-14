using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an existing cable segment without creating or modifying Domain objects.
/// </summary>
public sealed class CableRenderer
{
    private readonly CableSymbol _cableSymbol;
    private readonly CableLabel _cableLabel;

    public CableRenderer(SymbolLibrary? symbolLibrary = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _cableSymbol = new CableSymbol(library);
        _cableLabel = new CableLabel();
    }

    public IReadOnlyList<SceneElement> Render(
        CableSegment cableSegment,
        CableLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cableSegment);
        ArgumentNullException.ThrowIfNull(layout);

        var elements = new List<SceneElement>();
        elements.AddRange(_cableSymbol.CreateElements(layout));
        elements.AddRange(_cableLabel.CreateElements(cableSegment, layout));
        return elements;
    }
}
