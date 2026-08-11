using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class IntervalSymbol
{
    private readonly Dictionary<IntervalKind, IIntervalSymbolDefinition> _definitions = [];

    public IntervalSymbol(SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        SymbolLibrary = symbolLibrary;
        Register(new LoadSwitchIntervalSymbol());
        Register(new IntegratedFeederIntervalSymbol());
    }

    public SymbolLibrary SymbolLibrary { get; }

    public void Register(IIntervalSymbolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Kind] = definition;
    }

    public IReadOnlyList<SceneElement> CreateElements(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);

        if (interval.IntervalId != layout.IntervalId)
        {
            throw new InvalidOperationException(
                "Interval and interval layout IDs must match.");
        }

        if (!_definitions.TryGetValue(interval.IntervalKind, out IIntervalSymbolDefinition definition))
        {
            throw new NotSupportedException(
                $"No interval symbol is registered for '{interval.IntervalKind}'.");
        }

        return definition.Create(interval, layout, cabinetPosition, SymbolLibrary);
    }
}

public interface IIntervalSymbolDefinition
{
    IntervalKind Kind { get; }

    IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary);
}
