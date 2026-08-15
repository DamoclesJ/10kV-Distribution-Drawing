using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class JointSymbol
{
    private readonly SymbolLibrary _library;

    public JointSymbol(SymbolLibrary? symbolLibrary = null)
    {
        _library = symbolLibrary ?? new SymbolLibrary();
    }

    public IReadOnlyList<SceneElement> CreateElements(
        IntermediateTerminal intermediateTerminal,
        JointLayout layout)
    {
        ArgumentNullException.ThrowIfNull(intermediateTerminal);
        ArgumentNullException.ThrowIfNull(layout);

        if (intermediateTerminal.Id != layout.IntermediateTerminalId)
        {
            throw new InvalidOperationException(
                "Intermediate terminal and joint layout IDs must match.");
        }

        return _library.CreateJoint(
            layout.Position,
            layout.SizeMillimeters,
            label: null,
            layout.LabelPosition);
    }
}
