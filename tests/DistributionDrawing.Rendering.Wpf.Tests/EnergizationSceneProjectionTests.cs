using System.Windows.Media;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class EnergizationSceneProjectionTests
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

        SceneLine[] projected = EnergizationSceneProjector.Project(scene, Document(), result).Elements
            .OfType<SceneLine>().ToArray();

        Assert.Equal(2, projected.Length);
        Assert.All(projected, line =>
        {
            Assert.Equal(ElectricalVisualState.Energized, line.ElectricalState);
            Assert.Null(line.TargetId);
            Assert.Null(line.HitTestBounds);
        });
        Assert.Equal(SceneStrokeStyle.Dashed, cable.StrokeStyle);
        Assert.Equal(SceneStrokeStyle.Solid, ohl.StrokeStyle);
        Assert.Equal(cable.Start, projected[0].Start);
        Assert.Equal(cable.End, projected[0].End);
        Assert.Equal(ohl.Start, projected[1].Start);
        Assert.Equal(scene.Elements.Count, projected.Length);
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

        Assert.Equal(ElectricalVisualState.Unknown, EnergizationSceneProjector.Resolve(
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

        Assert.All(EnergizationSceneProjector.Project(scene, Document(), result).Elements, element =>
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
            EnergizationSceneProjector.Resolve(firstLead.ElectricalIdentity!, result));
        Assert.Equal(ElectricalVisualState.Deenergized,
            EnergizationSceneProjector.Resolve(secondLead.ElectricalIdentity!, result));
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
            EnergizationSceneProjector.Resolve(identity, result));
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
        SceneElement[] composed = [
            ..EnergizationSceneProjector.Project(scene, Document(), result).Elements,
            ..SelectionOverlayBuilder.CreateElements(hitIndex, target)];

        Assert.IsType<SceneRectangle>(composed[^1]);
        Assert.Equal(ElectricalVisualState.Energized, composed[^2].ElectricalState);
        Assert.Equal(target, hitIndex.HitTest(new DocumentPoint(10, 1)));
    }

    [Fact]
    public void PoleSymbolAndNumberUseOwnedAnchorTerminals()
    {
        Guid anchor = Guid.NewGuid();
        var pole = new Pole(Guid.NewGuid(), "P-1", overheadAnchorTerminalIds: [anchor]);
        DrawingDocument drawing = Document();
        drawing.AddDevice(pole);
        IReadOnlyList<SceneElement> elements = new SymbolLibrary().CreatePole(pole,
            new PoleLayout(pole.Id, new DocumentPoint(0, 0)));
        EnergizationResult result = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState> { [anchor] = EnergizationState.Energized });
        DrawingScene projected = EnergizationSceneProjector.Project(new DrawingScene(elements),
            drawing, result);

        Assert.Contains(projected.Elements.OfType<SceneEllipse>(), element =>
            element.ElectricalState == ElectricalVisualState.Energized);
        Assert.Contains(projected.Elements.OfType<SceneText>(), element =>
            element.Text == "P-1" &&
            element.ElectricalState == ElectricalVisualState.Energized);
    }

    [Fact]
    public void CableTerminationOutlineRequiresAgreementOfBothRealSides()
    {
        Guid cable = Guid.NewGuid();
        Guid overhead = Guid.NewGuid();
        var termination = new CableTermination(Guid.NewGuid(), cable, overhead,
            Guid.NewGuid());
        var pole = new Pole(Guid.NewGuid(), "P-1");
        var attachment = new PoleAttachment(Guid.NewGuid(), pole.Id, termination.Id);
        IReadOnlyList<SceneElement> icon = new SymbolLibrary().CreateAttachment(
            attachment, termination, new PoleLayout(pole.Id, new DocumentPoint(0, 0)),
            new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(18, 0)));
        Assert.Contains(icon.OfType<ScenePolyline>(), element =>
            element.ElectricalIdentity == ElectricalVisualIdentity.CableTermination(
                termination.Id));
        DrawingDocument drawing = Document();
        drawing.AddDevice(termination);
        ElectricalVisualIdentity identity = ElectricalVisualIdentity.CableTermination(
            termination.Id);
        EnergizationResult mixed = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState>
            {
                [cable] = EnergizationState.Energized,
                [overhead] = EnergizationState.Deenergized
            });

        Assert.Equal(ElectricalVisualState.Unknown,
            EnergizationSceneProjector.Resolve(identity, mixed, drawing));
        Assert.Equal(ElectricalVisualState.Energized,
            EnergizationSceneProjector.Resolve(identity,
                Result(EnergizationValidity.Complete,
                    new Dictionary<Guid, EnergizationState>
                    {
                        [cable] = EnergizationState.Energized,
                        [overhead] = EnergizationState.Energized
                    }), drawing));
    }

    [Fact]
    public void PtCoilsAndCabinetLabelsFollowTheirExplicitNodes()
    {
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "PT cabinet", [RingCabinetIntervalDefinition.CreatePT(
                7, SwitchState.Closed, SwitchState.Open, "PT interval")]));
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet,
            new RingCabinetLayoutFactory().Create(cabinet, new DocumentPoint(0, 0)));
        Assert.Contains(elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds.WidthMillimeters == DrawingMetrics.Default.PT.CoilRadius * 2 &&
            ellipse.ElectricalIdentity == ElectricalVisualIdentity.Node(interval.CircuitNodeId));
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text == "PT cabinet" &&
            text.ElectricalIdentity == ElectricalVisualIdentity.Node(cabinet.MainBusNodeId));
        Assert.Contains(elements.OfType<SceneText>(), text =>
            text.Text == "PT interval" &&
            text.ElectricalIdentity == ElectricalVisualIdentity.Node(interval.CircuitNodeId));
        DrawingDocument drawing = Document();
        drawing.AddDevice(cabinet);
        var result = new EnergizationResult(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>(),
            new Dictionary<Guid, EnergizationPointResult>
            {
                [cabinet.MainBusNodeId] = new(EnergizationState.Energized, []),
                [interval.CircuitNodeId] = new(EnergizationState.Deenergized, [])
            }, [], [], []);
        DrawingScene projected = EnergizationSceneProjector.Project(new DrawingScene(elements),
            drawing, result);
        Assert.Contains(projected.Elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds.WidthMillimeters == DrawingMetrics.Default.PT.CoilRadius * 2 &&
            ellipse.ElectricalState == ElectricalVisualState.Deenergized);
        Assert.Contains(projected.Elements.OfType<SceneText>(), text =>
            text.Text == "PT cabinet" &&
            text.ElectricalState == ElectricalVisualState.Energized);
    }

    [Fact]
    public void NativeRendererChangesOriginalStrokeWithoutAddingGeometry()
    {
        Guid terminal = Guid.NewGuid();
        var line = new SceneLine(new DocumentPoint(0, 0), new DocumentPoint(20, 0),
            Colors.Black, 0.8, SceneStrokeStyle.Dashed)
        {
            ElectricalIdentity = ElectricalVisualIdentity.Terminal(terminal)
        };
        DrawingScene projected = EnergizationSceneProjector.Project(new DrawingScene([line]),
            Document(), Result(EnergizationValidity.Complete,
                new Dictionary<Guid, EnergizationState>
                {
                    [terminal] = EnergizationState.Energized
                }));
        DrawingGroup drawing = new DrawingSceneRenderer().RenderDrawing(projected, 1);
        GeometryDrawing geometry = Assert.IsType<GeometryDrawing>(Assert.Single(drawing.Children));
        Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(geometry.Pen!.Brush).Color);
        Assert.NotNull(geometry.Pen.DashStyle);
        Assert.Single(projected.Elements);
        Assert.Equal(line.Start, Assert.IsType<SceneLine>(projected.Elements[0]).Start);
    }

    [Fact]
    public void OpenSwitchSidesStayIndependentAndClosedBladeUsesPreciseEdge()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Guid device = Guid.NewGuid();
        ElectricalVisualIdentity firstSide = ElectricalVisualIdentity.Terminal(first);
        ElectricalVisualIdentity secondSide = ElectricalVisualIdentity.Terminal(second);
        ElectricalVisualIdentity path = ElectricalVisualIdentity.Edge(
            ElectricalConnectivityEdgeType.ClosedSwitch, device, first, second);
        var definition = new SwitchSymbolDefinition(SymbolKind.LoadSwitch);
        var openContext = new SymbolRenderContext(new DocumentPoint(0, 0), 20, 12,
            state: SymbolVisualState.Open, includeLabel: false)
        {
            FirstSide = firstSide, SecondSide = secondSide, ConductingPath = path
        };
        IReadOnlyList<SceneElement> open = definition.Create(openContext);
        Assert.Contains(open, element => element.ElectricalIdentity == firstSide);
        Assert.Contains(open, element => element.ElectricalIdentity == secondSide);
        Assert.DoesNotContain(open, element => element.ElectricalIdentity == path);

        EnergizationResult separated = Result(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationState>
            {
                [first] = EnergizationState.Energized,
                [second] = EnergizationState.Deenergized
            });
        DrawingScene projected = EnergizationSceneProjector.Project(new DrawingScene(open),
            Document(), separated);
        Assert.Contains(projected.Elements, element => element.ElectricalState ==
            ElectricalVisualState.Energized);
        Assert.Contains(projected.Elements, element => element.ElectricalState ==
            ElectricalVisualState.Deenergized);

        IReadOnlyList<SceneElement> closed = definition.Create(new SymbolRenderContext(
            new DocumentPoint(0, 0), 20, 12, state: SymbolVisualState.Closed,
            includeLabel: false)
        {
            FirstSide = firstSide, SecondSide = secondSide, ConductingPath = path
        });
        Assert.Contains(closed, element => element.ElectricalIdentity == path);
    }

    [Fact]
    public void GroundSwitchEarthSideDoesNotAcquirePropagationIdentity()
    {
        ElectricalVisualIdentity deviceSide = ElectricalVisualIdentity.Terminal(Guid.NewGuid());
        IReadOnlyList<SceneElement> symbol = new SwitchSymbolDefinition(SymbolKind.GroundSwitch)
            .Create(new SymbolRenderContext(new DocumentPoint(0, 0), 20, 12,
                state: SymbolVisualState.Closed, includeLabel: false)
            {
                FirstSide = deviceSide
            });

        Assert.Contains(symbol, element => element.ElectricalIdentity == deviceSide);
        Assert.DoesNotContain(symbol, element =>
            element.ElectricalIdentity?.Kind == ElectricalVisualIdentityKind.Edge);
        Assert.Contains(symbol.OfType<SceneLine>(), element => element.ElectricalIdentity is null);
    }

    private static DrawingDocument Document() => new(Guid.NewGuid(), "EA visual test");

    private static EnergizationResult Result(EnergizationValidity validity,
        IReadOnlyDictionary<Guid, EnergizationState> terminals,
        IEnumerable<ElectricalConnectivityEdge>? edges = null) => new(validity,
        terminals.ToDictionary(item => item.Key,
            item => new EnergizationPointResult(item.Value, [])),
        new Dictionary<Guid, EnergizationPointResult>(), [], edges ?? [], []);

    private static RingCabinet Cabinet()
    {
        var template = new RingCabinetTemplate(
            new TemplateId("test:ea:rendering"), "EA cabinet", RingCabinetTemplateType.Conventional,
            [
                new BayTemplate(1, new LoadSwitchConfiguration()),
                new BayTemplate(2, new LoadSwitchConfiguration())
            ],
            RingCabinetLayoutRule.Default, NoSecondaryConfiguration.Instance);
        RingCabinetDomainBuildOutcome outcome =
            new RingCabinetTemplateDomainBuilder().Build(template, "EA cabinet");
        Assert.True(outcome.IsSuccess,
            outcome.Failure?.Cause?.ToString() ?? outcome.Failure?.Message);
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result).Cabinet;
    }
}
