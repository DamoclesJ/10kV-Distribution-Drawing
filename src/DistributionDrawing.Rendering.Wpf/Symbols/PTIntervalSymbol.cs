using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PTIntervalSymbol : IIntervalSymbolDefinition
{
    private const double ConductorThickness = 0.6;
    private const double PTSymbolWidth = 14;
    private const double PTSymbolHeight = 12;

    public IntervalKind Kind => IntervalKind.PTInterval;

    public IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary,
        bool includeLabels = true)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        if (interval.IntervalKind != Kind)
        {
            throw new InvalidOperationException(
                "The PT symbol requires a PT interval.");
        }

        DocumentPoint origin = new(
            cabinetPosition.XMillimeters + layout.RelativePosition.XMillimeters,
            cabinetPosition.YMillimeters + layout.RelativePosition.YMillimeters);
        DocumentPoint ptPosition = layout.PTSymbolPosition is DocumentPoint position
            ? new DocumentPoint(
                origin.XMillimeters + position.XMillimeters,
                origin.YMillimeters + position.YMillimeters)
            : throw new InvalidOperationException(
                $"PT interval '{interval.IntervalId}' has no PT symbol position.");
        var elements = new List<SceneElement>();

        elements.AddRange(symbolLibrary.Create(
            SymbolKind.RingCabinetInterval,
            new SymbolRenderContext(
                origin,
                layout.WidthMillimeters,
                layout.HeightMillimeters,
                fill: Colors.White,
                thicknessMillimeters: 0.6)));
        SwitchDevice isolation = GetSingleSwitch(interval, SwitchKind.IsolationSwitch);
        SwitchDevice ground = GetSingleSwitch(interval, SwitchKind.GroundSwitch);
        AddSwitch(elements, isolation, layout, origin, symbolLibrary);
        AddSwitch(elements, ground, layout, origin, symbolLibrary);

        elements.Add(new SceneRectangle(
            new DocumentRect(
                ptPosition.XMillimeters,
                ptPosition.YMillimeters,
                PTSymbolWidth,
                PTSymbolHeight),
            Colors.Black,
            0.8,
            Colors.White));
        elements.Add(new SceneText(
            new DocumentPoint(ptPosition.XMillimeters + 3, ptPosition.YMillimeters + 4),
            "PT",
            Colors.Black,
            3.5));

        DocumentPoint isolationCenter = GetSwitchCenter(isolation, layout, origin);
        DocumentPoint groundCenter = GetSwitchCenter(ground, layout, origin);
        DocumentPoint ptCenter = new(
            ptPosition.XMillimeters + PTSymbolWidth / 2,
            ptPosition.YMillimeters + PTSymbolHeight / 2);
        AddConductor(elements, new DocumentPoint(isolationCenter.XMillimeters, origin.YMillimeters), isolationCenter);
        AddConductor(elements, isolationCenter, ptCenter);
        AddGroundBranch(elements, ptCenter, groundCenter, symbolLibrary);

        return elements;
    }

    private static void AddSwitch(
        ICollection<SceneElement> elements,
        SwitchDevice switchDevice,
        RingCabinetIntervalLayout layout,
        DocumentPoint origin,
        SymbolLibrary symbolLibrary)
    {
        if (!layout.SwitchLayouts.TryGetValue(switchDevice.Id, out RingCabinetSwitchLayout switchLayout))
        {
            throw new InvalidOperationException(
                $"No layout exists for switch '{switchDevice.Id}' in interval '{layout.IntervalId}'.");
        }

        DocumentPoint switchOrigin = new(
            origin.XMillimeters + switchLayout.RelativePosition.XMillimeters,
            origin.YMillimeters + switchLayout.RelativePosition.YMillimeters);
        foreach (SceneElement element in symbolLibrary.Create(
                     SymbolLibrary.ResolveSwitchKind(switchDevice),
                     new SymbolRenderContext(
                         switchOrigin,
                         switchLayout.WidthMillimeters,
                         switchLayout.HeightMillimeters,
                         labelOrigin: new DocumentPoint(
                             switchOrigin.XMillimeters + switchLayout.LabelOffset.XMillimeters,
                             switchOrigin.YMillimeters + switchLayout.LabelOffset.YMillimeters),
                         state: SymbolLibrary.ResolveVisualState(switchDevice.SwitchState),
                         fill: Colors.White)))
        {
            elements.Add(element);
        }
    }

    private static DocumentPoint GetSwitchCenter(
        SwitchDevice switchDevice,
        RingCabinetIntervalLayout layout,
        DocumentPoint origin)
    {
        RingCabinetSwitchLayout switchLayout = layout.SwitchLayouts[switchDevice.Id];
        return new DocumentPoint(
            origin.XMillimeters + switchLayout.RelativePosition.XMillimeters + switchLayout.WidthMillimeters / 2,
            origin.YMillimeters + switchLayout.RelativePosition.YMillimeters + switchLayout.HeightMillimeters / 2);
    }

    private static void AddConductor(
        ICollection<SceneElement> elements,
        DocumentPoint start,
        DocumentPoint end) => elements.Add(new SceneLine(start, end, Colors.Black, ConductorThickness));

    private static void AddGroundBranch(
        ICollection<SceneElement> elements,
        DocumentPoint node,
        DocumentPoint groundCenter,
        SymbolLibrary symbolLibrary)
    {
        elements.Add(new SceneLine(node, groundCenter, Colors.Black, ConductorThickness));
        DocumentPoint earth = new(groundCenter.XMillimeters + 18, groundCenter.YMillimeters);
        foreach (SceneElement element in symbolLibrary.Create(
                     SymbolKind.GroundingLine,
                     new SymbolRenderContext(
                         groundCenter,
                         1,
                         1,
                         end: earth,
                         thicknessMillimeters: ConductorThickness)))
        {
            elements.Add(element);
        }
    }

    private static SwitchDevice GetSingleSwitch(RingCabinetInterval interval, SwitchKind kind) =>
        interval.SwitchDevices.SingleOrDefault(device => device.SwitchKind == kind)
        ?? throw new InvalidOperationException(
            $"Interval '{interval.IntervalId}' does not contain exactly one '{kind}'.");
}
