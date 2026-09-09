using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Metrics;

public sealed record DrawingMetrics(
    GeneralDrawingMetrics General,
    DrawingTypographyMetrics Typography,
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
    public TransformerDrawingMetrics Transformer { get; init; } = new(
        MainRadius: 12,
        SmallCircleRadius: 2.5,
        SmallCircleOffsetX: 5,
        SmallCircleOffsetY: 15,
        TeeHalfWidth: 6,
        TeeTopY: -3,
        TriangleHalfWidth: 11,
        TriangleApexY: -12,
        TriangleBaseY: 9,
        IndoorCoilRadius: 8,
        IndoorCoilCenterSpacing: 12,
        HitPadding: 2);

    public GroundingDrawingMetrics Grounding { get; init; } = new(
        LeaderLength: 18, StemLength: 12, BarSpacing: 3,
        TopBarWidth: 12, MiddleBarWidth: 8, BottomBarWidth: 4,
        HitPadding: 2, NumberOffset: new DocumentPoint(8, 0),
        ManualSnapTolerance: 1);

    public static DrawingMetrics Default { get; } = new(
        new GeneralDrawingMetrics(
            StandardStrokeThickness: 0.8,
            ThinStrokeThickness: 0.6,
            StandardFontSize: 4,
            SmallFontSize: 3.5),
        new DrawingTypographyMetrics(
            CabinetNameFontSize: 16,
            LineNameFontSize: 8,
            IntervalNumberFontSize: 10.5,
            SwitchNumberFontSize: 7,
            PoleNumberFontSize: 8,
            PTLabelFontSize: 7),
        new RingCabinetDrawingMetrics(
            CabinetPadding: 10,
            StandardIntervalWidth: 60,
            StandardIntervalHeight: 125,
            BusbarOffset: 25,
            BusbarHeight: 1,
            IntervalSpacing: 5,
            CabinetNameOffset: new DocumentPoint(0, -8),
            DeviceVerticalSpacing: 12,
            SwitchSymbolScale: 2),
        new SwitchDrawingMetrics(
            StandardSwitchLength: 16,
            GroundSwitchLength: 16,
            ContactRadius: 1.5,
            LogicalHitHeight: 10),
        new PTDrawingMetrics(
            CoilRadius: 7,
            CoilSpacing: 6),
        new PoleDrawingMetrics(
            PoleRadius: 10.5,
            LabelOffset: new DocumentPoint(16, -4)),
        new PoleAttachmentDrawingMetrics(
            SymbolWidth: 27,
            SymbolHeight: 15,
            LabelOffset: new DocumentPoint(0, -4),
            InternalInset: 4.5,
            ContactMarkerLength: 6,
            ContactCrossSize: 4.5,
            IsolationBladeStartRatio: 0.28,
            IsolationContactRatio: 0.72,
            OpenBladeTopRatio: 0.18,
            FuseTubeWidth: 3.6,
            FuseTubeInset: 3,
            FuseOpenOffset: 6.75,
            OperationArrowLength: 7.5),
        new CableTerminationDrawingMetrics(
            TriangleWidth: 10,
            TriangleHeight: 8,
            LogicalHitPadding: 2,
            CableTerminalExitMinimumStubLength: 50),
        new LineDrawingMetrics(
            ConnectionThickness: 0.8,
            CableDashLength: 4,
            CableDashGap: 3,
            GroundingAccessMarkerDiameter: 2,
            GroundingAccessClearance: 2,
            GroundingAccessHitPadding: 3),
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

public sealed record TransformerDrawingMetrics(
    double MainRadius,
    double SmallCircleRadius,
    double SmallCircleOffsetX,
    double SmallCircleOffsetY,
    double TeeHalfWidth,
    double TeeTopY,
    double TriangleHalfWidth,
    double TriangleApexY,
    double TriangleBaseY,
    double IndoorCoilRadius,
    double IndoorCoilCenterSpacing,
    double HitPadding);

public sealed record GroundingDrawingMetrics(
    double LeaderLength,
    double StemLength,
    double BarSpacing,
    double TopBarWidth,
    double MiddleBarWidth,
    double BottomBarWidth,
    double HitPadding,
    DocumentPoint NumberOffset,
    double ManualSnapTolerance);

public sealed record GeneralDrawingMetrics(
    double StandardStrokeThickness,
    double ThinStrokeThickness,
    double StandardFontSize,
    double SmallFontSize);

public sealed class DrawingTypographyMetrics
{
    public DrawingTypographyMetrics(
        double CabinetNameFontSize,
        double LineNameFontSize,
        double IntervalNumberFontSize,
        double SwitchNumberFontSize,
        double PoleNumberFontSize,
        double PTLabelFontSize,
        double GroundingPointNumberFontSize = 7)
    {
        Update(
            CabinetNameFontSize,
            LineNameFontSize,
            IntervalNumberFontSize,
            SwitchNumberFontSize,
            PoleNumberFontSize,
            PTLabelFontSize,
            GroundingPointNumberFontSize);
    }

    public double CabinetNameFontSize { get; private set; }
    public double LineNameFontSize { get; private set; }
    public double IntervalNumberFontSize { get; private set; }
    public double SwitchNumberFontSize { get; private set; }
    public double PoleNumberFontSize { get; private set; }
    public double PTLabelFontSize { get; private set; }
    public double GroundingPointNumberFontSize { get; private set; }

    public void Update(
        double cabinetNameFontSize,
        double lineNameFontSize,
        double intervalNumberFontSize,
        double switchNumberFontSize,
        double poleNumberFontSize,
        double ptLabelFontSize,
        double? groundingPointNumberFontSize = null)
    {
        double[] values =
        [
            cabinetNameFontSize,
            lineNameFontSize,
            intervalNumberFontSize,
            switchNumberFontSize,
            poleNumberFontSize,
            ptLabelFontSize,
            groundingPointNumberFontSize ?? GroundingPointNumberFontSize
        ];
        if (values.Any(value => !double.IsFinite(value) || value <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cabinetNameFontSize),
                "Drawing font sizes must be finite and greater than zero.");
        }

        CabinetNameFontSize = cabinetNameFontSize;
        LineNameFontSize = lineNameFontSize;
        IntervalNumberFontSize = intervalNumberFontSize;
        SwitchNumberFontSize = switchNumberFontSize;
        PoleNumberFontSize = poleNumberFontSize;
        PTLabelFontSize = ptLabelFontSize;
        GroundingPointNumberFontSize = groundingPointNumberFontSize ?? GroundingPointNumberFontSize;
    }
}

public sealed class RingCabinetDrawingMetrics
{
    public RingCabinetDrawingMetrics(
        double CabinetPadding,
        double StandardIntervalWidth,
        double StandardIntervalHeight,
        double BusbarOffset,
        double BusbarHeight,
        double IntervalSpacing,
        DocumentPoint CabinetNameOffset,
        double DeviceVerticalSpacing,
        double SwitchSymbolScale)
    {
        this.CabinetPadding = CabinetPadding;
        this.StandardIntervalWidth = StandardIntervalWidth;
        this.StandardIntervalHeight = StandardIntervalHeight;
        this.BusbarOffset = BusbarOffset;
        this.BusbarHeight = BusbarHeight;
        this.IntervalSpacing = IntervalSpacing;
        this.CabinetNameOffset = CabinetNameOffset;
        this.DeviceVerticalSpacing = DeviceVerticalSpacing;
        this.SwitchSymbolScale = SwitchSymbolScale;
    }

    public double CabinetPadding { get; }
    public double StandardIntervalWidth { get; }
    public double StandardIntervalHeight { get; }
    public double BusbarOffset { get; }
    public double BusbarHeight { get; }
    public double IntervalSpacing { get; }
    public DocumentPoint CabinetNameOffset { get; }
    public double DeviceVerticalSpacing { get; }
    public double SwitchSymbolScale { get; }
}

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
    double LogicalHitPadding,
    double CableTerminalExitMinimumStubLength);

public sealed record LineDrawingMetrics(
    double ConnectionThickness,
    double CableDashLength,
    double CableDashGap,
    double GroundingAccessMarkerDiameter,
    double GroundingAccessClearance,
    double GroundingAccessHitPadding);

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
