using System.Windows.Media;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class EnergizationNativeRenderingTests
{
    [Fact]
    public void IndependentOhlConnectionSegmentsDoNotPropagateColorByTouchingGeometry()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), third = Guid.NewGuid(), fourth = Guid.NewGuid();
        Guid edgeA = Guid.NewGuid(), edgeB = Guid.NewGuid();
        var scene = new DrawingScene([
            new SceneLine(new(0, 0), new(10, 0), Colors.Black, 0.8)
            { TargetId = edgeA, ElectricalIdentity = ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection, edgeA, first, second) },
            new SceneLine(new(10, 0), new(20, 0), Colors.Black, 0.8)
            { TargetId = edgeB, ElectricalIdentity = ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection, edgeB, third, fourth) }
        ]);
        var result = Result([first, second], [], EnergizationState.Energized,
            [new ElectricalConnectivityEdge(first, second, ElectricalConnectivityEdgeType.Connection, edgeA),
             new ElectricalConnectivityEdge(third, fourth, ElectricalConnectivityEdgeType.Connection, edgeB)],
            new Dictionary<Guid, EnergizationState> { [third] = EnergizationState.Deenergized, [fourth] = EnergizationState.Deenergized });
        var styled = EnergizationSceneStyler.Build(scene, result);
        Assert.Equal(ElectricalVisualState.Energized, styled[0].ElectricalState);
        Assert.Equal(ElectricalVisualState.Deenergized, styled[1].ElectricalState);
        Assert.Equal(edgeA, styled[0].TargetId);
        Assert.Equal(edgeB, styled[1].TargetId);
        Assert.Equal(((SceneLine)scene.Elements[0]).End, ((SceneLine)styled[0]).End);
        Assert.Equal(((SceneLine)scene.Elements[1]).Start, ((SceneLine)styled[1]).Start);
    }

    [Fact]
    public void DocumentPoleAndNumberUseExplicitOhlSupportAssociationWithoutProximityGuessing()
    {
        var first = new PoleCreationFactory().Create("P-A");
        var second = new PoleCreationFactory().Create("P-B");
        var document = new DrawingDocument(Guid.NewGuid(), "Explicit support");
        var drawingLayout = new DrawingLayout();
        foreach (var creation in new[] { first, second })
        {
            document.AddDevice(creation.Pole);
            foreach (var terminal in creation.Terminals) document.AddTerminal(terminal);
        }
        drawingLayout.Add(new PoleLayout(first.Pole.Id, new(0, 0)));
        drawingLayout.Add(new PoleLayout(second.Pole.Id, new(100, 50)));
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            first.Pole.OverheadAnchorTerminalIds.Single(), second.Pole.OverheadAnchorTerminalIds.Single(), "OHL", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "OHL", [first.Pole.Id, second.Pole.Id]));
        drawingLayout.Add(new OverheadLineLayout(connection.Id, new(0, 0), new(100, 50)));
        var scene = new DrawingSceneBuilder().Build(document, new RuntimeLayoutDocument(drawingLayout, new Dictionary<Guid, RingCabinetLayout>()));
        var edge = ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection, connection.Id,
            connection.StartTerminalId, connection.EndTerminalId);
        foreach (var pole in new[] { first.Pole, second.Pole })
        {
            var identity = ElectricalVisualIdentity.Association(pole.Id, [edge]);
            Assert.Equal(identity, Assert.Single(scene.Elements.OfType<SceneText>(), text => text.Text == pole.PoleNumber).ElectricalIdentity);
            Assert.Contains(scene.Elements.OfType<SceneEllipse>(), ellipse => ellipse.ElectricalIdentity == identity);
        }
        var styled = EnergizationSceneStyler.Build(scene, Result([connection.StartTerminalId, connection.EndTerminalId], [],
            EnergizationState.Energized, [new ElectricalConnectivityEdge(connection.StartTerminalId, connection.EndTerminalId,
                ElectricalConnectivityEdgeType.Connection, connection.Id)]));
        Assert.All(styled.Where(element => element.ElectricalIdentity is not null), element => Assert.Equal(ElectricalVisualState.Energized, element.ElectricalState));
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding)]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void IntegratedCabinetInternalConductorsAndOpenSwitchPartsKeepSeparateTerminalBindings(GroundingStructureKind structure)
    {
        var template = new RingCabinetTemplate(new TemplateId("test:ea:integrated"), "Integrated", RingCabinetTemplateType.Mixed,
            [new BayTemplate(1, new IntegratedFeederConfiguration(structure)), new BayTemplate(2, new LoadSwitchConfiguration())],
            RingCabinetLayoutRule.Default, NoSecondaryConfiguration.Instance);
        var outcome = new RingCabinetTemplateDomainBuilder().Build(template, "Integrated");
        Assert.True(outcome.IsSuccess, outcome.Failure?.Message);
        var cabinet = outcome.Result!.Cabinet;
        var elements = new RingCabinetRenderer().Render(cabinet, new RingCabinetLayoutFactory().Create(cabinet, new(0, 0)));
        foreach (var device in cabinet.Intervals[0].SwitchDevices.Where(device => device.SwitchKind != SwitchKind.GroundSwitch))
        {
            Assert.Contains(elements.OfType<SceneLine>(), line => line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.FirstTerminalId));
            Assert.Contains(elements.OfType<SceneLine>(), line => line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.SecondTerminalId));
            Assert.DoesNotContain(elements, element => element.ElectricalIdentity?.Kind == ElectricalVisualIdentityKind.Edge && element.ElectricalIdentity.Id == device.Id);
        }
        var ground = cabinet.Intervals[0].SwitchDevices.Single(device => device.SwitchKind == SwitchKind.GroundSwitch);
        Assert.DoesNotContain(elements, element => element.ElectricalIdentity == ElectricalVisualIdentity.Terminal(ground.FirstTerminalId) ||
            element.ElectricalIdentity == ElectricalVisualIdentity.Terminal(ground.SecondTerminalId));
    }

    [Theory]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    public void ClosedSwitchConductingPathRequiresExactAnalyzerEdge(SwitchKind kind)
    {
        var device = SwitchDevice.CreateForPole(Guid.NewGuid(), kind, Guid.NewGuid(), Guid.NewGuid(), SwitchState.Closed);
        var context = new SymbolRenderContext(new(0, 0), 14, 14, state: SymbolVisualState.Closed)
        { ElectricalSwitch = device };
        var scene = new DrawingScene(new SymbolLibrary().Create(SymbolLibrary.ResolveAttachmentKind(device), context));
        var path = ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.ClosedSwitch,
            device.Id, device.FirstTerminalId, device.SecondTerminalId);
        Assert.Contains(scene.Elements.OfType<SceneLine>(), line => line.ElectricalIdentity == path);
        var result = Result(device.TerminalIds, [], EnergizationState.Energized,
            [new ElectricalConnectivityEdge(device.FirstTerminalId, device.SecondTerminalId, ElectricalConnectivityEdgeType.ClosedSwitch, device.Id)]);
        var styled = EnergizationSceneStyler.Build(scene, result);
        Assert.All(styled.Where(element => element.ElectricalIdentity is not null), element =>
            Assert.Equal(ElectricalVisualState.Energized, element.ElectricalState));
        Assert.Equal(ElectricalVisualState.Unknown, EnergizationSceneStyler.Resolve(path,
            Result(device.TerminalIds, [], EnergizationState.Energized)));
    }

    [Fact]
    public void NativeStylingPreservesEveryPrimitiveGeometryThicknessAndHitIdentity()
    {
        Guid terminal = Guid.NewGuid();
        var identity = ElectricalVisualIdentity.Terminal(terminal);
        SceneElement[] originals = [
            new SceneLine(new(0, 0), new(20, 0), Colors.Black, 0.8, SceneStrokeStyle.Dashed),
            new SceneEllipse(new(0, 2, 4, 4), Colors.Black, 0.6, Colors.White),
            new SceneArc(new(10, 10), 3, 0, 180, Colors.Black, 0.7),
            new ScenePolyline([new(0, 0), new(1, 1), new(2, 0)], true, Colors.Black, 0.4, Colors.White),
            new SceneRectangle(new(20, 20, 5, 6), Colors.Black, 0.5, Colors.White),
            new SceneText(new(10, 20), "owned label", Colors.Black, 3),
            new SceneText(new(50, 50), "unrelated annotation", Colors.Black, 3)
        ];
        var scene = new DrawingScene(originals.Select((element, index) => index == 6 ? element :
            element with { ElectricalIdentity = identity, TargetId = terminal, HitTestBounds = new(0, 0, 20, 20) }));
        var styled = EnergizationSceneStyler.Build(scene, Result([terminal], [], EnergizationState.Energized));
        Assert.Equal(scene.Elements.Count, styled.Count);
        for (int index = 0; index < styled.Count - 1; index++)
        {
            Assert.Equal(identity, styled[index].ElectricalIdentity);
            Assert.Equal(terminal, styled[index].TargetId);
            Assert.Equal(scene.Elements[index].HitTestBounds, styled[index].HitTestBounds);
            Assert.Equal(ElectricalVisualState.Energized, styled[index].ElectricalState);
        }
        Assert.Equal(((SceneLine)scene.Elements[0]) with { Stroke = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[0]);
        Assert.Equal(((SceneEllipse)scene.Elements[1]) with { Stroke = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[1]);
        Assert.Equal(((SceneArc)scene.Elements[2]) with { Stroke = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[2]);
        Assert.Equal(((ScenePolyline)scene.Elements[3]) with { Stroke = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[3]);
        Assert.Equal(((SceneRectangle)scene.Elements[4]) with { Stroke = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[4]);
        Assert.Equal(((SceneText)scene.Elements[5]) with { Foreground = Colors.Red, ElectricalState = ElectricalVisualState.Energized }, styled[5]);
        Assert.Same(scene.Elements[6], styled[6]);
    }

    [Theory]
    [InlineData(EnergizationState.Energized)]
    [InlineData(EnergizationState.Deenergized)]
    [InlineData(EnergizationState.Unknown)]
    public void PtCoilsAndLabelReadTheIsolationLoadTerminalWithoutAddingSources(EnergizationState state)
    {
        RingCabinet cabinet = Cabinet(new PTConfiguration());
        SwitchDevice isolation = cabinet.Intervals[1].SwitchDevices.Single(device => device.SwitchKind == SwitchKind.IsolationSwitch);
        var scene = new DrawingScene(new RingCabinetRenderer().Render(cabinet, new RingCabinetLayoutFactory().Create(cabinet, new(0, 0))));
        SceneText label = Assert.Single(scene.Elements.OfType<SceneText>(), text => text.Text == "PT");
        var identity = ElectricalVisualIdentity.Terminal(isolation.SecondTerminalId);
        Assert.Equal(identity, label.ElectricalIdentity);
        SceneEllipse[] coils = scene.Elements.OfType<SceneEllipse>().Where(ellipse => ellipse.ElectricalIdentity == identity).ToArray();
        Assert.Equal(2, coils.Length);
        var result = Result([isolation.SecondTerminalId], [], state);
        var styled = EnergizationSceneStyler.Build(scene, result);
        Assert.All(styled.Where(element => element.ElectricalIdentity == identity), element =>
            Assert.Equal((ElectricalVisualState)((int)state + 1), element.ElectricalState));
        Assert.All(result.Terminals.Values, point => Assert.Empty(point.EnergizedBy));
    }

    [Fact]
    public void OpenIsolationKeepsPtVisualOnItsLoadTerminalWhenBusSideIsEnergized()
    {
        RingCabinet cabinet = Cabinet(new PTConfiguration());
        SwitchDevice isolation = cabinet.Intervals[1].SwitchDevices.Single(device => device.SwitchKind == SwitchKind.IsolationSwitch);
        Assert.Equal(SwitchState.Open, isolation.SwitchState);
        var scene = new DrawingScene(new RingCabinetRenderer().Render(cabinet,
            new RingCabinetLayoutFactory().Create(cabinet, new(0, 0))));
        var result = Result([isolation.FirstTerminalId], [cabinet.MainBusNodeId],
            EnergizationState.Energized, extra: new Dictionary<Guid, EnergizationState>
            {
                [isolation.SecondTerminalId] = EnergizationState.Deenergized
            });

        IReadOnlyList<SceneElement> styled = EnergizationSceneStyler.Build(scene, result);
        SceneText label = Assert.Single(styled.OfType<SceneText>(), text => text.Text == "PT");
        SceneEllipse[] coils = styled.OfType<SceneEllipse>()
            .Where(ellipse => ellipse.ElectricalIdentity == ElectricalVisualIdentity.Terminal(isolation.SecondTerminalId))
            .ToArray();
        SceneText cabinetName = Assert.Single(styled.OfType<SceneText>(), text => text.Text == cabinet.DisplayName);

        Assert.Equal(ElectricalVisualIdentity.Terminal(isolation.SecondTerminalId), label.ElectricalIdentity);
        Assert.Equal(ElectricalVisualState.Deenergized, label.ElectricalState);
        Assert.Equal(2, coils.Length);
        Assert.All(coils, coil => Assert.Equal(ElectricalVisualState.Deenergized, coil.ElectricalState));
        Assert.Equal(ElectricalVisualState.Energized, cabinetName.ElectricalState);
        Assert.All(result.Terminals.Values, point => Assert.Empty(point.EnergizedBy));
    }

    [Fact]
    public void CabinetBusAndNamesReadBusNodeWhileIntervalNumberReadsItsOwnLineTerminal()
    {
        RingCabinet cabinet = Cabinet(new LoadSwitchConfiguration());
        var scene = new DrawingScene(new RingCabinetRenderer().Render(cabinet, new RingCabinetLayoutFactory().Create(cabinet, new(0, 0))));
        var bus = ElectricalVisualIdentity.Node(cabinet.MainBusNodeId);
        Assert.Contains(scene.Elements.OfType<SceneText>(), text => text.Text == cabinet.DisplayName && text.ElectricalIdentity == bus);
        Assert.Contains(scene.Elements.OfType<SceneLine>(), line => line.ElectricalIdentity == bus);
        foreach (var interval in cabinet.Intervals)
        {
            var load = interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
            Assert.Contains(scene.Elements.OfType<SceneText>(), text => text.Text == interval.BusinessNumber &&
                text.ElectricalIdentity == ElectricalVisualIdentity.Terminal(load.SecondTerminalId));
        }
        var styled = EnergizationSceneStyler.Build(scene, Result([], [cabinet.MainBusNodeId], EnergizationState.Energized));
        Assert.All(styled.Where(element => element.ElectricalIdentity == bus), element => Assert.Equal(ElectricalVisualState.Energized, element.ElectricalState));
    }

    [Theory]
    [InlineData(SwitchKind.IsolationSwitch, 0)]
    [InlineData(SwitchKind.IsolationSwitch, 1)]
    [InlineData(SwitchKind.LoadSwitch, 2)]
    [InlineData(SwitchKind.CircuitBreaker, 3)]
    [InlineData(SwitchKind.DropoutFuse, 1)]
    public void PoleSwitchSymbolsRetainIndependentTerminalPartsThroughRotation(SwitchKind kind, int rotation)
    {
        var creation = new PoleCreationFactory().CreateWithAttachments("P", PoleType.Cement, null, [kind], false);
        var device = Assert.Single(creation.Devices.OfType<SwitchDevice>());
        var attachment = Assert.Single(creation.Attachments);
        var elements = new SymbolLibrary().CreateAttachment(attachment, device,
            new PoleLayout(creation.Pole.Id, new(20, 20)),
            new AttachmentLayout(attachment.AttachmentId, new(15, 0)).RotateBy(rotation), includeLabel: false);
        Assert.Contains(elements.OfType<SceneLine>(), line => line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.FirstTerminalId));
        Assert.Contains(elements.OfType<SceneLine>(), line => line.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.SecondTerminalId));
        var result = Result([device.FirstTerminalId], [], EnergizationState.Energized,
            extra: new Dictionary<Guid, EnergizationState> { [device.SecondTerminalId] = EnergizationState.Deenergized });
        var styled = EnergizationSceneStyler.Build(new DrawingScene(elements), result);
        Assert.Contains(styled, element => element.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.FirstTerminalId) && element.ElectricalState == ElectricalVisualState.Energized);
        Assert.Contains(styled, element => element.ElectricalIdentity == ElectricalVisualIdentity.Terminal(device.SecondTerminalId) && element.ElectricalState == ElectricalVisualState.Deenergized);
    }

    [Theory]
    [InlineData(EnergizationState.Energized)]
    [InlineData(EnergizationState.Deenergized)]
    [InlineData(EnergizationState.Unknown)]
    public void PoleSymbolAndPoleNumberFollowRegisteredAnchorIdentity(EnergizationState state)
    {
        var creation = new PoleCreationFactory().Create("P-09");
        var scene = new DrawingScene(new MixedPoleRenderer().Render(creation.Pole,
            new PoleLayout(creation.Pole.Id, new(20, 20)), [], []));
        var identity = ElectricalVisualIdentity.Association(creation.Pole.Id,
            creation.Pole.OverheadAnchorTerminalIds.Select(ElectricalVisualIdentity.Terminal));
        Assert.Equal(identity, Assert.Single(scene.Elements.OfType<SceneText>()).ElectricalIdentity);
        Assert.Contains(scene.Elements.OfType<SceneEllipse>(), ellipse => ellipse.ElectricalIdentity == identity);
        var styled = EnergizationSceneStyler.Build(scene, Result(creation.Pole.OverheadAnchorTerminalIds, [], state));
        Assert.All(styled.Where(element => element.ElectricalIdentity == identity), element =>
            Assert.Equal((ElectricalVisualState)((int)state + 1), element.ElectricalState));
    }

    [Fact]
    public void UnknownPreservesCableDashSemanticsAndMarksTextWithoutChangingLabelContent()
    {
        var identity = ElectricalVisualIdentity.Terminal(Guid.NewGuid());
        var scene = new DrawingScene([
            new SceneLine(new(0, 0), new(20, 0), Colors.Black, 0.8, SceneStrokeStyle.Dashed) { ElectricalIdentity = identity },
            new SceneEllipse(new(20, 0, 5, 5), Colors.Black, 0.8) { ElectricalIdentity = identity },
            new SceneText(new(0, 4), "P-unknown", Colors.Black, 3) { ElectricalIdentity = identity }
        ]);
        var styled = EnergizationSceneStyler.Build(scene, Result([], [], EnergizationState.Unknown));
        Assert.Equal(SceneStrokeStyle.DashDot, ((SceneLine)styled[0]).StrokeStyle);
        Assert.Equal(SceneStrokeStyle.Dotted, ((SceneEllipse)styled[1]).StrokeStyle);
        Assert.Equal("P-unknown", ((SceneText)styled[2]).Text);
        Assert.Equal(ElectricalVisualState.Unknown, styled[2].ElectricalState);
        // Actual WPF rendering adds a non-color text cue and keeps native conductor geometry.
        DrawingGroup drawing = new DrawingSceneRenderer().RenderDrawing(new DrawingScene(styled), 1);
        Assert.True(drawing.Children.Count > styled.Count);
        Assert.Equal(scene.Elements.Count, styled.Count);
    }

    [Fact]
    public void AssociationRequiresEveryMemberToAgreeAndNeverUsesAnySideEnergized()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        var identity = ElectricalVisualIdentity.Association(Guid.NewGuid(), [ElectricalVisualIdentity.Terminal(first), ElectricalVisualIdentity.Terminal(second)]);
        Assert.Equal(ElectricalVisualState.Unknown, EnergizationSceneStyler.Resolve(identity,
            Result([first], [], EnergizationState.Energized, extra: new Dictionary<Guid, EnergizationState> { [second] = EnergizationState.Deenergized })));
        Assert.Equal(ElectricalVisualState.Energized, EnergizationSceneStyler.Resolve(identity, Result([first, second], [], EnergizationState.Energized)));
        Assert.Equal(ElectricalVisualState.Unknown, EnergizationSceneStyler.Resolve(ElectricalVisualIdentity.Association(Guid.NewGuid(), []), Result([], [], EnergizationState.Energized)));
    }

    [Fact]
    public void PoleAndCableTerminationAggregatesBecomeUnknownForMixedOrMissingMembers()
    {
        Guid poleId = Guid.NewGuid();
        Guid cableSide = Guid.NewGuid(), overheadSide = Guid.NewGuid(), internalNode = Guid.NewGuid();
        Guid edgeAId = Guid.NewGuid(), edgeBId = Guid.NewGuid();
        Guid edgeAFirst = Guid.NewGuid(), edgeASecond = Guid.NewGuid();
        Guid edgeBFirst = Guid.NewGuid(), edgeBSecond = Guid.NewGuid();
        var pole = ElectricalVisualIdentity.Association(poleId, [
            ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection, edgeAId, edgeAFirst, edgeASecond),
            ElectricalVisualIdentity.Edge(ElectricalConnectivityEdgeType.Connection, edgeBId, edgeBFirst, edgeBSecond)]);
        var termination = new CableTermination(Guid.NewGuid(), cableSide, overheadSide, internalNode, "CT");
        var terminationIdentity = ElectricalVisualIdentity.Association(termination.Id, [
            ElectricalVisualIdentity.Terminal(termination.CableSideTerminalId),
            ElectricalVisualIdentity.Node(termination.InternalNodeId),
            ElectricalVisualIdentity.Terminal(termination.OverheadSideTerminalId)]);
        var result = new EnergizationResult(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>
            {
                [edgeAFirst] = new(EnergizationState.Energized, []),
                [edgeASecond] = new(EnergizationState.Energized, []),
                [edgeBFirst] = new(EnergizationState.Deenergized, []),
                [edgeBSecond] = new(EnergizationState.Deenergized, []),
                [cableSide] = new(EnergizationState.Energized, []),
                [overheadSide] = new(EnergizationState.Energized, [])
            },
            new Dictionary<Guid, EnergizationPointResult>
            {
                [internalNode] = new(EnergizationState.Deenergized, [])
            }, [], [
                new ElectricalConnectivityEdge(edgeAFirst, edgeASecond, ElectricalConnectivityEdgeType.Connection, edgeAId),
                new ElectricalConnectivityEdge(edgeBFirst, edgeBSecond, ElectricalConnectivityEdgeType.Connection, edgeBId)
            ], []);
        var styled = EnergizationSceneStyler.Build(new DrawingScene([
            new SceneEllipse(new(0, 0, 5, 5), Colors.Black, 0.5) { ElectricalIdentity = pole },
            new ScenePolyline([new(0, 0), new(5, 0), new(5, 5)], true, Colors.Black, 0.5) { ElectricalIdentity = terminationIdentity },
            new SceneText(new(0, 0), "P-01", Colors.Black, 3) { ElectricalIdentity = pole }
        ]), result);

        Assert.All(styled, element => Assert.Equal(ElectricalVisualState.Unknown, element.ElectricalState));

        var missingTerminationState = new EnergizationResult(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>
            {
                [cableSide] = new(EnergizationState.Energized, []),
                [overheadSide] = new(EnergizationState.Energized, [])
            }, new Dictionary<Guid, EnergizationPointResult>(), [], [], []);
        Assert.Equal(ElectricalVisualState.Unknown,
            EnergizationSceneStyler.Resolve(terminationIdentity, missingTerminationState));
    }

    private static RingCabinet Cabinet(BayEquipmentConfiguration second)
    {
        var template = new RingCabinetTemplate(new TemplateId("test:ea:native"), "Native cabinet", RingCabinetTemplateType.Conventional,
            [new BayTemplate(1, new LoadSwitchConfiguration()), new BayTemplate(2, second)], RingCabinetLayoutRule.Default, NoSecondaryConfiguration.Instance);
        var outcome = new RingCabinetTemplateDomainBuilder().Build(template, "Native cabinet");
        Assert.True(outcome.IsSuccess, outcome.Failure?.Message);
        return outcome.Result!.Cabinet;
    }

    internal static EnergizationResult Result(IEnumerable<Guid> terminals, IEnumerable<Guid> nodes, EnergizationState state,
        IEnumerable<ElectricalConnectivityEdge>? edges = null, IReadOnlyDictionary<Guid, EnergizationState>? extra = null) =>
        new(EnergizationValidity.Complete,
            terminals.Select(id => new KeyValuePair<Guid, EnergizationState>(id, state)).Concat(extra ?? new Dictionary<Guid, EnergizationState>())
                .ToDictionary(item => item.Key, item => new EnergizationPointResult(item.Value, [])),
            nodes.ToDictionary(id => id, _ => new EnergizationPointResult(state, [])), [], edges ?? [], []);
}
