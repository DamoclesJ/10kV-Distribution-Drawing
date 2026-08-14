using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an existing ring-cabinet aggregate without creating or modifying Domain objects.
/// </summary>
public sealed class RingCabinetRenderer
{
    private readonly RingCabinetSymbol _symbol;

    public RingCabinetRenderer(SymbolLibrary? symbolLibrary = null)
    {
        _symbol = new RingCabinetSymbol(symbolLibrary ?? new SymbolLibrary());
    }

    public IReadOnlyList<SceneElement> Render(
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);

        return _symbol.CreateElements(cabinet, layout);
    }
}
