using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Metrics;

public sealed record DrawingMetrics(
    GeneralDrawingMetrics General,
    RingCabinetDrawingMetrics RingCabinet,
    SwitchDrawingMetrics Switch,
    PTDrawingMetrics PT,
    PoleDrawingMetrics Pole,
    PoleAttachmentDrawingMetrics PoleAttachment,
    CableTerminationDrawingMetrics CableTermination,
    LineDrawingMetrics Line,
    RoutingDrawingMetrics Routing,
    AlignmentDrawingMetrics Alignment,
    LineJumpDrawingMetrics LineJump)
{
    public static DrawingMetrics Default { get; } = new(
        new GeneralDrawingMetrics(
            StandardStrokeThickness: 0.8,
            ThinStrokeThickness: 0.6,
            StandardFontSize: 4,
            SmallFontSize: 3.5),
        new RingCabinetDrawingMetrics(
            CabinetPadding: 10,
            StandardIntervalWidth: 60,
            StandardIntervalHeight: 125,
            BusbarOffset: 25,
            BusbarHeight: 1,
            IntervalSpacing: 5,
            CabinetNameOffset: new DocumentPoint(0, -8),
            DeviceVerticalSpacing: 12),
        new SwitchDrawingMetrics(
            StandardSwitchLength: 16,
            GroundSwitchLength: 16,
            ContactRadius: 1.5,
            LogicalHitHeight: 10),
        new PTDrawingMetrics(
            CoilRadius: 7,
            CoilSpacing: 6),
        new PoleDrawingMetrics(
            PoleRadius: 7,
            LabelOffset: new DocumentPoint(16, -4)),
        new PoleAttachmentDrawingMetrics(
            SymbolWidth: 18,
            SymbolHeight: 10,
            LabelOffset: new DocumentPoint(0, -4),
            InternalInset: 3,
            ContactMarkerLength: 4,
            ContactCrossSize: 3,
            IsolationBladeStartRatio: 0.28,
            IsolationContactRatio: 0.72,
            OpenBladeTopRatio: 0.18,
            FuseTubeWidth: 2.4,
            FuseTubeInset: 2,
            FuseOpenOffset: 4.5,
            OperationArrowLength: 5),
        new CableTerminationDrawingMetrics(
            TriangleWidth: 10,
            TriangleHeight: 8,
            LogicalHitPadding: 2),
        new LineDrawingMetrics(
            ConnectionThickness: 0.8,
            CableDashLength: 4,
            CableDashGap: 3),
        new RoutingDrawingMetrics(
            PortStubLength: 8,
            ObstacleClearance: 4,
            ParallelSpacing: 6,
            MinimumDoglegLength: 10,
            CrossingTolerance: 0.001),
        new AlignmentDrawingMetrics(SnapTolerance: 4),
        new LineJumpDrawingMetrics(
            Radius: 4,
            EndpointClearance: 2));
}

public sealed record GeneralDrawingMetrics(
    double StandardStrokeThickness,
    double ThinStrokeThickness,
    double StandardFontSize,
    double SmallFontSize);

public sealed record RingCabinetDrawingMetrics(
    double CabinetPadding,
    double StandardIntervalWidth,
    double StandardIntervalHeight,
    double BusbarOffset,
    double BusbarHeight,
    double IntervalSpacing,
    DocumentPoint CabinetNameOffset,
    double DeviceVerticalSpacing);

public sealed record SwitchDrawingMetrics(
    double StandardSwitchLength,
    double GroundSwitchLength,
    double ContactRadius,
    double LogicalHitHeight);

public sealed record PTDrawingMetrics(
    double CoilRadius,
    double CoilSpacing);

public sealed record PoleDrawingMetrics(
    double PoleRadius,
    DocumentPoint LabelOffset);

public sealed record PoleAttachmentDrawingMetrics(
    double SymbolWidth,
    double SymbolHeight,
    DocumentPoint LabelOffset,
    double InternalInset,
    double ContactMarkerLength,
    double ContactCrossSize,
    double IsolationBladeStartRatio,
    double IsolationContactRatio,
    double OpenBladeTopRatio,
    double FuseTubeWidth,
    double FuseTubeInset,
    double FuseOpenOffset,
    double OperationArrowLength);

public sealed record CableTerminationDrawingMetrics(
    double TriangleWidth,
    double TriangleHeight,
    double LogicalHitPadding);

public sealed record LineDrawingMetrics(
    double ConnectionThickness,
    double CableDashLength,
    double CableDashGap);

public sealed record RoutingDrawingMetrics(
    double PortStubLength,
    double ObstacleClearance,
    double ParallelSpacing,
    double MinimumDoglegLength,
    double CrossingTolerance);

public sealed record AlignmentDrawingMetrics(double SnapTolerance);

public sealed record LineJumpDrawingMetrics(
    double Radius,
    double EndpointClearance);
