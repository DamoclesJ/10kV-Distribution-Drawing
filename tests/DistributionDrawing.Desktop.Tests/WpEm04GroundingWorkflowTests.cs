using System.IO;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.GroundingAccessPointCreation;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WpEm04GroundingWorkflowTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void PoleWorkflow_ExposesOneOrMultipleRouteBasedCandidates()
    {
        Scenario scenario = CreateScenario();

        GroundingAccessCandidate endpoint = Assert.Single(
            GroundingAccessPointCreationService.GetCandidates(
                scenario.Session, scenario.Start.Pole.Id));
        GroundingAccessCandidate[] middle = GroundingAccessPointCreationService.GetCandidates(
            scenario.Session, scenario.Middle.Pole.Id).ToArray();

        Assert.Equal(2, middle.Length);
        Assert.Equal(scenario.Middle.Pole.Id, endpoint.AdjacentPoleId);
        Assert.All(middle, candidate => Assert.Contains(
            candidate.VisualDirection, new[] { "左侧", "右侧", "上侧", "下侧" }));
        Assert.Equal(
            new[] { scenario.Start.Pole.Id, scenario.End.Pole.Id }.OrderBy(id => id),
            middle.Select(candidate => candidate.AdjacentPoleId).OrderBy(id => id));
        OverheadLine line = Assert.Single(scenario.Session.PersistenceSession.Domain.OverheadLines);
        OrthogonalRoute route = Assert.Single(scenario.Session.Scene.Routes);
        foreach (GroundingAccessCandidate candidate in middle)
        {
            Assert.True(SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                scenario.Session.Layout.DrawingLayout,
                candidate.PoleId,
                candidate.AdjacentPoleId,
                out GroundingAccessHalfEdge halfEdge));
            Assert.Equal(DirectionText(halfEdge), candidate.VisualDirection);
        }
    }

    [Theory]
    [InlineData("P-10", "P-11", GroundingAccessLineSide.LargerNumberSide)]
    [InlineData("11#", "10#", GroundingAccessLineSide.SmallerNumberSide)]
    public void SimplePoleNumbers_ProduceConservativeRecommendation(
        string poleNumber,
        string adjacentPoleNumber,
        GroundingAccessLineSide expected)
    {
        Assert.Equal(expected,
            GroundingAccessPointCreationService.RecommendLineSide(poleNumber, adjacentPoleNumber));
    }

    [Theory]
    [InlineData("东支-甲", "P-11")]
    [InlineData("P-10", "10")]
    public void UnsupportedOrEqualPoleNumbers_RequireManualChoice(
        string poleNumber,
        string adjacentPoleNumber)
    {
        Assert.Null(GroundingAccessPointCreationService.RecommendLineSide(
            poleNumber, adjacentPoleNumber));
    }

    [Fact]
    public void UserSideOverrideKeepsPhysicalAdjacentPole_AndImmediateGroundingIsOptional()
    {
        Scenario scenario = CreateScenario();
        GroundingAccessCandidate[] candidates = GroundingAccessPointCreationService.GetCandidates(
            scenario.Session, scenario.Middle.Pole.Id).ToArray();

        scenario.Session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                scenario.Session, candidates[0],
                GroundingAccessLineSide.LargerNumberSide,
                addGroundingPoint: false),
            scenario.Session.RebuildScene);
        GroundingAccessPoint first = Assert.Single(
            scenario.Session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Equal(candidates[0].AdjacentPoleId, first.AdjacentPoleId);
        Assert.Equal(GroundingAccessLineSide.LargerNumberSide, first.LineSide);
        Assert.Empty(scenario.Session.PersistenceSession.Domain.GroundingPoints);

        scenario.Session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                scenario.Session, candidates[1],
                GroundingAccessLineSide.SmallerNumberSide,
                addGroundingPoint: true),
            scenario.Session.RebuildScene);
        Assert.Equal(2, scenario.Session.PersistenceSession.Domain.GroundingAccessPoints.Count);
        GroundingPoint grounding = Assert.Single(
            scenario.Session.PersistenceSession.Domain.GroundingPoints);
        Assert.Equal(GroundingTargetKind.GroundingAccessPoint, grounding.Target.Kind);
        Assert.Equal("小号侧", grounding.Location);
    }

    [Fact]
    public void SingleCandidateDialog_AutoSelectsTheOnlyPhysicalDirection()
    {
        RunOnSta(() =>
        {
            Scenario scenario = CreateScenario();
            GroundingAccessCandidate candidate = Assert.Single(
                GroundingAccessPointCreationService.GetCandidates(
                    scenario.Session, scenario.Start.Pole.Id));
            var dialog = new GroundingAccessPointCreationDialog([candidate]);

            Assert.Same(candidate, dialog.SelectedCandidate);
            dialog.Close();
        });
    }

    [Fact]
    public void GroundingTargetWhitelist_AcceptsOnlyGapAndCableSideTerminals()
    {
        ProjectRuntimeSession session = CreateSession("grounding whitelist");
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain, session.Layout, new DocumentPoint(20, 20));
        pole.Execute();
        AddCableTerminationAttachmentCommand termination =
            factory.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "终端",
                new DocumentPoint(10, 0));
        termination.Execute();
        AddPoleSwitchAttachmentCommand poleSwitch = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.IsolationSwitch,
            new DocumentPoint(-10, 0));
        poleSwitch.Execute();
        AddRingCabinetCommand cabinet = factory.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "测试柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional, 3)),
            new DocumentPoint(200, 20));
        cabinet.Execute();

        Guid ringCableTerminal = cabinet.Cabinet.Intervals[0].CableTerminalId!.Value;
        Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            session.PersistenceSession.Domain, termination.Creation.CableSideTerminal.Id));
        Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            session.PersistenceSession.Domain, ringCableTerminal));
        Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            session.PersistenceSession.Domain, termination.Creation.OverheadSideTerminal.Id));
        Assert.All(poleSwitch.Creation.SwitchDevice.TerminalIds, terminalId => Assert.False(
            ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                session.PersistenceSession.Domain, terminalId)));
    }

    [Fact]
    public void CanvasSceneAndPngExportUseTheSameGapMarkerElement()
    {
        RunOnSta(() =>
        {
            Scenario scenario = CreateScenario();
            GroundingAccessCandidate candidate = Assert.Single(
                GroundingAccessPointCreationService.GetCandidates(
                    scenario.Session, scenario.Start.Pole.Id));
            scenario.Session.CommandStack.ExecuteCommand(
                GroundingAccessPointCreationService.CreateCommand(
                    scenario.Session, candidate,
                    GroundingAccessLineSide.LargerNumberSide,
                    addGroundingPoint: false),
                scenario.Session.RebuildScene);
            GroundingAccessPoint gap = Assert.Single(
                scenario.Session.PersistenceSession.Domain.GroundingAccessPoints);
            SceneEllipse marker = FindGapMarker(scenario.Session.Scene,
                gap.GroundingAccessPointId);
            using var stream = new MemoryStream();

            DrawingSceneBitmapResult result = new DrawingSceneBitmapRenderer().RenderPng(
                scenario.Session.Scene,
                stream,
                new DrawingSceneBitmapOptions(Dpi: 96));

            Assert.True(marker.Bounds.WidthMillimeters > 0);
            Assert.True(result.WidthPixels > 0);
            Assert.True(stream.Length > 0);
        });
    }

    private Scenario CreateScenario()
    {
        ProjectRuntimeSession session = CreateSession("GAP workflow");
        var factory = new DeviceCommandFactory();
        AddPoleCommand start = AddPole(session, factory, new DocumentPoint(10, 10));
        AddPoleCommand middle = AddPole(session, factory, new DocumentPoint(90, 70));
        AddPoleCommand end = AddPole(session, factory, new DocumentPoint(190, 10));
        start.Pole.RenamePoleNumber("P-10");
        middle.Pole.RenamePoleNumber("P-11");
        end.Pole.RenamePoleNumber("P-12");
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId, ConnectionType.OverheadLine,
            start.Terminal.Id, end.Terminal.Id, "测试架空线", "10kV");
        var line = new OverheadLine(
            connectionId, "JKLYJ", [start.Pole.Id, middle.Pole.Id, end.Pole.Id]);
        var layout = new OverheadLineLayout(
            connectionId, start.Layout.Position, end.Layout.Position);
        new AddOverheadLineCommand(
            session.PersistenceSession.Domain,
            session.Layout,
            connection,
            line,
            layout).Execute();
        session.RebuildScene();
        return new Scenario(session, start, middle, end);
    }

    private static AddPoleCommand AddPole(
        ProjectRuntimeSession session,
        DeviceCommandFactory factory,
        DocumentPoint position)
    {
        AddPoleCommand command = factory.CreateAddPole(
            session.PersistenceSession.Domain, session.Layout, position);
        command.Execute();
        return command;
    }

    private ProjectRuntimeSession CreateSession(string title)
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"wp-em-04-desktop-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        ProjectSession persistence = new ProjectService().CreateProject(path, title);
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static string DirectionText(GroundingAccessHalfEdge halfEdge)
    {
        double dx = halfEdge.DirectionPoint.XMillimeters - halfEdge.PoleCenter.XMillimeters;
        double dy = halfEdge.DirectionPoint.YMillimeters - halfEdge.PoleCenter.YMillimeters;
        return dx < 0 ? "左侧" : dx > 0 ? "右侧" : dy < 0 ? "上侧" : "下侧";
    }

    private static SceneEllipse FindGapMarker(DrawingScene scene, Guid groundingAccessPointId)
    {
        SelectionHitTestEntry gapHit = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(
                SelectionTargetKind.GroundingAccessPoint,
                groundingAccessPointId)));
        DocumentPoint center = Center(gapHit.Bounds);
        return Assert.Single(scene.Elements.OfType<SceneEllipse>(), ellipse =>
            Center(ellipse.Bounds) == center);
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private sealed record Scenario(
        ProjectRuntimeSession Session,
        AddPoleCommand Start,
        AddPoleCommand Middle,
        AddPoleCommand End);
}
