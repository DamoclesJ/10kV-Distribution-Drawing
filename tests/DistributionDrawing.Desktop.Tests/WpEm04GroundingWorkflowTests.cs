using System.IO;
using System.Runtime.ExceptionServices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.GroundingAccessPointCreation;
using DistributionDrawing.Desktop.Clipboard;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.DrawingTypography;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
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

[Collection("WP-EM-04 typography")]
public sealed class WpEm04GroundingWorkflowTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void ShortOverheadLineToTransformer_ExposesTerminalHalfEdgeCandidateAndCreatesGap()
    {
        ProjectRuntimeSession session = CreateSession("WP-EM-07A short OHL");
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 40));
        pole.Execute();
        AddPoleSwitchAttachmentCommand fuse = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(90, 40),
            "测试变压器");
        transformer.Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            fuse.Creation.SecondTerminal.Id,
            transformer.Creation.HvTerminal.Id,
            new DocumentPoint(35, 40),
            new DocumentPoint(90, 40));
        line.Execute();
        session.RebuildScene();

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            session.PersistenceSession.Domain,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts,
            transformerLayouts: session.Layout.TransformerLayouts);
        Assert.True(anchors.TryGet(
            transformer.Creation.HvTerminal.Id,
            out TerminalAnchor transformerAnchor));
        GroundingTargetCandidate picked = Assert.IsType<GroundingTargetCandidate>(
            new GroundingTargetPicker().Resolve(
                session.PersistenceSession.Domain,
                session.Layout,
                session.Scene,
                transformerAnchor.Position,
                toleranceMillimeters: 1));
        Assert.Equal(
            GroundingTarget.ForTerminal(transformer.Creation.HvTerminal.Id),
            picked.Target);

        GroundingAccessCandidate candidate = Assert.Single(
            GroundingAccessPointCreationService.GetCandidates(session, pole.Pole.Id));
        Assert.Equal(GroundingAdjacentEndpointKind.Terminal, candidate.AdjacentEndpoint.Kind);
        Assert.Equal(transformer.Creation.HvTerminal.Id, candidate.AdjacentEndpoint.TargetId);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, candidate.FixedLineSide);
        Assert.Equal(GroundingAccessPlacementSide.PoleSide, candidate.PlacementSide);
        GroundingAccessCandidateLineSideState state =
            GroundingAccessPointCreationService.ResolveLineSideState(candidate);
        Assert.True(state.IsLocked);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, state.SelectedLineSide);
        Assert.Contains("变压器侧", state.Message);
        Assert.Null(candidate.AdjacentPoleId);

        session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                session,
                candidate,
                GroundingAccessLineSide.LargerNumberSide,
                addGroundingPoint: true),
            session.RebuildScene);

        GroundingAccessPoint gap = Assert.Single(
            session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Equal(candidate.AdjacentEndpoint, gap.AdjacentEndpoint);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, gap.LineSide);
        Assert.Equal(GroundingAccessPlacementSide.PoleSide, gap.PlacementSide);
        Assert.Equal(
            GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            Assert.Single(session.PersistenceSession.Domain.GroundingPoints).Target);
        Assert.DoesNotContain(session.Scene.Diagnostics, diagnostic =>
            diagnostic.Code == "GroundingAccessPointAnchorMissing");
        Assert.Contains(session.Scene.Elements.OfType<SceneEllipse>(), element =>
            element.TargetId == gap.GroundingAccessPointId);
        AssertTerminalGapScene(session, pole.Pole.Id, gap);

        Guid groundingPointId = Assert.Single(
            session.PersistenceSession.Domain.GroundingPoints).GroundingPointId;
        Assert.True(session.CommandStack.Undo());
        Assert.Empty(session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Empty(session.PersistenceSession.Domain.GroundingPoints);
        Assert.True(session.CommandStack.Redo());
        GroundingAccessPoint restoredGap = Assert.Single(
            session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Equal(gap.GroundingAccessPointId, restoredGap.GroundingAccessPointId);
        Assert.Equal(candidate.AdjacentEndpoint, restoredGap.AdjacentEndpoint);
        Assert.Equal(groundingPointId, Assert.Single(
            session.PersistenceSession.Domain.GroundingPoints).GroundingPointId);
    }

    [Fact]
    public void ShortOverheadLineToTransformer_WhenTransformerIsStart_ResolvesTerminalGapScene()
    {
        ProjectRuntimeSession session = CreateSession("WP-EM-07A mirrored short OHL");
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 40));
        pole.Execute();
        AddPoleSwitchAttachmentCommand fuse = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(90, 40),
            "测试变压器");
        transformer.Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            transformer.Creation.HvTerminal.Id,
            fuse.Creation.SecondTerminal.Id,
            new DocumentPoint(90, 40),
            new DocumentPoint(35, 40));
        line.Execute();
        session.RebuildScene();

        GroundingAccessCandidate candidate = Assert.Single(
            GroundingAccessPointCreationService.GetCandidates(session, pole.Pole.Id));
        Assert.Equal(GroundingAdjacentEndpointKind.Terminal, candidate.AdjacentEndpoint.Kind);
        Assert.Equal(transformer.Creation.HvTerminal.Id, candidate.AdjacentEndpoint.TargetId);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, candidate.FixedLineSide);

        session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                session,
                candidate,
                GroundingAccessLineSide.LargerNumberSide,
                addGroundingPoint: true),
            session.RebuildScene);

        GroundingAccessPoint gap = Assert.Single(
            session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Equal(candidate.AdjacentEndpoint, gap.AdjacentEndpoint);
        Assert.Equal(
            GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            Assert.Single(session.PersistenceSession.Domain.GroundingPoints).Target);
        AssertTerminalGapScene(session, pole.Pole.Id, gap);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, false)]
    [InlineData(TransformerKind.PublicPoleMounted, true)]
    [InlineData(TransformerKind.DedicatedPoleMounted, false)]
    [InlineData(TransformerKind.DedicatedPoleMounted, true)]
    public void PoleAndTransformerWorkflows_CreateIndependentDualGap(
        TransformerKind kind,
        bool transformerIsStart)
    {
        ProjectRuntimeSession session = CreateSession("WP-EM-07A dual GAP");
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 40));
        pole.Execute();
        AddPoleSwitchAttachmentCommand fuse = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            kind,
            new DocumentPoint(90, 40),
            "测试变压器");
        transformer.Execute();
        Guid first = transformerIsStart
            ? transformer.Creation.HvTerminal.Id
            : fuse.Creation.SecondTerminal.Id;
        Guid second = transformerIsStart
            ? fuse.Creation.SecondTerminal.Id
            : transformer.Creation.HvTerminal.Id;
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            first,
            second,
            transformerIsStart ? new DocumentPoint(90, 40) : new DocumentPoint(35, 40),
            transformerIsStart ? new DocumentPoint(35, 40) : new DocumentPoint(90, 40));
        line.Execute();
        session.RebuildScene();

        GroundingAccessCandidate poleCandidate = Assert.Single(
            GroundingAccessPointCreationService.GetCandidates(session, pole.Pole.Id));
        session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                session,
                poleCandidate,
                GroundingAccessLineSide.SmallerNumberSide,
                addGroundingPoint: true),
            session.RebuildScene);
        GroundingAccessCandidate transformerCandidate = Assert.Single(
            GroundingAccessPointCreationService.GetTransformerCandidates(
                session,
                transformer.Creation.Transformer.Id));
        Assert.Equal(
            GroundingAccessPlacementSide.AdjacentEndpointSide,
            transformerCandidate.PlacementSide);
        session.CommandStack.ExecuteCommand(
            GroundingAccessPointCreationService.CreateCommand(
                session,
                transformerCandidate,
                GroundingAccessLineSide.TransformerSide,
                addGroundingPoint: true),
            session.RebuildScene);

        GroundingAccessPoint[] gaps = session.PersistenceSession.Domain.GroundingAccessPoints
            .OrderBy(point => point.PlacementSide)
            .ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.Equal(GroundingAccessPlacementSide.PoleSide, gaps[0].PlacementSide);
        Assert.Equal(GroundingAccessPlacementSide.AdjacentEndpointSide, gaps[1].PlacementSide);
        Assert.Equal(2, session.PersistenceSession.Domain.GroundingPoints.Count);
        Assert.Contains(session.PersistenceSession.Domain.GroundingPoints,
            point => point.Location == $"{pole.Pole.PoleNumber}杆变压器侧");
        Assert.Contains(session.PersistenceSession.Domain.GroundingPoints,
            point => point.Location == "变压器高压侧导线");
        DocumentPoint[] markerCenters = gaps.Select(gap => Center(
            FindGapMarker(session.Scene, gap.GroundingAccessPointId).Bounds)).ToArray();
        Assert.NotEqual(markerCenters[0], markerCenters[1]);
        OrthogonalRoute finalRoute = Assert.Single(session.Scene.Routes,
            route => route.ConnectionId == line.Connection.Id);
        Assert.All(markerCenters, marker => Assert.Contains(
            finalRoute.Segments,
            segment => Contains(segment, marker)));
        TransformerProfessionalGeometry transformerGeometry =
            TransformerProfessionalGeometry.Create(
                transformer.Creation.Transformer,
                transformer.Creation.Layout,
                DrawingMetrics.Default.Transformer);
        DocumentPoint transformerMarker = markerCenters[Array.FindIndex(
            gaps,
            gap => gap.PlacementSide == GroundingAccessPlacementSide.AdjacentEndpointSide)];
        Assert.False(transformerMarker.XMillimeters > transformerGeometry.Bounds.XMillimeters &&
                     transformerMarker.XMillimeters < transformerGeometry.Bounds.XMillimeters +
                         transformerGeometry.Bounds.WidthMillimeters &&
                     transformerMarker.YMillimeters > transformerGeometry.Bounds.YMillimeters &&
                     transformerMarker.YMillimeters < transformerGeometry.Bounds.YMillimeters +
                         transformerGeometry.Bounds.HeightMillimeters);
        Assert.DoesNotContain(session.Scene.Diagnostics, diagnostic =>
            diagnostic.Code is "GroundingAccessPointAnchorMissing" or
                "GroundingPresentationAnchorMissing");
        Assert.Empty(GroundingAccessPointCreationService.GetCandidates(session, pole.Pole.Id));
        Assert.Empty(GroundingAccessPointCreationService.GetTransformerCandidates(
            session,
            transformer.Creation.Transformer.Id));

        Guid transformerSideGapId = gaps[1].GroundingAccessPointId;
        Assert.True(session.CommandStack.Undo());
        Assert.Single(session.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.True(session.CommandStack.Redo());
        Assert.Contains(session.PersistenceSession.Domain.GroundingAccessPoints,
            point => point.GroundingAccessPointId == transformerSideGapId);
    }

    [Fact]
    public void TransformerWorkflow_PublicIndoorHasNoOverheadCandidate()
    {
        ProjectRuntimeSession session = CreateSession("WP-EM-07A indoor reject");
        AddTransformerCommand transformer = new DeviceCommandFactory().CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicIndoor,
            new DocumentPoint(90, 40),
            "测试变压器");
        transformer.Execute();
        session.RebuildScene();

        Assert.Empty(GroundingAccessPointCreationService.GetTransformerCandidates(
            session,
            transformer.Creation.Transformer.Id));
    }

    [Fact]
    public void PoleWorkflow_MixedPoleAndTransformerCandidatesHaveSafeLineSideState()
    {
        ProjectRuntimeSession session = CreateSession("WP-EM-07A mixed candidates");
        var factory = new DeviceCommandFactory();
        AddPoleCommand left = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(10, 40));
        AddPoleCommand middle = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(80, 40));
        left.Execute();
        middle.Execute();
        left.Pole.RenamePoleNumber("11#");
        middle.Pole.RenamePoleNumber("12#");
        AddOverheadLineCommand ordinary = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            left.Terminal.Id,
            middle.Terminal.Id,
            new DocumentPoint(10, 40),
            new DocumentPoint(80, 40));
        ordinary.Execute();
        AddPoleSwitchAttachmentCommand fuse = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            middle.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand transformer = factory.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(150, 40),
            "测试变压器");
        transformer.Execute();
        Terminal[] fuseTerminals =
            [fuse.Creation.FirstTerminal, fuse.Creation.SecondTerminal];
        Terminal occupiedFuseTerminal = Assert.Single(fuseTerminals, terminal =>
            session.PersistenceSession.Domain.Connections.Any(connection =>
                connection.UsesTerminal(terminal.Id)));
        Terminal freeFuseTerminal = Assert.Single(fuseTerminals, terminal =>
            !session.PersistenceSession.Domain.Connections.Any(connection =>
                connection.UsesTerminal(terminal.Id)));
        Connection migratedOrdinary = session.PersistenceSession.Domain.Connections.Single(
            connection => connection.Id == ordinary.Connection.Id);
        Assert.True(migratedOrdinary.UsesTerminal(occupiedFuseTerminal.Id));
        AddOverheadLineCommand shortLine = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            freeFuseTerminal.Id,
            transformer.Creation.HvTerminal.Id,
            new DocumentPoint(95, 40),
            new DocumentPoint(150, 40));
        shortLine.Execute();
        session.RebuildScene();

        GroundingAccessCandidate[] candidates = GroundingAccessPointCreationService
            .GetCandidates(session, middle.Pole.Id)
            .ToArray();
        Assert.Equal(2, candidates.Length);
        GroundingAccessCandidate poleCandidate = Assert.Single(candidates,
            candidate => candidate.AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Pole);
        GroundingAccessCandidate transformerCandidate = Assert.Single(candidates,
            candidate => candidate.AdjacentEndpoint.Kind == GroundingAdjacentEndpointKind.Terminal);
        GroundingAccessCandidateLineSideState poleState =
            GroundingAccessPointCreationService.ResolveLineSideState(poleCandidate);
        GroundingAccessCandidateLineSideState transformerState =
            GroundingAccessPointCreationService.ResolveLineSideState(transformerCandidate);
        Assert.Equal(GroundingAccessLineSide.SmallerNumberSide, poleState.SelectedLineSide);
        Assert.False(poleState.IsLocked);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, transformerState.SelectedLineSide);
        Assert.True(transformerState.IsLocked);
        Assert.Null(transformerCandidate.AdjacentPoleId);
    }

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
            middle.Select(candidate => candidate.AdjacentPoleId!.Value).OrderBy(id => id));
        OverheadLine line = Assert.Single(scenario.Session.PersistenceSession.Domain.OverheadLines);
        OrthogonalRoute route = Assert.Single(scenario.Session.Scene.Routes);
        foreach (GroundingAccessCandidate candidate in middle)
        {
            Assert.True(SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
                route,
                line,
                scenario.Session.Layout.DrawingLayout,
                candidate.PoleId,
                candidate.AdjacentPoleId!.Value,
                out GroundingAccessHalfEdge halfEdge));
            Assert.Equal(DirectionText(halfEdge), candidate.VisualDirection);
        }
    }

    [Fact]
    public void DuplicateGroundingPointNumber_UsesIndependentCommandHistory()
    {
        RunOnSta(() =>
        {
            Scenario scenario = CreateScenario();
            GroundingAccessCandidate first = GroundingAccessPointCreationService.GetCandidates(scenario.Session, scenario.Middle.Pole.Id)[0];
            GroundingAccessCandidate second = GroundingAccessPointCreationService.GetCandidates(scenario.Session, scenario.Middle.Pole.Id)[1];
            scenario.Session.CommandStack.ExecuteCommand(GroundingAccessPointCreationService.CreateCommand(
                scenario.Session, first, GroundingAccessLineSide.SmallerNumberSide, true), scenario.Session.RebuildScene);
            scenario.Session.CommandStack.ExecuteCommand(GroundingAccessPointCreationService.CreateCommand(
                scenario.Session, second, GroundingAccessLineSide.LargerNumberSide, true), scenario.Session.RebuildScene);
            GroundingPoint target = Assert.Single(scenario.Session.PersistenceSession.Domain.GroundingPoints,
                point => point.Number == "L02");
            var editor = new PropertyEditor(scenario.Session.SelectionResolver, scenario.Session.CommandStack, scenario.Session.Layout);
            SelectionReference reference = new(SelectionTargetKind.GroundingPoint, target.GroundingPointId);
            Assert.False(editor.TryEdit(reference, PropertyCommandFactory.GroundingPointNumberPropertyKey, "L01").IsSuccess);
        });
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
        Assert.Equal("P-11杆小号侧", grounding.Location);
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
        foreach (Guid terminalId in new[] { ringCableTerminal, termination.Creation.CableSideTerminal.Id })
        {
            session.CommandStack.ExecuteCommand(new ProfessionalCommandFactory().CreateAddGroundingPoint(
                session.PersistenceSession.Domain, GroundingTarget.ForTerminal(terminalId), "电缆侧"));
        }
        Assert.Equal(
            ["S01", "S02"],
            session.PersistenceSession.Domain.GroundingPoints.Select(point => point.Number!).ToArray());
        session.RebuildScene();
        Assert.Empty(session.Scene.Diagnostics);
        foreach (GroundingPoint point in session.PersistenceSession.Domain.GroundingPoints)
        {
            SceneLine[] lines = session.Scene.Elements.OfType<SceneLine>().Where(line => line.TargetId == point.GroundingPointId).ToArray();
            Assert.True(lines.Length >= 5);
            Assert.DoesNotContain(session.Scene.Elements, element => element.TargetId == point.GroundingPointId && element is SceneRectangle);
        }
    }

    [Fact]
    public void CableSideCreation_UsesFirstFreeSNumbersAcrossBothLegalTargetKinds()
    {
        ProjectRuntimeSession session = CreateSession("cable grounding numbers");
        var devices = new DeviceCommandFactory();
        var professional = new ProfessionalCommandFactory();
        AddPoleCommand pole = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 20));
        pole.Execute();
        AddCableTerminationAttachmentCommand termination = devices.CreateAddCableTerminationAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            "终端",
            new DocumentPoint(10, 0));
        termination.Execute();
        AddRingCabinetCommand cabinet = devices.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "测试柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(200, 20));
        cabinet.Execute();
        Guid[] cableTargets =
        [
            termination.Creation.CableSideTerminal.Id,
            .. cabinet.Cabinet.Intervals.Select(interval => interval.CableTerminalId).OfType<Guid>()
        ];
        Assert.True(cableTargets.Length >= 4);

        var stack = new CommandStack();
        foreach (Guid terminalId in cableTargets.Take(3))
        {
            stack.ExecuteCommand(professional.CreateAddGroundingPoint(
                session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(terminalId),
                "电缆侧"));
        }
        Assert.Equal(
            ["S01", "S02", "S03"],
            session.PersistenceSession.Domain.GroundingPoints.Select(point => point.Number!).ToArray());

        GroundingPoint s02 = session.PersistenceSession.Domain.GroundingPoints.Single(
            point => point.Number == "S02");
        stack.ExecuteCommand(professional.CreateRemoveGroundingPoint(
            session.PersistenceSession.Domain,
            s02.GroundingPointId));
        stack.ExecuteCommand(professional.CreateAddGroundingPoint(
            session.PersistenceSession.Domain,
            GroundingTarget.ForTerminal(cableTargets[3]),
            "电缆侧"));
        Assert.Contains(session.PersistenceSession.Domain.GroundingPoints,
            point => point.Number == "S02" && point.Target.TargetId == cableTargets[3]);
    }

    [Fact]
    public void CreationWithoutLocationInput_UsesTargetBusinessNamesInsteadOfInternalIds()
    {
        ProjectRuntimeSession session = CreateSession("grounding location defaults");
        var devices = new DeviceCommandFactory();
        var professional = new ProfessionalCommandFactory();
        AddRingCabinetCommand firstCabinet = devices.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "新11KB5",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(100, 20));
        AddRingCabinetCommand secondCabinet = devices.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "东环路1号环网柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(300, 20));
        firstCabinet.Execute();
        secondCabinet.Execute();
        RingCabinetInterval firstInterval = firstCabinet.Cabinet.Intervals[0];
        RingCabinetInterval secondInterval = secondCabinet.Cabinet.Intervals[1];
        firstCabinet.Cabinet.RenameInterval(firstInterval.IntervalId, "负4");
        secondCabinet.Cabinet.RenameInterval(secondInterval.IntervalId, "负6");

        AddGroundingPointCommand first = (AddGroundingPointCommand)
            professional.CreateAddGroundingPoint(
                session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(firstInterval.CableTerminalId!.Value));
        first.Execute();

        AddGroundingPointCommand second = (AddGroundingPointCommand)
            professional.CreateAddGroundingPoint(
                session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(secondInterval.CableTerminalId!.Value));
        second.Execute();

        Assert.Equal("新11KB5负4间隔", first.After.Location);
        Assert.Equal("东环路1号环网柜负6间隔", second.After.Location);
        Assert.DoesNotContain(firstInterval.CableTerminalId.Value.ToString(), first.After.Location);
        Assert.DoesNotContain(secondInterval.CableTerminalId.Value.ToString(), second.After.Location);
    }

    [Fact]
    public void CableTerminationAndGapCreationWithoutLocationInput_UseReadableDefaults()
    {
        Scenario scenario = CreateScenario();
        var devices = new DeviceCommandFactory();
        var professional = new ProfessionalCommandFactory();
        AddCableTerminationAttachmentCommand termination =
            devices.CreateAddCableTerminationAttachment(
                scenario.Session.PersistenceSession.Domain,
                scenario.Session.Layout,
                scenario.Start.Pole.Id,
                "东侧电缆终端",
                new DocumentPoint(10, 0));
        termination.Execute();
        AddGroundingPointCommand cableGround = (AddGroundingPointCommand)
            professional.CreateAddGroundingPoint(
                scenario.Session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(termination.Creation.CableSideTerminal.Id));
        cableGround.Execute();

        GroundingAccessPoint gap = scenario.Session.PersistenceSession.Domain
            .CreateGroundingAccessPoint(
                Guid.NewGuid(),
                Assert.Single(scenario.Session.PersistenceSession.Domain.Connections).Id,
                scenario.Middle.Pole.Id,
                scenario.Start.Pole.Id,
                GroundingAccessLineSide.SmallerNumberSide);
        AddGroundingPointCommand overheadGround = (AddGroundingPointCommand)
            professional.CreateAddGroundingPoint(
                scenario.Session.PersistenceSession.Domain,
                GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId));
        overheadGround.Execute();

        Assert.Equal("P-10杆电缆终端", cableGround.After.Location);
        Assert.Equal("P-11杆小号侧", overheadGround.After.Location);
        Assert.Equal("S01", cableGround.After.Number);
        Assert.Equal("L01", overheadGround.After.Number);
    }

    [Fact]
    public void CableTerminationConnectedToRingInterval_UsesLocalPoleLocationWhileRingTargetUsesCabinetLocation()
    {
        ProjectRuntimeSession session = CreateSession("connected grounding location");
        var devices = new DeviceCommandFactory();
        AddPoleCommand pole = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 20));
        pole.Execute();
        pole.Pole.RenamePoleNumber("P-01");
        AddCableTerminationAttachmentCommand termination =
            devices.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "本地终端名",
                new DocumentPoint(10, 0));
        termination.Execute();
        AddRingCabinetCommand cabinet = devices.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "滨河站环网柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(240, 20));
        cabinet.Execute();
        RingCabinetInterval interval = cabinet.Cabinet.Intervals[0];
        cabinet.Cabinet.RenameInterval(interval.IntervalId, "备用馈线");
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            termination.Creation.CableSideTerminal.Id,
            interval.CableTerminalId!.Value,
            "连接电缆",
            "10kV");
        session.PersistenceSession.Domain.AddConnection(connection);

        AddGroundingPointCommand ringCommand = (AddGroundingPointCommand)
            new ProfessionalCommandFactory().CreateAddGroundingPoint(
                session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(interval.CableTerminalId.Value));
        ringCommand.Execute();

        AddGroundingPointCommand cableCommand = (AddGroundingPointCommand)
            new ProfessionalCommandFactory().CreateAddGroundingPoint(
                session.PersistenceSession.Domain,
                GroundingTarget.ForTerminal(
                    termination.Creation.CableSideTerminal.Id));
        cableCommand.Execute();

        Assert.Equal("滨河站环网柜备用馈线间隔", ringCommand.After.Location);
        Assert.Equal("P-01杆电缆终端", cableCommand.After.Location);
        Assert.NotEqual(ringCommand.After.Location, cableCommand.After.Location);
        Assert.DoesNotContain(
            termination.Creation.CableSideTerminal.Id.ToString(),
            cableCommand.After.Location);
    }

    [Fact]
    public void AutomaticallyCreatedLocation_RemainsInspectorEditableWithoutRetargeting()
    {
        Scenario scenario = CreateScenario();
        DrawingDocument document = scenario.Session.PersistenceSession.Domain;
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            Assert.Single(document.Connections).Id,
            scenario.Middle.Pole.Id,
            scenario.Start.Pole.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        AddGroundingPointCommand add = (AddGroundingPointCommand)
            new ProfessionalCommandFactory().CreateAddGroundingPoint(
                document,
                GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId));
        scenario.Session.CommandStack.ExecuteCommand(add, scenario.Session.RebuildScene);
        GroundingTarget target = add.After.Target;
        var reference = new SelectionReference(
            SelectionTargetKind.GroundingPoint,
            add.After.GroundingPointId);
        var editor = new PropertyEditor(
            scenario.Session.SelectionResolver,
            scenario.Session.CommandStack,
            scenario.Session.Layout);

        PropertyEditResult result = editor.TryEdit(
            reference,
            PropertyCommandFactory.GroundingPointLocationPropertyKey,
            " 自定义检修位置 ");

        Assert.True(result.IsSuccess);
        GroundingPoint groundingPoint = document.GetGroundingPoint(add.After.GroundingPointId);
        Assert.Equal("自定义检修位置", groundingPoint.Location);
        Assert.Equal(target, groundingPoint.Target);
        Assert.True(scenario.Session.CommandStack.Undo());
        Assert.Equal("P-11杆小号侧", groundingPoint.Location);
        Assert.Equal(target, groundingPoint.Target);
    }

    [Fact]
    public void SwitchEndpointDirectionResolution_AllowsCreatingGapWithoutException()
    {
        Scenario scenario = CreateScenario();
        ProjectRuntimeSession session = scenario.Session;
        AddPoleSwitchAttachmentCommand command = new DeviceCommandFactory().CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain, session.Layout, scenario.Start.Pole.Id,
            SwitchKind.IsolationSwitch, PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.IsolationSwitch));
        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        GroundingAccessCandidate candidate = Assert.Single(
            GroundingAccessPointCreationService.GetCandidates(session, scenario.Start.Pole.Id));
        session.CommandStack.ExecuteCommand(GroundingAccessPointCreationService.CreateCommand(
            session, candidate, GroundingAccessLineSide.LargerNumberSide, false), session.RebuildScene);
        Assert.Empty(session.Scene.Diagnostics);
        Assert.Single(session.PersistenceSession.Domain.GroundingAccessPoints);
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

    [Fact]
    public void SelectEditDeleteGroundingPoint_ReleasesGapForClipboardAndPreservesUndoIdentity()
    {
        Scenario scenario = CreateScenario();
        ProjectRuntimeSession session = scenario.Session;
        var document = session.PersistenceSession.Domain;
        GroundingAccessCandidate candidate = GroundingAccessPointCreationService.GetCandidates(session, scenario.Middle.Pole.Id)[0];
        session.CommandStack.ExecuteCommand(GroundingAccessPointCreationService.CreateCommand(session, candidate,
            GroundingAccessLineSide.SmallerNumberSide, true), session.RebuildScene);
        GroundingAccessPoint gap = Assert.Single(document.GroundingAccessPoints);
        GroundingPoint gp = Assert.Single(document.GroundingPoints);
        session.CommandStack.MarkSaved();
        Assert.False(session.IsDirty);
        SelectionReference[] structure =
        [
            new(SelectionTargetKind.Device, scenario.Start.Pole.Id),
            new(SelectionTargetKind.Device, scenario.Middle.Pole.Id),
            new(SelectionTargetKind.Device, scenario.End.Pole.Id),
            new(SelectionTargetKind.Connection, candidate.ConnectionId)
        ];
        var clipboard = new DrawingClipboardService();
        session.SelectionManager.Replace(structure);
        Assert.False(clipboard.Copy(session).IsSuccess);
        var gapReference = new SelectionReference(SelectionTargetKind.GroundingAccessPoint, gap.GroundingAccessPointId);
        Assert.Equal("验电接地环", session.PropertyProjector.Project(session.SelectionResolver.Resolve(gapReference)).ObjectType);
        Assert.Throws<InvalidOperationException>(() => session.CommandStack.ExecuteCommand(
            new SelectionDeletePlanner().Create(session, SelectionSet.Create([gapReference])), session.RebuildScene));
        Assert.False(session.IsDirty);
        Assert.Same(gp, Assert.Single(document.GroundingPoints));
        SceneLine stem = Assert.Single(session.Scene.Elements.OfType<SceneLine>(), line =>
            line.TargetId == gp.GroundingPointId &&
            line.Start.XMillimeters == line.End.XMillimeters &&
            line.End.YMillimeters - line.Start.YMillimeters == DrawingMetrics.Default.Grounding.StemLength);
        SelectionReference gpReference = session.Scene.HitTestIndex.HitTest(stem.End)!;
        Assert.Equal(new SelectionReference(SelectionTargetKind.GroundingPoint, gp.GroundingPointId), gpReference);
        session.SelectionManager.Select(gpReference);
        Assert.Equal("工作地线", session.PropertyProjector.Project(session.SelectionResolver.Resolve(gpReference)).ObjectType);
        var editor = new PropertyEditor(session.SelectionResolver, session.CommandStack, session.Layout);
        Assert.True(editor.TryEdit(gpReference, PropertyCommandFactory.GroundingPointNumberPropertyKey, " L03 ").IsSuccess);
        session.RebuildScene();
        Assert.Equal("L03", gp.Number);
        Assert.True(session.CommandStack.Undo());
        Assert.Equal("L01", gp.Number);
        Assert.True(session.CommandStack.Redo());
        session.CommandStack.ExecuteCommand(new SelectionDeletePlanner().Create(session, SelectionSet.Create([gpReference])), session.RebuildScene);
        Assert.Empty(document.GroundingPoints);
        Assert.Same(gap, document.GetGroundingAccessPoint(gap.GroundingAccessPointId));
        Assert.True(session.CommandStack.Undo());
        Assert.Equal(gp.GroundingPointId, Assert.Single(document.GroundingPoints).GroundingPointId);
        Assert.True(session.CommandStack.Redo());
        Assert.True(session.IsDirty);
        session.RebuildScene();
        Assert.DoesNotContain(session.SelectionManager.SelectionSet.SelectedReferences,
            item => item.Kind == SelectionTargetKind.GroundingPoint && item.ObjectId == gp.GroundingPointId);
        session.SelectionManager.Replace(structure);
        Assert.True(clipboard.Copy(session).IsSuccess);
        Assert.True(clipboard.Paste(session).IsSuccess);
        GroundingAccessPoint pasted = Assert.Single(document.GroundingAccessPoints, point =>
            point.ConnectionId != gap.ConnectionId && point.LineSide == gap.LineSide);
        Assert.NotEqual(gap.GroundingAccessPointId, pasted.GroundingAccessPointId);
        Assert.NotEqual(gap.PoleId, pasted.PoleId);
        Assert.NotEqual(gap.AdjacentPoleId, pasted.AdjacentPoleId);
        Assert.Empty(session.Scene.Diagnostics);
    }

    [Fact]
    public void FontDialogAndSceneRebuild_UseSessionTypographyForCanvasAndPng()
    {
        RunOnSta(() =>
        {
            Scenario scenario = CreateScenario();
            GroundingAccessCandidate candidate = GroundingAccessPointCreationService.GetCandidates(scenario.Session, scenario.Start.Pole.Id)[0];
            scenario.Session.CommandStack.ExecuteCommand(GroundingAccessPointCreationService.CreateCommand(scenario.Session,
                candidate, GroundingAccessLineSide.LargerNumberSide, true), scenario.Session.RebuildScene);
            GroundingPoint gp = Assert.Single(scenario.Session.PersistenceSession.Domain.GroundingPoints);
            DrawingTypographyMetrics typography = DrawingMetrics.Default.Typography;
            double before = typography.GroundingPointNumberFontSize;
            System.Windows.Application application = System.Windows.Application.Current ?? new System.Windows.Application();
            if (!application.Resources.MergedDictionaries.Any(dictionary =>
                    dictionary.Source?.OriginalString.Contains("DesktopTheme.xaml", StringComparison.Ordinal) == true))
            {
                application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                {
                    Source = new Uri("/DistributionDrawing.Desktop;component/Themes/DesktopTheme.xaml", UriKind.Relative)
                });
            }
            var dialog = new DrawingTypographyDialog();
            Assert.NotNull(dialog.FindName("GroundingPointNumberFontSizeInput"));
            Assert.NotNull(dialog.FindName("TransformerNameFontSizeInput"));
            dialog.Close();
            try
            {
                typography.Update(typography.CabinetNameFontSize, typography.LineNameFontSize,
                    typography.IntervalNumberFontSize, typography.SwitchNumberFontSize,
                    typography.PoleNumberFontSize, typography.PTLabelFontSize, before + 2);
                scenario.Session.RebuildScene();
                SceneText label = Assert.Single(scenario.Session.Scene.Elements.OfType<SceneText>(), text => text.TargetId == gp.GroundingPointId);
                Assert.Equal(before + 2, label.FontSizeMillimeters);
                using var stream = new MemoryStream();
                new DrawingSceneBitmapRenderer().RenderPng(scenario.Session.Scene, stream, new DrawingSceneBitmapOptions(Dpi: 96));
                Assert.True(stream.Length > 0);
                Assert.Equal(before + 2, label.FontSizeMillimeters);
            }
            finally
            {
                typography.Update(typography.CabinetNameFontSize, typography.LineNameFontSize,
                    typography.IntervalNumberFontSize, typography.SwitchNumberFontSize,
                    typography.PoleNumberFontSize, typography.PTLabelFontSize, before);
            }
        });
    }

    [Fact]
    public void ToolboxGroundingAccessIcon_HasVerticalLeadAndSquare_DistinctFromGroundingSymbol()
    {
        RunOnSta(() =>
        {
            var resources = new System.Windows.ResourceDictionary
            {
                Source = new Uri("/DistributionDrawing.Desktop;component/Themes/DesktopIcons.xaml", UriKind.Relative)
            };
            var gap = (System.Windows.Media.Geometry)resources["Icon.GroundingAccessPoint"];
            var grounding = (System.Windows.Media.Geometry)resources["Icon.WorkGrounding"];
            System.Windows.Media.PathGeometry geometry = gap.GetFlattenedPathGeometry();
            Assert.Equal(2, geometry.Figures.Count);
            Assert.Equal(new System.Windows.Point(12, 3), geometry.Figures[0].StartPoint);
            Assert.Equal(new System.Windows.Point(12, 15), geometry.Figures[0].Segments.OfType<System.Windows.Media.LineSegment>().Single().Point);
            Assert.True(geometry.Figures[1].IsClosed);
            var squarePoints = geometry.Figures[1].Segments
                .SelectMany(segment => segment switch
                {
                    System.Windows.Media.LineSegment line => new[] { line.Point },
                    System.Windows.Media.PolyLineSegment poly => poly.Points.ToArray(),
                    _ => Array.Empty<System.Windows.Point>()
                })
                .Append(geometry.Figures[1].StartPoint)
                .ToArray();
            Assert.Equal(9, squarePoints.Min(point => point.X));
            Assert.Equal(15, squarePoints.Min(point => point.Y));
            Assert.Equal(15, squarePoints.Max(point => point.X));
            Assert.Equal(21, squarePoints.Max(point => point.Y));
            Assert.NotEqual(gap.ToString(), grounding.ToString());
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
        double dx = halfEdge.DirectionPoint.XMillimeters - halfEdge.ConductorOrigin.XMillimeters;
        double dy = halfEdge.DirectionPoint.YMillimeters - halfEdge.ConductorOrigin.YMillimeters;
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

    private static void AssertTerminalGapScene(
        ProjectRuntimeSession session,
        Guid poleId,
        GroundingAccessPoint gap)
    {
        DrawingDocument document = session.PersistenceSession.Domain;
        OverheadLine line = Assert.Single(document.OverheadLines, item =>
            item.ConnectionId == gap.ConnectionId);
        Connection connection = Assert.Single(document.Connections, item =>
            item.Id == gap.ConnectionId);
        OrthogonalRoute route = Assert.Single(session.Scene.Routes, item =>
            item.ConnectionId == gap.ConnectionId);
        Assert.True(SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
            route,
            line,
            session.Layout.DrawingLayout,
            poleId,
            gap.AdjacentEndpoint,
            connection,
            out GroundingAccessHalfEdge halfEdge));
        Assert.NotEqual(halfEdge.ConductorOrigin, halfEdge.DirectionPoint);
        Assert.True(new GroundingAccessPointAnchorResolver().TryResolve(
            gap,
            document,
            session.Layout.DrawingLayout,
            new Dictionary<Guid, OrthogonalRoute> { [connection.Id] = route },
            out GroundingPresentationAnchor anchor));
        Assert.Contains(route.Segments, segment => Contains(segment, anchor.Position));

        DocumentRect occupied = PoleProfessionalGeometry.GetOccupiedEnvelope(
            poleId,
            document,
            session.Layout.DrawingLayout,
            DrawingMetrics.Default);
        double required = DrawingMetrics.Default.Line.GroundingAccessClearance +
            (DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter +
             DrawingMetrics.Default.Line.ConnectionThickness) / 2;
        DocumentRect forbidden = new(
            occupied.XMillimeters - required,
            occupied.YMillimeters - required,
            occupied.WidthMillimeters + required * 2,
            occupied.HeightMillimeters + required * 2);
        Assert.True(
            anchor.Position.XMillimeters <= forbidden.XMillimeters ||
            anchor.Position.XMillimeters >= forbidden.XMillimeters + forbidden.WidthMillimeters ||
            anchor.Position.YMillimeters <= forbidden.YMillimeters ||
            anchor.Position.YMillimeters >= forbidden.YMillimeters + forbidden.HeightMillimeters);
        Assert.Equal(anchor.Position, Center(FindGapMarker(
            session.Scene,
            gap.GroundingAccessPointId).Bounds));
        Assert.DoesNotContain(session.Scene.Diagnostics, diagnostic =>
            diagnostic.Code is "GroundingAccessPointAnchorMissing" or
                "GroundingPresentationAnchorMissing");
    }

    private static bool Contains(OrthogonalRouteSegment segment, DocumentPoint point) =>
        segment.IsHorizontal
            ? point.YMillimeters == segment.Start.YMillimeters &&
              point.XMillimeters >= Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) &&
              point.XMillimeters <= Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters)
            : point.XMillimeters == segment.Start.XMillimeters &&
              point.YMillimeters >= Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) &&
              point.YMillimeters <= Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters);

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

[CollectionDefinition("WP-EM-04 typography", DisableParallelization = true)]
public sealed class WpEm04TypographyCollection;
