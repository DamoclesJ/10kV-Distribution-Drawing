using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Creates the initial millimeter layout for an existing, validated cabinet aggregate.
/// These dimensions are an initial layout strategy, not electrical or professional rules.
/// </summary>
public sealed class RingCabinetLayoutFactory
{
    private const double CabinetPadding = 10;
    private const double IntervalGap = 5;
    private const double IntervalWidth = 60;
    private const double IntervalHeight = 125;
    private const double CabinetHeight = 145;
    private const double MainBusY = 25;
    private const double SwitchWidth = 16;
    private const double SwitchHeight = 10;

    public RingCabinetLayout Create(RingCabinet cabinet, DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(cabinet);

        RingCabinetIntervalLayout[] intervals = cabinet.Intervals
            .Select(CreateIntervalLayout)
            .ToArray();
        double width = CabinetPadding * 2 +
                       IntervalWidth * intervals.Length +
                       IntervalGap * Math.Max(0, intervals.Length - 1);

        return new RingCabinetLayout(
            cabinet.Id,
            position,
            width,
            CabinetHeight,
            MainBusY,
            intervals);
    }

    private static RingCabinetIntervalLayout CreateIntervalLayout(
        RingCabinetInterval interval)
    {
        double x = CabinetPadding +
                   (interval.Sequence - 1) * (IntervalWidth + IntervalGap);
        IReadOnlyList<RingCabinetSwitchLayout> switches = interval.IntervalKind switch
        {
            IntervalKind.LoadSwitchInterval => CreateLoadSwitchLayouts(interval),
            IntervalKind.IntegratedFeederInterval => CreateIntegratedFeederLayouts(interval),
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
            new DocumentPoint(x, CabinetPadding),
            IntervalWidth,
            IntervalHeight,
            switchLayouts: switches);
    }

    private static IReadOnlyList<RingCabinetSwitchLayout> CreateLoadSwitchLayouts(
        RingCabinetInterval interval)
    {
        return
        [
            CreateSwitchLayout(interval, SwitchKind.LoadSwitch, new DocumentPoint(23, 35)),
            CreateSwitchLayout(interval, SwitchKind.GroundSwitch, new DocumentPoint(23, 72))
        ];
    }

    private static IReadOnlyList<RingCabinetSwitchLayout> CreateIntegratedFeederLayouts(
        RingCabinetInterval interval)
    {
        GroundingStructureKind structure = interval.GroundingStructureKind
            ?? throw new InvalidOperationException(
                $"Integrated-feeder interval '{interval.IntervalId}' has no grounding structure.");
        (SwitchKind upper, SwitchKind lower, DocumentPoint groundPosition) = structure switch
        {
            GroundingStructureKind.UpperIsolationGrounding =>
                (SwitchKind.IsolationSwitch, SwitchKind.CircuitBreaker, new DocumentPoint(42, 49)),
            GroundingStructureKind.UpperLowerGrounding =>
                (SwitchKind.IsolationSwitch, SwitchKind.CircuitBreaker, new DocumentPoint(42, 84)),
            GroundingStructureKind.LowerLowerGrounding =>
                (SwitchKind.CircuitBreaker, SwitchKind.IsolationSwitch, new DocumentPoint(42, 84)),
            _ => throw new ArgumentOutOfRangeException(nameof(interval))
        };

        return
        [
            CreateSwitchLayout(interval, upper, new DocumentPoint(18, 28)),
            CreateSwitchLayout(interval, lower, new DocumentPoint(18, 70)),
            CreateSwitchLayout(interval, SwitchKind.GroundSwitch, groundPosition)
        ];
    }

    private static RingCabinetSwitchLayout CreateSwitchLayout(
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
            SwitchWidth,
            SwitchHeight);
    }
}
