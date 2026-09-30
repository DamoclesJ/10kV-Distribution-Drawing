using System.Windows.Media;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class EnergizationOverlayTests
{
    [Fact]
    public void ExactConnectionIdentityControlsCableAndOhlSegments()
    {
        Guid connection = Guid.NewGuid();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ElectricalVisualIdentity identity = ElectricalVisualIdentity.Edge(
            ElectricalConnectivityEdgeType.Connection, connection, first, second);
        var cable = new SceneLine(new DocumentPoint(0, 0), new DocumentPoint(20, 0),
            Colors.Black, 0.8, SceneStrokeStyle.Dashed) { ElectricalIdentity = identity };
        var ohl = new SceneLine(new DocumentPoint(0, 10), new DocumentPoint(20, 10),
            Colors.Black, 0.8) { ElectricalIdentity = identity };
        DrawingScene scene = new([cable, ohl]);
        EnergizationResult result = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState> { [first] = EnergizationState.Energized,
                [second] = EnergizationState.Energized },
            edges: [new ElectricalConnectivityEdge(first, second,
                ElectricalConnectivityEdgeType.Connection, connection)]);

        SceneLine[] overlay = EnergizationOverlayBuilder.Build(scene, result)
            .OfType<SceneLine>().ToArray();

        Assert.Equal(2, overlay.Length);
        Assert.All(overlay, line =>
        {
            Assert.Equal(ElectricalVisualState.Energized, line.ElectricalState);
            Assert.Null(line.TargetId);
            Assert.Null(line.HitTestBounds);
        });
        Assert.Equal(SceneStrokeStyle.Dashed, cable.StrokeStyle);
        Assert.Equal(SceneStrokeStyle.Solid, ohl.StrokeStyle);
        Assert.NotEqual(cable.Start, overlay[0].Start);
    }

    [Fact]
    public void EdgeSourceAloneCannotColorDifferentTerminalPair()
    {
        Guid source = Guid.NewGuid();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Guid third = Guid.NewGuid();
        EnergizationResult result = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState> { [first] = EnergizationState.Energized,
                [second] = EnergizationState.Energized,
                [third] = EnergizationState.Energized },
            edges: [new ElectricalConnectivityEdge(first, second,
                ElectricalConnectivityEdgeType.Connection, source)]);

        Assert.Equal(ElectricalVisualState.Unknown, EnergizationOverlayBuilder.Resolve(
            ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection,
                source, first, third), result));
    }

    [Fact]
    public void NodeBoundBusAndTerminationPathUseNodeResult()
    {
        Guid node = Guid.NewGuid();
        ElectricalVisualIdentity identity = ElectricalVisualIdentity.Node(node);
        var result = new EnergizationResult(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>(),
            new Dictionary<Guid, EnergizationPointResult>
            {
                [node] = new(EnergizationState.Energized, [])
            }, [], [], []);
        var scene = new DrawingScene([
            new SceneLine(new DocumentPoint(0, 0), new DocumentPoint(20, 0),
                Colors.Black, 1) { ElectricalIdentity = identity },
            new SceneLine(new DocumentPoint(30, 0), new DocumentPoint(34, 0),
                Colors.Black, 1) { ElectricalIdentity = identity }
        ]);

        Assert.All(EnergizationOverlayBuilder.Build(scene, result), element =>
            Assert.Equal(ElectricalVisualState.Energized, element.ElectricalState));
    }

    [Fact]
    public void OpenSwitchLeadsUseIndependentTerminalStatesAndGroundBranchIsUnbound()
    {
        RingCabinet cabinet = Cabinet();
        SwitchDevice load = cabinet.Intervals[0].SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.LoadSwitch);
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet,
            new RingCabinetLayoutFactory().Create(cabinet, new DocumentPoint(0, 0)));
        SceneLine firstLead = Assert.Single(elements.OfType<SceneLine>(), line =>
            line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(load.FirstTerminalId));
        SceneLine secondLead = Assert.Single(elements.OfType<SceneLine>(), line =>
            line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(load.SecondTerminalId));
        EnergizationResult result = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState>
            {
                [load.FirstTerminalId] = EnergizationState.Energized,
                [load.SecondTerminalId] = EnergizationState.Deenergized
            });

        Assert.NotEqual(firstLead.Start, secondLead.Start);
        Assert.Equal(ElectricalVisualState.Energized,
            EnergizationOverlayBuilder.Resolve(firstLead.ElectricalIdentity!, result));
        Assert.Equal(ElectricalVisualState.Deenergized,
            EnergizationOverlayBuilder.Resolve(secondLead.ElectricalIdentity!, result));
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            line.ElectricalIdentity is null && line.Start.YMillimeters == line.End.YMillimeters);
    }

    [Fact]
    public void UnknownUsesDifferentPatternAndNeverBecomesDeenergizedBeforeComplete()
    {
        Guid terminal = Guid.NewGuid();
        ElectricalVisualIdentity identity = ElectricalVisualIdentity.Terminal(terminal);
        EnergizationResult result = Result(EnergizationValidity.ForwardOnly,
            new Dictionary<Guid, EnergizationState> { [terminal] = EnergizationState.Deenergized });
        Assert.Equal(ElectricalVisualState.Unknown,
            EnergizationOverlayBuilder.Resolve(identity, result));
        Assert.NotEqual(EnergizationVisualStyleResolver.Resolve(ElectricalVisualState.Unknown).StrokeStyle,
            EnergizationVisualStyleResolver.Resolve(ElectricalVisualState.Deenergized).StrokeStyle);
    }

    [Fact]
    public void SelectionRemainsTopmostAndHitTestIdentityIsUnchanged()
    {
        Guid terminal = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        var target = new SelectionReference(SelectionTargetKind.Connection, targetId);
        var hitIndex = new SelectionHitTestIndex([
            new SelectionHitTestEntry(target, new DocumentRect(0, 0, 20, 3), 30)]);
        DrawingScene scene = new([
            new SceneLine(new DocumentPoint(0, 1), new DocumentPoint(20, 1),
                Colors.Black, 0.8)
            {
                ElectricalIdentity = ElectricalVisualIdentity.Terminal(terminal)
            }], hitIndex);
        EnergizationResult result = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState>
            {
                [terminal] = EnergizationState.Energized
            });
        SceneElement[] composed = [..scene.Elements,
            ..EnergizationOverlayBuilder.Build(scene, result),
            ..SelectionOverlayBuilder.CreateElements(hitIndex, target)];

        Assert.IsType<SceneRectangle>(composed[^1]);
        Assert.Null(composed[^2].HitTestBounds);
        Assert.Equal(target, hitIndex.HitTest(new DocumentPoint(10, 1)));
    }

    private static EnergizationResult Result(EnergizationValidity validity,
        IReadOnlyDictionary<Guid, EnergizationState> terminals,
        IEnumerable<ElectricalConnectivityEdge>? edges = null) => new(validity,
        terminals.ToDictionary(item => item.Key,
            item => new EnergizationPointResult(item.Value, [])),
        new Dictionary<Guid, EnergizationPointResult>(), [], edges ?? [], []);

    private static RingCabinet Cabinet()
    {
        var template = new RingCabinetTemplate(
            new TemplateId("test:ea:rendering"), "EA cabinet", RingCabinetTemplateType.Mixed,
            [new BayTemplate(1, new LoadSwitchConfiguration())],
            RingCabinetLayoutRule.Default, NoSecondaryConfiguration.Instance);
        RingCabinetDomainBuildOutcome outcome =
            new RingCabinetTemplateDomainBuilder().Build(template, "EA cabinet");
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result).Cabinet;
    }
}
