using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PoleSymbol
{
    private readonly SymbolLibrary _library;

    public PoleSymbol(SymbolLibrary? library = null)
    {
        _library = library ?? new SymbolLibrary();
    }

    public IReadOnlyList<SceneElement> CreateElements(
        Pole pole,
        PoleLayout layout,
        bool includeLabel = true)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        return _library.CreatePole(pole, layout, includeLabel);
    }
}
