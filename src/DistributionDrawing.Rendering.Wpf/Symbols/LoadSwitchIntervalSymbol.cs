using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class LoadSwitchIntervalSymbol : IIntervalSymbolDefinition
{
    private readonly DrawingMetrics _metrics = DrawingMetrics.Default;

    public IntervalKind Kind => IntervalKind.LoadSwitchInterval;

    public IReadOnlyList<SceneElement> Create(
        RingCabinetInterval interval,
        RingCabinetIntervalLayout layout,
        DocumentPoint cabinetPosition,
        SymbolLibrary symbolLibrary,
        bool includeLabels = true,
        double? busbarYMillimeters = null)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(symbolLibrary);

        DocumentPoint origin = new(
            cabinetPosition.XMillimeters + layout.RelativePosition.XMillimeters,
            cabinetPosition.YMillimeters + layout.RelativePosition.YMillimeters);
        double centerX = origin.XMillimeters + layout.WidthMillimeters / 2;
        double busY = busbarYMillimeters ?? origin.YMillimeters;
        var elements = new List<SceneElement>();

        SwitchDevice loadSwitch = GetSingleSwitch(interval, SwitchKind.LoadSwitch);
        SwitchDevice groundSwitch = GetSingleSwitch(interval, SwitchKind.GroundSwitch);
        RingCabinetSwitchLayout loadLayout = GetLayout(layout, loadSwitch);
        RingCabinetSwitchLayout groundLayout = GetLayout(layout, groundSwitch);

        (DocumentPoint loadTop, DocumentPoint common) =
            RingCabinetProfessionalGeometry.AddThreePositionSwitch(
                elements,
                loadSwitch,
                groundSwitch,
                loadLayout,
                groundLayout,
                origin,
                _metrics);
        elements.Add(new SceneLine(
            new DocumentPoint(centerX, busY),
            loadTop,
            Colors.Black,
            _metrics.General.StandardStrokeThickness));

        DocumentPoint terminalTip = new(
            centerX,
            origin.YMillimeters + layout.HeightMillimeters);
        double terminalTop = terminalTip.YMillimeters -
                             _metrics.CableTermination.TriangleHeight;
        elements.Add(new SceneLine(
            common,
            new DocumentPoint(centerX, terminalTop),
            Colors.Black,
            _metrics.General.StandardStrokeThickness));
        RingCabinetProfessionalGeometry.AddCableTerminationMarker(
            elements,
            terminalTip,
            _metrics);

        return elements;
    }

    private static RingCabinetSwitchLayout GetLayout(
        RingCabinetIntervalLayout intervalLayout,
        SwitchDevice switchDevice) =>
        intervalLayout.SwitchLayouts.GetValueOrDefault(switchDevice.Id)
        ?? throw new InvalidOperationException(
            $"No layout exists for switch '{switchDevice.Id}' in interval '{intervalLayout.IntervalId}'.");

    private static SwitchDevice GetSingleSwitch(
        RingCabinetInterval interval,
        SwitchKind kind) =>
        interval.SwitchDevices.SingleOrDefault(device => device.SwitchKind == kind)
        ?? throw new InvalidOperationException(
            $"Interval '{interval.IntervalId}' does not contain exactly one '{kind}'.");
}
