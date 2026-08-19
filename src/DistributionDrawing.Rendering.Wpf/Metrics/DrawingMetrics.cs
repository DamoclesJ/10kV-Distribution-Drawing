using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Metrics;

public sealed record DrawingMetrics(
    GeneralDrawingMetrics General,
    RingCabinetDrawingMetrics RingCabinet,
    SwitchDrawingMetrics Switch,
    PTDrawingMetrics PT,
    PoleDrawingMetrics Pole,
    CableTerminationDrawingMetrics CableTermination,
    LineDrawingMetrics Line,
    LineJumpDrawingMetrics LineJump)
{
    public static DrawingMetrics Default { get; } = new(
        new GeneralDrawingMetrics(
            StandardStrokeThickness: 0.8,
            ThinStrokeThickness: 0.6,
            StandardFontSize: 4,
            SmallFontSize: 3.5),
        new RingCabinetDrawingMetrics(
            StandardIntervalWidth: 60,
            StandardIntervalHeight: 125,
            BusbarOffset: 25,
            BusbarHeight: 1,
            IntervalSpacing: 5,
            CabinetNameOffset: new DocumentPoint(0, -8)),
        new SwitchDrawingMetrics(
            StandardSwitchLength: 16,
            GroundSwitchLength: 16,
            ContactRadius: 1.5),
        new PTDrawingMetrics(
            CoilRadius: 7,
            CoilSpacing: 6),
        new PoleDrawingMetrics(PoleRadius: 7),
        new CableTerminationDrawingMetrics(
            TriangleWidth: 10,
            TriangleHeight: 8),
        new LineDrawingMetrics(
            ConnectionThickness: 0.8,
            CableDashLength: 4,
            CableDashGap: 3),
        new LineJumpDrawingMetrics(Radius: 4));
}

public sealed record GeneralDrawingMetrics(
    double StandardStrokeThickness,
    double ThinStrokeThickness,
    double StandardFontSize,
    double SmallFontSize);

public sealed record RingCabinetDrawingMetrics(
    double StandardIntervalWidth,
    double StandardIntervalHeight,
    double BusbarOffset,
    double BusbarHeight,
    double IntervalSpacing,
    DocumentPoint CabinetNameOffset);

public sealed record SwitchDrawingMetrics(
    double StandardSwitchLength,
    double GroundSwitchLength,
    double ContactRadius);

public sealed record PTDrawingMetrics(
    double CoilRadius,
    double CoilSpacing);

public sealed record PoleDrawingMetrics(double PoleRadius);

public sealed record CableTerminationDrawingMetrics(
    double TriangleWidth,
    double TriangleHeight);

public sealed record LineDrawingMetrics(
    double ConnectionThickness,
    double CableDashLength,
    double CableDashGap);

public sealed record LineJumpDrawingMetrics(double Radius);
