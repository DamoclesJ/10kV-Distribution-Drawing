using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Creates the initial millimeter layout for an existing, validated cabinet aggregate.
/// These dimensions are an initial layout strategy, not electrical or professional rules.
/// </summary>
public sealed class RingCabinetLayoutFactory
{
    public static DocumentPoint DefaultPTSymbolPosition { get; } = new(
        DrawingMetrics.Default.RingCabinet.StandardIntervalWidth / 2 -
        DrawingMetrics.Default.PT.CoilRadius,
        DrawingMetrics.Default.RingCabinet.StandardIntervalHeight + 8);

    private readonly DrawingMetrics _metrics;

    public RingCabinetLayoutFactory(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public RingCabinetLayout Create(RingCabinet cabinet, DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(cabinet);

        RingCabinetIntervalLayout[] intervals = cabinet.Intervals
            .Select(CreateIntervalLayout)
            .ToArray();
        double width = _metrics.RingCabinet.CabinetPadding * 2 +
                       _metrics.RingCabinet.StandardIntervalWidth * intervals.Length +
                       _metrics.RingCabinet.IntervalSpacing * Math.Max(0, intervals.Length - 1);

        return new RingCabinetLayout(
            cabinet.Id,
            position,
            width,
            _metrics.RingCabinet.StandardIntervalHeight +
            _metrics.RingCabinet.CabinetPadding * 2,
            _metrics.RingCabinet.BusbarOffset,
            intervals);
    }

    public RingCabinetLayout RebuildInterval(
        RingCabinet cabinet,
        RingCabinetLayout currentLayout,
        Guid intervalId)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(currentLayout);
        if (cabinet.Id != currentLayout.CabinetId)
        {
            throw new ArgumentException(
                "Ring cabinet and layout IDs must match.",
                nameof(currentLayout));
        }

        RingCabinetInterval interval = cabinet.Intervals.SingleOrDefault(
                candidate => candidate.IntervalId == intervalId)
            ?? throw new InvalidOperationException(
                $"Interval '{intervalId}' does not belong to cabinet '{cabinet.Id}'.");
        RingCabinetIntervalLayout currentInterval = currentLayout.IntervalLayouts
            .GetValueOrDefault(intervalId)
            ?? throw new InvalidOperationException(
                $"No layout exists for interval '{intervalId}'.");
        RingCabinetIntervalLayout standard = CreateIntervalLayout(interval);
        var replacement = new RingCabinetIntervalLayout(
            intervalId,
            currentInterval.RelativePosition,
            currentInterval.WidthMillimeters,
            currentInterval.HeightMillimeters,
            currentInterval.SequenceLabelOffset,
            currentInterval.NameLabelOffset,
            standard.SwitchLayouts.Values,
            standard.PTSymbolPosition);
        RingCabinetIntervalLayout[] intervals = currentLayout.IntervalLayouts.Values
            .Select(layout => layout.IntervalId == intervalId ? replacement : layout)
            .ToArray();

        return new RingCabinetLayout(
            currentLayout.CabinetId,
            currentLayout.Position,
            currentLayout.WidthMillimeters,
            currentLayout.HeightMillimeters,
            currentLayout.MainBusYMillimeters,
            intervals,
            currentLayout.LabelOffset);
    }

    public static DocumentPoint? ResolvePTSymbolPosition(IntervalKind intervalKind) =>
        intervalKind == IntervalKind.PTInterval ? DefaultPTSymbolPosition : null;

    private RingCabinetIntervalLayout CreateIntervalLayout(
        RingCabinetInterval interval)
    {
        double x = _metrics.RingCabinet.CabinetPadding +
                   (interval.Sequence - 1) *
                   (_metrics.RingCabinet.StandardIntervalWidth +
                    _metrics.RingCabinet.IntervalSpacing);
        IReadOnlyList<RingCabinetSwitchLayout> switches = interval.IntervalKind switch
        {
            IntervalKind.LoadSwitchInterval => CreateLoadSwitchLayouts(interval),
            IntervalKind.IntegratedFeederInterval => CreateIntegratedFeederLayouts(interval),
            IntervalKind.PTInterval => CreatePTLayouts(interval),
            _ => throw new NotSupportedException(
                $"No initial layout strategy exists for '{interval.IntervalKind}'.")
        };

        HashSet<Guid> switchIds = interval.SwitchDevices
            .Select(device => device.Id)
            .ToHashSet();
        Guid[] layoutSwitchIds = switches
            .Select(layout => layout.SwitchDeviceId)
            .ToArray();
        if (layoutSwitchIds.Distinct().Count() != layoutSwitchIds.Length ||
            !switchIds.SetEquals(layoutSwitchIds))
        {
            throw new InvalidOperationException(
                $"Initial layout coverage does not match interval '{interval.IntervalId}'.");
        }

        return new RingCabinetIntervalLayout(
            interval.IntervalId,
            new DocumentPoint(x, _metrics.RingCabinet.CabinetPadding),
            _metrics.RingCabinet.StandardIntervalWidth,
            _metrics.RingCabinet.StandardIntervalHeight,
            switchLayouts: switches,
            ptSymbolPosition: ResolvePTSymbolPosition(interval.IntervalKind));
    }

    private IReadOnlyList<RingCabinetSwitchLayout> CreateLoadSwitchLayouts(
        RingCabinetInterval interval)
    {
        double mainX = GetMainSwitchX();
        double primaryY = GetPrimaryDeviceY();
        double groundX = GetGroundSwitchX();
        double groundY = GetGroundSwitchYForNode(primaryY);
        return
        [
            CreateSwitchLayout(
                interval,
                SwitchKind.LoadSwitch,
                new DocumentPoint(mainX, primaryY)),
            CreateSwitchLayout(
                interval,
                SwitchKind.GroundSwitch,
                new DocumentPoint(groundX, groundY))
        ];
    }

    private IReadOnlyList<RingCabinetSwitchLayout> CreateIntegratedFeederLayouts(
        RingCabinetInterval interval)
    {
        GroundingStructureKind structure = interval.GroundingStructureKind
            ?? throw new InvalidOperationException(
                $"Integrated-feeder interval '{interval.IntervalId}' has no grounding structure.");
        double mainX = GetMainSwitchX();
        double primaryY = GetPrimaryDeviceY();
        double secondaryY = GetSecondaryDeviceY();
        double groundX = GetGroundSwitchX();
        (SwitchKind upper, SwitchKind lower, double connectedDeviceY) = structure switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (SwitchKind.IsolationSwitch, SwitchKind.CircuitBreaker, primaryY),
            GroundingStructureKind.UpperLowerGrounding =>
                (SwitchKind.IsolationSwitch, SwitchKind.CircuitBreaker, secondaryY),
            GroundingStructureKind.LowerLowerGrounding =>
                (SwitchKind.CircuitBreaker, SwitchKind.IsolationSwitch, secondaryY),
            _ => throw new ArgumentOutOfRangeException(nameof(interval))
        };
        double groundY = GetGroundSwitchYForNode(connectedDeviceY);

        return
        [
            CreateSwitchLayout(interval, upper, new DocumentPoint(mainX, primaryY)),
            CreateSwitchLayout(interval, lower, new DocumentPoint(mainX, secondaryY)),
            CreateSwitchLayout(
                interval,
                SwitchKind.GroundSwitch,
                new DocumentPoint(groundX, groundY))
        ];
    }

    private IReadOnlyList<RingCabinetSwitchLayout> CreatePTLayouts(
        RingCabinetInterval interval)
    {
        return
        [
            CreateSwitchLayout(
                interval,
                SwitchKind.IsolationSwitch,
                new DocumentPoint(GetMainSwitchX(), GetPrimaryDeviceY())),
            CreateSwitchLayout(
                interval,
                SwitchKind.GroundSwitch,
                new DocumentPoint(
                    GetGroundSwitchX(),
                    GetGroundSwitchYForNode(GetPrimaryDeviceY())))
        ];
    }

    private RingCabinetSwitchLayout CreateSwitchLayout(
        RingCabinetInterval interval,
        SwitchKind switchKind,
        DocumentPoint position)
    {
        SwitchDevice switchDevice = interval.SwitchDevices.SingleOrDefault(
                candidate => candidate.SwitchKind == switchKind)
            ?? throw new InvalidOperationException(
                $"Interval '{interval.IntervalId}' does not contain exactly one '{switchKind}'.");
        return new RingCabinetSwitchLayout(
            switchDevice.Id,
            position,
            switchKind == SwitchKind.GroundSwitch
                ? _metrics.Switch.GroundSwitchLength * _metrics.RingCabinet.SwitchSymbolScale
                : _metrics.Switch.StandardSwitchLength * _metrics.RingCabinet.SwitchSymbolScale,
            _metrics.Switch.LogicalHitHeight * _metrics.RingCabinet.SwitchSymbolScale);
    }

    private double GetMainSwitchX() =>
        (_metrics.RingCabinet.StandardIntervalWidth -
         _metrics.Switch.StandardSwitchLength * _metrics.RingCabinet.SwitchSymbolScale) / 2;

    private double GetGroundSwitchX() =>
        _metrics.RingCabinet.StandardIntervalWidth / 2 -
        _metrics.RingCabinet.DeviceVerticalSpacing / 2 -
        _metrics.Switch.GroundSwitchLength * _metrics.RingCabinet.SwitchSymbolScale;

    private double GetPrimaryDeviceY() =>
        _metrics.RingCabinet.BusbarOffset -
        _metrics.RingCabinet.CabinetPadding +
        _metrics.RingCabinet.DeviceVerticalSpacing;

    private double GetSecondaryDeviceY() =>
        GetPrimaryDeviceY() +
        _metrics.Switch.LogicalHitHeight * _metrics.RingCabinet.SwitchSymbolScale +
        _metrics.RingCabinet.DeviceVerticalSpacing;

    private double GetGroundSwitchYForNode(double connectedDeviceY)
    {
        double contactInset = Math.Max(
            _metrics.Switch.ContactRadius,
            Math.Min(
                _metrics.Switch.LogicalHitHeight * _metrics.RingCabinet.SwitchSymbolScale / 4,
                _metrics.Switch.StandardSwitchLength * _metrics.RingCabinet.SwitchSymbolScale / 4));
        return connectedDeviceY +
               _metrics.Switch.LogicalHitHeight * _metrics.RingCabinet.SwitchSymbolScale / 2 -
               contactInset;
    }
}
