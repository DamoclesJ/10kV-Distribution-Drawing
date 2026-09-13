using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class PlacementPreviewTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(),
        $"placement-preview-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void PoleGhostUsesTheFinalSnappedPositionWithoutChangingTheDocument()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);

        controller.BeginPole();
        controller.UpdatePointer(new DocumentPoint(13, 17), snapEnabled: true);

        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.NotEmpty(controller.CreatePreviewElements());
        Assert.Equal(
            new DocumentPoint(10, 20),
            controller.ResolvePlacementPosition(new DocumentPoint(13, 17), true));

        Assert.True(controller.Place(new DocumentPoint(13, 17), snapEnabled: true));
        Pole pole = Assert.Single(session.PersistenceSession.Domain.Devices.OfType<Pole>());
        Assert.Equal(new DocumentPoint(10, 20), session.Layout.DrawingLayout.Poles[pole.Id].Position);
        Assert.Single(session.CommandStack.History);
        Assert.True(session.CommandStack.IsDirty);

        controller.Cancel();
        Assert.Empty(controller.CreatePreviewElements());
    }

    [Fact]
    public void RingCabinetGhostUsesTheConfiguredIntervalsAndCommitsOnlyOnClick()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            5,
            includePTInterval: true,
            ptPlacement: RingCabinetPTPlacement.Left);
        var configuration = new RingCabinetCreationConfiguration("Ghost cabinet", template);

        controller.BeginRingCabinet(configuration);
        controller.UpdatePointer(new DocumentPoint(26, 34), snapEnabled: true);
        IReadOnlyList<SceneElement> preview = controller.CreatePreviewElements();

        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.Contains(preview.OfType<SceneText>(), text => text.Text == "PT");
        Assert.True(preview.OfType<SceneLine>().Count() > 5);

        Assert.True(controller.Place(new DocumentPoint(26, 34), snapEnabled: true));
        RingCabinet cabinet = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        Assert.Equal(5, cabinet.Intervals.Count);
        Assert.Equal(1, Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval).BayIndex);
        Assert.Equal(new DocumentPoint(30, 30), session.Layout.RingCabinetLayouts[cabinet.Id].Position);
        Assert.Single(session.CommandStack.History);
        Assert.Equal(PlacementMode.Idle, controller.Mode);
        Assert.Empty(controller.CreatePreviewElements());
    }

    [Fact]
    public void CancelClearsAnUncommittedRingCabinetGhost()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.Conventional,
            4);

        controller.BeginRingCabinet(new RingCabinetCreationConfiguration("Canceled", template));
        controller.UpdatePointer(new DocumentPoint(20, 20), snapEnabled: false);
        Assert.NotEmpty(controller.CreatePreviewElements());

        controller.Cancel();

        Assert.Equal(PlacementMode.Idle, controller.Mode);
        Assert.Empty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.False(session.CommandStack.IsDirty);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    public void TransformerPlacementUsesKindDefaultWithoutOrientationInput(
        TransformerKind kind,
        TransformerOrientation expectedOrientation)
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);

        controller.BeginTransformer(kind, "测试变压器");
        controller.UpdatePointer(new DocumentPoint(23, 37), snapEnabled: true);

        Assert.NotEmpty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.True(controller.Place(new DocumentPoint(23, 37), snapEnabled: true));

        Transformer transformer = Assert.Single(session.PersistenceSession.Domain.Transformers);
        Guid transformerId = transformer.Id;
        Guid terminalId = transformer.HvTerminalId;
        Assert.Equal(kind, transformer.TransformerKind);
        Assert.Equal("测试变压器", transformer.DisplayName);
        Assert.Equal(new DocumentPoint(20, 40), session.Layout.TransformerLayouts[transformer.Id].Position);
        Assert.Equal(expectedOrientation, session.Layout.TransformerLayouts[transformer.Id].Orientation);
        Assert.Equal(new SelectionReference(SelectionTargetKind.Device, transformer.Id), session.SelectionManager.Selected);
        Assert.Equal(PlacementMode.Idle, controller.Mode);

        Assert.True(session.CommandStack.Undo());
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.True(session.CommandStack.Redo());
        Transformer redone = Assert.Single(session.PersistenceSession.Domain.Transformers);
        Assert.Equal(transformerId, redone.Id);
        Assert.Equal(terminalId, redone.HvTerminalId);
        Assert.Equal("测试变压器", redone.DisplayName);
    }

    [Fact]
    public void CancelClearsNamedTransformerPreviewWithoutCreatingAggregate()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);

        controller.BeginTransformer(TransformerKind.PublicPoleMounted, "待取消变压器");
        controller.UpdatePointer(new DocumentPoint(23, 37), snapEnabled: true);
        Assert.Contains(controller.CreatePreviewElements().OfType<SceneText>(),
            text => text.Text == "待取消变压器");

        controller.Cancel();

        Assert.Equal(PlacementMode.Idle, controller.Mode);
        Assert.Empty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.False(session.CommandStack.IsDirty);
    }

    [Fact]
    public void TransformerInspectorRename_RefreshesSceneAndKeepsSelection()
    {
        ProjectRuntimeSession session = CreateSession();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(30, 40),
            "原名称");
        session.CommandStack.ExecuteCommand(
            new AddTransformerCommand(
                session.PersistenceSession.Domain,
                session.Layout,
                creation),
            session.RebuildScene);
        session.CommandStack.MarkSaved();
        var selected = new SelectionReference(
            SelectionTargetKind.Device,
            creation.Transformer.Id);
        session.SelectionManager.Select(selected);
        var editor = new PropertyEditor(
            session.SelectionResolver,
            session.CommandStack,
            session.Layout);

        Assert.True(editor.TryEdit(
            selected,
            PropertyCommandFactory.TransformerDisplayNamePropertyKey,
            "  新名称  ").IsSuccess);
        session.RebuildScene();

        Assert.Equal(selected, session.SelectionManager.Selected);
        Assert.Contains(session.Scene.Elements.OfType<SceneText>(),
            text => text.Text == "新名称");
        Assert.True(session.IsDirty);

        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        Assert.Equal("原名称", creation.Transformer.DisplayName);
        Assert.Equal(selected, session.SelectionManager.Selected);
        Assert.False(session.IsDirty);
        Assert.True(session.CommandStack.Redo());
        session.RebuildScene();
        Assert.Equal("新名称", creation.Transformer.DisplayName);
        Assert.Equal(selected, session.SelectionManager.Selected);
    }

    [Fact]
    public void UnifiedDeleteRemovesTransformerAndUndoRestoresExactIdentity()
    {
        ProjectRuntimeSession session = CreateSession();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(30, 40),
            "测试变压器");
        new AddTransformerCommand(session.PersistenceSession.Domain, session.Layout, creation).Execute();
        SelectionSet selection = SelectionSet.Create(
            [new SelectionReference(SelectionTargetKind.Device, creation.Transformer.Id)]);
        ICommand command = new SelectionDeletePlanner().Create(session, selection);

        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.Empty(session.Layout.TransformerLayouts);

        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        Assert.Same(creation.Transformer, Assert.Single(session.PersistenceSession.Domain.Transformers));
        Assert.Equal("测试变压器", creation.Transformer.DisplayName);
        Assert.Same(creation.HvTerminal, Assert.Single(session.PersistenceSession.Domain.Terminals));
        Assert.Same(creation.Layout, session.Layout.TransformerLayouts[creation.Transformer.Id]);
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CustomerStationPlacementCommitsAtSnappedPointAndAutoSelects(
        StationKind kind,
        int feederCount)
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);
        string[] names = feederCount == 1 ? ["主供"] : ["主供", "备供"];

        controller.BeginCustomerStation(kind, names);
        controller.UpdatePointer(new DocumentPoint(23, 37), snapEnabled: true);

        Assert.NotEmpty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.True(controller.Place(new DocumentPoint(23, 37), snapEnabled: true));
        CustomerStation station = Assert.Single(session.PersistenceSession.Domain.CustomerStations);
        Assert.Equal(feederCount, station.IncomingFeeders.Count);
        Assert.Equal(new DocumentPoint(20, 40), session.Layout.CustomerStationLayouts[station.Id].Position);
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.Equal(SwitchState.Open, feeder.IsolationSwitch.SwitchState);
            Assert.True(session.Layout.CustomerStationLayouts[station.Id]
                .IncomingFeeders[feeder.IncomingFeederId].ShowIncomingSwitch);
        });
        Assert.Equal(new SelectionReference(SelectionTargetKind.Device, station.Id),
            session.SelectionManager.Selected);
        Assert.Equal(PlacementMode.Idle, controller.Mode);
    }

    [Fact]
    public void UnifiedDeleteRemovesCustomerStationAndRejectsOwnedSwitchSelection()
    {
        ProjectRuntimeSession session = CreateSession();
        AddCustomerStationWithLayoutCommand add = new DeviceCommandFactory().CreateAddCustomerStation(
            session.PersistenceSession.Domain,
            session.Layout,
            StationKind.IndoorStation,
            ["主供", "备供"],
            new DocumentPoint(30, 40));
        add.Execute();
        CustomerStation station = add.Creation.CustomerStation;
        var planner = new SelectionDeletePlanner();

        Assert.Throws<InvalidOperationException>(() => planner.Create(
            session,
            SelectionSet.Create([new SelectionReference(
                SelectionTargetKind.Device,
                station.IncomingFeeders[0].IsolationSwitch.Id,
                station.IncomingFeeders[0].IncomingFeederId)])));
        ICommand remove = planner.Create(session, SelectionSet.Create([
            new SelectionReference(SelectionTargetKind.Device, station.Id)]));
        remove.Execute();
        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.Empty(session.Layout.CustomerStationLayouts);
        remove.Undo();
        Assert.Same(station, Assert.Single(session.PersistenceSession.Domain.CustomerStations));
        Assert.Same(add.Creation.Layout, session.Layout.CustomerStationLayouts[station.Id]);
        remove.Redo();
        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
    }

    [Fact]
    public void GroundingPickerUsesCurrentCustomerStationCableTerminalAnchor()
    {
        ProjectRuntimeSession session = CreateSession();
        AddCustomerStationWithLayoutCommand add = new DeviceCommandFactory().CreateAddCustomerStation(
            session.PersistenceSession.Domain,
            session.Layout,
            StationKind.IndoorStation,
            ["主供"],
            new DocumentPoint(100, 100));
        add.Execute();
        session.RebuildScene();
        IncomingFeeder feeder = Assert.Single(add.Creation.CustomerStation.IncomingFeeders);
        TerminalAnchor shown = Anchor(session, feeder.CableTerminalId);
        var picker = new GroundingTargetPicker();

        GroundingTargetCandidate shownCandidate = Assert.IsType<GroundingTargetCandidate>(
            picker.Resolve(session.PersistenceSession.Domain, session.Layout, session.Scene,
                shown.Position, 0.1));
        Assert.Equal(feeder.CableTerminalId, shownCandidate.Target.TargetId);

        new SetCustomerStationIncomingSwitchVisibilityCommand(
            session.Layout,
            add.Creation.CustomerStation,
            feeder.IncomingFeederId,
            false).Execute();
        session.RebuildScene();
        TerminalAnchor hidden = Anchor(session, feeder.CableTerminalId);
        GroundingTargetCandidate hiddenCandidate = Assert.IsType<GroundingTargetCandidate>(
            picker.Resolve(session.PersistenceSession.Domain, session.Layout, session.Scene,
                hidden.Position, 0.1));
        Assert.Equal(feeder.CableTerminalId, hiddenCandidate.Target.TargetId);
        Assert.NotEqual(shown.Position, hidden.Position);
        Assert.Null(picker.Resolve(session.PersistenceSession.Domain, session.Layout, session.Scene,
            add.Creation.Layout.Position, 0.1));
    }

    private static TerminalAnchor Anchor(ProjectRuntimeSession session, Guid terminalId)
    {
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            session.PersistenceSession.Domain,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts,
            session.PersistenceSession.Domain.Connections,
            session.PersistenceSession.Domain.CableSegments,
            session.Layout.TransformerLayouts,
            session.Layout.CustomerStationLayouts);
        Assert.True(anchors.TryGet(terminalId, out TerminalAnchor anchor));
        return anchor;
    }

    private ProjectRuntimeSession CreateSession()
    {
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(_filePath, "Placement preview");
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
