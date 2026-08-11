using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class LoadSwitchIntervalSymbol : IIntervalSymbolDefinition
{
    public IntervalKind Kind => IntervalKind.LoadSwitchInterval;

    public IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(symbolLibrary);

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

        double centerX = origin.XMillimeters + layout.WidthMillimeters / 2;
        elements.Add(
            new SceneLine(
                new DocumentPoint(centerX, origin.YMillimeters),
                new DocumentPoint(centerX, origin.YMillimeters + layout.HeightMillimeters),
                Colors.Black,
                0.6));

        SwitchDevice loadSwitch = GetSingleSwitch(interval, SwitchKind.LoadSwitch);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);

        AddSwitch(
            elements,
            loadSwitch,
            layout,
            origin,
            symbolLibrary);
        AddSwitch(
            elements,
            groundSwitch,
            layout,
            origin,
            symbolLibrary);

        return elements;
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
        SymbolKind symbolKind = SymbolLibrary.ResolveSwitchKind(switchDevice);

        elements.AddRange(
            symbolLibrary.Create(
                symbolKind,
                new SymbolRenderContext(
                    switchOrigin,
                    switchLayout.WidthMillimeters,
                    switchLayout.HeightMillimeters,
                    labelOrigin: new DocumentPoint(
                        switchOrigin.XMillimeters + switchLayout.LabelOffset.XMillimeters,
                        switchOrigin.YMillimeters + switchLayout.LabelOffset.YMillimeters),
                    label: switchDevice.DisplayName,
                    state: SymbolLibrary.ResolveVisualState(switchDevice.SwitchState),
                    fill: Colors.White)));
    }

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
