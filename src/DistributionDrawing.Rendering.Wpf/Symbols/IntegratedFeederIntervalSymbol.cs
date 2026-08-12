using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

/// <summary>
/// Renders the visual composition of an integrated-feeder interval.
/// It does not evaluate operational state, grounding, or interlocks.
/// </summary>
public sealed class IntegratedFeederIntervalSymbol : IIntervalSymbolDefinition
{
    private const double ConductorThickness = 0.6;
    private const double TerminalWidth = 10;
    private const double TerminalHeight = 8;

    public IntervalKind Kind => IntervalKind.IntegratedFeederInterval;

    public IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        if (interval.IntervalKind != Kind)
        {
            throw new InvalidOperationException(
                "The integrated-feeder symbol requires an integrated-feeder interval.");
        }

        if (interval.GroundingStructureKind is not GroundingStructureKind structureKind)
        {
            throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' has no grounding structure.");
        }

        DocumentPoint origin = new(
            cabinetPosition.XMillimeters + layout.RelativePosition.XMillimeters,
            cabinetPosition.YMillimeters + layout.RelativePosition.YMillimeters);
        var elements = new List<SceneElement>();

        elements.AddRange(
            symbolLibrary.Create(
                SymbolKind.RingCabinetInterval,
                new SymbolRenderContext(
                    origin,
                    layout.WidthMillimeters,
                    layout.HeightMillimeters,
                    fill: Colors.White,
                    thicknessMillimeters: 0.6)));
        AddLabels(elements, interval, layout, origin);

        SwitchDevice isolation = GetSingleSwitch(interval, SwitchKind.IsolationSwitch);
        SwitchDevice breaker = GetSingleSwitch(interval, SwitchKind.CircuitBreaker);
        SwitchDevice ground = GetSingleSwitch(interval, SwitchKind.GroundSwitch);

        AddSwitch(elements, isolation, layout, origin, symbolLibrary);
        AddSwitch(elements, breaker, layout, origin, symbolLibrary);
        AddSwitch(elements, ground, layout, origin, symbolLibrary);

        DocumentPoint isolationCenter = GetSwitchCenter(isolation, layout, origin);
        DocumentPoint breakerCenter = GetSwitchCenter(breaker, layout, origin);
        DocumentPoint groundCenter = GetSwitchCenter(ground, layout, origin);

        DocumentPoint upperNode;
        DocumentPoint lowerNode;
        DocumentPoint groundingNode;
        switch (structureKind)
        {
            case GroundingStructureKind.UpperIsolationGrounding:
                upperNode = isolationCenter;
                lowerNode = breakerCenter;
                groundingNode = Midpoint(isolationCenter, breakerCenter);
                break;
            case GroundingStructureKind.UpperLowerGrounding:
                upperNode = isolationCenter;
                lowerNode = breakerCenter;
                groundingNode = lowerNode;
                break;
            case GroundingStructureKind.LowerLowerGrounding:
                upperNode = breakerCenter;
                lowerNode = isolationCenter;
                groundingNode = lowerNode;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(structureKind));
        }

        AddConductor(
            elements,
            new DocumentPoint(upperNode.XMillimeters, origin.YMillimeters),
            upperNode);
        AddConductor(elements, upperNode, lowerNode);
        AddGroundBranch(elements, groundingNode, groundCenter, symbolLibrary);
        AddExternalTerminal(elements, lowerNode, layout, origin, symbolLibrary);

        return elements;
    }

    private static void AddLabels(
        ICollection<SceneElement> elements,
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint origin)
    {
        elements.Add(
            new SceneText(
                new DocumentPoint(
                    origin.XMillimeters + layout.SequenceLabelOffset.XMillimeters,
                    origin.YMillimeters + layout.SequenceLabelOffset.YMillimeters),
                $"{interval.Sequence}#",
                Colors.Black,
                3.5));
        elements.Add(
            new SceneText(
                new DocumentPoint(
                    origin.XMillimeters + layout.NameLabelOffset.XMillimeters,
                    origin.YMillimeters + layout.NameLabelOffset.YMillimeters),
                interval.DisplayName,
                Colors.Black,
                3.5));
    }

    private static void AddSwitch(
        ICollection<SceneElement> elements,
        SwitchDevice switchDevice,
        RingCabinetIntervalLayout intervalLayout,
        DocumentPoint intervalOrigin,
        SymbolLibrary symbolLibrary)
    {
        if (!intervalLayout.SwitchLayouts.TryGetValue(
                switchDevice.Id,
                out RingCabinetSwitchLayout switchLayout))
        {
            throw new InvalidOperationException(
                $"No layout exists for switch '{switchDevice.Id}' in interval '{intervalLayout.IntervalId}'.");
        }

        DocumentPoint switchOrigin = new(
            intervalOrigin.XMillimeters + switchLayout.RelativePosition.XMillimeters,
            intervalOrigin.YMillimeters + switchLayout.RelativePosition.YMillimeters);
        foreach (SceneElement element in symbolLibrary.Create(
                     SymbolLibrary.ResolveSwitchKind(switchDevice),
                     new SymbolRenderContext(
                         switchOrigin,
                         switchLayout.WidthMillimeters,
                         switchLayout.HeightMillimeters,
                         labelOrigin: new DocumentPoint(
                             switchOrigin.XMillimeters + switchLayout.LabelOffset.XMillimeters,
                             switchOrigin.YMillimeters + switchLayout.LabelOffset.YMillimeters),
                         label: switchDevice.DisplayName,
                         state: SymbolLibrary.ResolveVisualState(switchDevice.SwitchState),
                         fill: Colors.White)))
        {
            elements.Add(element);
        }
    }

    private static DocumentPoint GetSwitchCenter(
        SwitchDevice switchDevice,
        RingCabinetIntervalLayout intervalLayout,
        DocumentPoint intervalOrigin)
    {
        if (!intervalLayout.SwitchLayouts.TryGetValue(
                switchDevice.Id,
                out RingCabinetSwitchLayout switchLayout))
        {
            throw new InvalidOperationException(
                $"No layout exists for switch '{switchDevice.Id}' in interval '{intervalLayout.IntervalId}'.");
        }

        return new DocumentPoint(
            intervalOrigin.XMillimeters + switchLayout.RelativePosition.XMillimeters +
            switchLayout.WidthMillimeters / 2,
            intervalOrigin.YMillimeters + switchLayout.RelativePosition.YMillimeters +
            switchLayout.HeightMillimeters / 2);
    }

    private static void AddConductor(
        ICollection<SceneElement> elements,
        DocumentPoint start,
        DocumentPoint end)
    {
        elements.Add(new SceneLine(start, end, Colors.Black, ConductorThickness));
    }

    private static void AddGroundBranch(
        ICollection<SceneElement> elements,
        DocumentPoint node,
        DocumentPoint groundSwitchCenter,
        SymbolLibrary symbolLibrary)
    {
        elements.Add(
            new SceneLine(
                node,
                groundSwitchCenter,
                Colors.Black,
                ConductorThickness));
        DocumentPoint earth = new(
            groundSwitchCenter.XMillimeters + 18,
            groundSwitchCenter.YMillimeters);
        foreach (SceneElement element in symbolLibrary.Create(
                     SymbolKind.GroundingLine,
                     new SymbolRenderContext(
                         groundSwitchCenter,
                         1,
                         1,
                         end: earth,
                         thicknessMillimeters: ConductorThickness)))
        {
            elements.Add(element);
        }
    }

    private static void AddExternalTerminal(
        ICollection<SceneElement> elements,
        DocumentPoint lowerNode,
        RingCabinetIntervalLayout layout,
        DocumentPoint origin,
        SymbolLibrary symbolLibrary)
    {
        DocumentPoint terminalOrigin = new(
            origin.XMillimeters + (layout.WidthMillimeters - TerminalWidth) / 2,
            origin.YMillimeters + layout.HeightMillimeters - TerminalHeight - 4);
        DocumentPoint terminalCenter = new(
            terminalOrigin.XMillimeters + TerminalWidth / 2,
            terminalOrigin.YMillimeters);
        elements.Add(new SceneLine(lowerNode, terminalCenter, Colors.Black, ConductorThickness));
        foreach (SceneElement element in symbolLibrary.Create(
                     SymbolKind.CableTermination,
                     new SymbolRenderContext(
                         terminalOrigin,
                         TerminalWidth,
                         TerminalHeight,
                         label: "外部端子",
                         fill: Colors.White,
                         thicknessMillimeters: ConductorThickness)))
        {
            elements.Add(element);
        }
    }

    private static DocumentPoint Midpoint(DocumentPoint first, DocumentPoint second) =>
        new(
            (first.XMillimeters + second.XMillimeters) / 2,
            (first.YMillimeters + second.YMillimeters) / 2);

    private static SwitchDevice GetSingleSwitch(
        RingCabinetInterval interval,
        SwitchKind kind)
    {
        return interval.SwitchDevices.SingleOrDefault(
                   switchDevice => switchDevice.SwitchKind == kind)
               ?? throw new InvalidOperationException(
                   $"Interval '{interval.IntervalId}' does not contain exactly one '{kind}'.");
    }
}
