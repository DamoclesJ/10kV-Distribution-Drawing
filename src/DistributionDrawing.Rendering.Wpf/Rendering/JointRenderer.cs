using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders an intermediate topology terminal as a joint symbol without
/// creating or modifying Domain objects.
/// </summary>
public sealed class JointRenderer
{
    private readonly JointSymbol _jointSymbol;

    public JointRenderer(SymbolLibrary? symbolLibrary = null)
    {
        _jointSymbol = new JointSymbol(symbolLibrary ?? new SymbolLibrary());
    }

    public IReadOnlyList<SceneElement> Render(
        IntermediateTerminal intermediateTerminal,
        JointLayout layout)
    {
        ArgumentNullException.ThrowIfNull(intermediateTerminal);
        ArgumentNullException.ThrowIfNull(layout);

        return _jointSymbol.CreateElements(intermediateTerminal, layout);
    }
}
