using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class OptionalCableTerminalPresentationTests
{
    [Theory]
    [InlineData(IntervalKind.LoadSwitchInterval)]
    [InlineData(IntervalKind.IntegratedFeederInterval)]
    public void RenderingAndAnchor_FollowCableTerminalPresence(IntervalKind kind)
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval,
            RingCabinetLayout layout) = CreateScenario(kind);
        Guid terminalId = interval.CableTerminalId!.Value;
        var renderer = new RingCabinetRenderer();

        IReadOnlyList<SceneElement> present = renderer.Render(cabinet, layout);
        TerminalAnchorIndex presentAnchors = TerminalAnchorIndex.Build(
            document,
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout });
        Assert.Equal(2, present.OfType<ScenePolyline>().Count(IsCableTerminalMarker));
        Assert.True(presentAnchors.TryGet(terminalId, out _));

        cabinet.SetIntervalCableTerminal(interval.IntervalId, null);
        document.SynchronizeRingCabinetAggregate(cabinet);

        IReadOnlyList<SceneElement> absent = renderer.Render(cabinet, layout);
        TerminalAnchorIndex absentAnchors = TerminalAnchorIndex.Build(
            document,
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout });
        Assert.Equal(1, absent.OfType<ScenePolyline>().Count(IsCableTerminalMarker));
        Assert.False(absentAnchors.TryGet(terminalId, out _));
        Assert.NotEmpty(absent.OfType<SceneLine>());
    }

    [Fact]
    public void Inspector_ShowsPresenceForSupportedIntervalAndOmitsItForPT()
    {
        (_, RingCabinet cabinet, RingCabinetInterval interval, RingCabinetLayout layout) =
            CreateScenario(IntervalKind.LoadSwitchInterval);
        PropertyInspectorSnapshot present = Project(cabinet, interval, layout);
        Assert.Equal("有", FindCableTerminalRow(present).DisplayValue);

        cabinet.SetIntervalCableTerminal(interval.IntervalId, null);
        RingCabinetInterval absentInterval = cabinet.Intervals.Single(item =>
            item.IntervalId == interval.IntervalId);
        PropertyInspectorSnapshot absent = Project(cabinet, absentInterval, layout);
        Assert.Equal("无", FindCableTerminalRow(absent).DisplayValue);

        (_, RingCabinet ptCabinet, RingCabinetInterval pt, RingCabinetLayout ptLayout) =
            CreateScenario(IntervalKind.PTInterval);
        PropertyInspectorSnapshot ptSnapshot = Project(ptCabinet, pt, ptLayout);
        Assert.DoesNotContain(
            ptSnapshot.Sections.SelectMany(section => section.Properties),
            row => row.PropertyKey ==
                PropertyCommandFactory.IntervalCableTerminalPresencePropertyKey);
    }

    private static PropertyInspectorSnapshot Project(
        RingCabinet cabinet,
        RingCabinetInterval interval,
        RingCabinetLayout layout) => new PropertyProjector().Project(new ResolvedSelection
    {
        Reference = new SelectionReference(
            SelectionTargetKind.RingCabinetInterval,
            interval.IntervalId,
            cabinet.Id),
        RingCabinet = cabinet,
        RingCabinetInterval = interval,
        RingCabinetLayout = layout,
        RingCabinetIntervalLayout = layout.IntervalLayouts[interval.IntervalId]
    });

    private static PropertyRowViewModel FindCableTerminalRow(
        PropertyInspectorSnapshot snapshot) => snapshot.Sections
        .SelectMany(section => section.Properties)
        .Single(row => row.PropertyKey ==
            PropertyCommandFactory.IntervalCableTerminalPresencePropertyKey);

    private static bool IsCableTerminalMarker(ScenePolyline polyline) =>
        polyline.IsClosed && polyline.Points.Count == 3;

    private static (DrawingDocument, RingCabinet, RingCabinetInterval, RingCabinetLayout)
        CreateScenario(IntervalKind kind)
    {
        RingCabinetIntervalDefinition definition = kind switch
        {
            IntervalKind.LoadSwitchInterval => RingCabinetIntervalDefinition.CreateLoadSwitch(
                1, SwitchState.Open, SwitchState.Open),
            IntervalKind.IntegratedFeederInterval =>
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    1,
                    GroundingStructureKind.UpperLowerGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open),
            IntervalKind.PTInterval => RingCabinetIntervalDefinition.CreatePT(
                1, SwitchState.Open, SwitchState.Open),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        IReadOnlyList<RingCabinetIntervalDefinition> definitions = kind == IntervalKind.PTInterval
            ? [definition]
            : [
                definition,
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    2, SwitchState.Open, SwitchState.Open)
            ];
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "Cabinet", definitions));
        var document = new DrawingDocument(Guid.NewGuid(), "Presentation");
        document.AddDevice(cabinet);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        return (document, cabinet, cabinet.Intervals[0], layout);
    }
}
