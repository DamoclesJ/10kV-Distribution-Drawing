using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.CableConnection;
using DistributionDrawing.Desktop.CableTerminationCreation;
using DistributionDrawing.Desktop.ConnectionEditing;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Desktop.PoleSwitchCreation;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CustomerStationSelectionDeleteTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(
        Path.GetTempPath(),
        $"customer-station-selection-delete-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void SelectionDeletePlanner_FoldsSelectedBoxStationOwnedSwitchIntoParentDeletion()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        SelectionReference parent = StationReference(station);
        SelectionReference ownedSwitch = SwitchReference(feeder);
        session.SelectionManager.Replace([ownedSwitch, parent]);

        CreateDeleteCoordinator(session).RemoveSelected();

        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices,
            device => device.Id == feeder.IsolationSwitch.Id);
        Assert.DoesNotContain(station.Id, session.Layout.CustomerStationLayouts.Keys);
        Assert.Empty(session.SelectionManager.SelectionSet.SelectedReferences);
        Assert.Single(session.CommandStack.History);
        Assert.Equal(1, session.CommandStack.CurrentIndex);
    }

    [Fact]
    public void SelectionDeletePlanner_FoldsBothOwnedSwitchesForDualFeederIndoorStation()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(
            session,
            StationKind.IndoorStation,
            ["室内主供", "室内备供"]);
        SelectionReference[] ownedSwitches = station.IncomingFeeders
            .Select(SwitchReference)
            .ToArray();
        Assert.Equal(2, ownedSwitches.Length);
        session.SelectionManager.Replace([StationReference(station), .. ownedSwitches]);

        CreateDeleteCoordinator(session).RemoveSelected();

        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices,
            device => ownedSwitches.Any(reference => reference.ObjectId == device.Id));
        Assert.Empty(session.Layout.CustomerStationLayouts);
        Assert.Single(session.CommandStack.History);
    }

    [Fact]
    public void SelectionDeletePlanner_StillRejectsStandaloneCustomerStationOwnedSwitch()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        SelectionReference ownedSwitch = SwitchReference(feeder);
        session.SelectionManager.Select(ownedSwitch);
        int historyBefore = session.CommandStack.History.Count;
        int indexBefore = session.CommandStack.CurrentIndex;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        Assert.Contains("不能单独删除", exception.Message);
        Assert.Contains(station, session.PersistenceSession.Domain.CustomerStations);
        Assert.Contains(feeder.IsolationSwitch, session.PersistenceSession.Domain.Devices);
        Assert.Contains(station.Id, session.Layout.CustomerStationLayouts.Keys);
        Assert.Equal([ownedSwitch], session.SelectionManager.SelectionSet.SelectedReferences);
        Assert.Equal(historyBefore, session.CommandStack.History.Count);
        Assert.Equal(indexBefore, session.CommandStack.CurrentIndex);
    }

    [Fact]
    public void SelectionDeletePlanner_ProtectsUnselectedCableDependency()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        CableSegment cable = AddCableToStation(session, station.IncomingFeeders[0]);
        session.RebuildScene();
        SelectionReference[] selection = [
            StationReference(station),
            SwitchReference(station.IncomingFeeders[0])
        ];
        session.SelectionManager.Replace(selection);
        DomainSnapshot before = Capture(session);

        Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        Assert.Contains(cable, session.PersistenceSession.Domain.CableSegments);
        AssertUnchanged(session, before);
    }

    [Fact]
    public void SelectionDeletePlanner_DeletesParentAndExplicitlySelectedCableTogether()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        CableSegment cable = AddCableToStation(session, station.IncomingFeeders[0]);
        session.RebuildScene();
        session.SelectionManager.Replace([
            StationReference(station),
            SwitchReference(station.IncomingFeeders[0]),
            new SelectionReference(SelectionTargetKind.CableSegment, cable.Id)
        ]);

        CreateDeleteCoordinator(session).RemoveSelected();

        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.DoesNotContain(session.PersistenceSession.Domain.CableSegments,
            item => item.Id == cable.Id);
        Assert.DoesNotContain(session.PersistenceSession.Domain.Connections,
            item => item.Id == cable.ConnectionId);
        Assert.DoesNotContain(station.Id, session.Layout.CustomerStationLayouts.Keys);
        Assert.Single(session.CommandStack.History);
    }

    [Fact]
    public void SelectionDeletePlanner_DoesNotFoldSwitchOwnedByUnselectedCustomerStation()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation selectedStation = AddStation(
            session,
            StationKind.BoxStation,
            ["选中站进线"]);
        CustomerStation otherStation = AddStation(
            session,
            StationKind.BoxStation,
            ["未选中站进线"],
            new DocumentPoint(420, 80));
        IncomingFeeder otherFeeder = Assert.Single(otherStation.IncomingFeeders);
        SelectionReference[] selection = [
            StationReference(selectedStation),
            SwitchReference(otherFeeder)
        ];
        session.SelectionManager.Replace(selection);
        DomainSnapshot before = Capture(session);

        Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        AssertUnchanged(session, before);
        Assert.Contains(otherStation, session.PersistenceSession.Domain.CustomerStations);
    }

    [Fact]
    public void DeleteSelected_CtrlASelectionFoldsStationChildrenAndDeletesSelectedDependencies()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        AddRingAndCable(session, station.IncomingFeeders[0]);
        session.RebuildScene();
        IReadOnlyList<SelectionReference> selectAll = new SceneSelectionQuery()
            .SelectAll(session.Scene.HitTestIndex);
        Assert.Contains(selectAll, reference =>
            reference == StationReference(station));
        Assert.Contains(selectAll, reference =>
            reference == SwitchReference(station.IncomingFeeders[0]));
        Assert.Contains(selectAll, reference =>
            reference.Kind == SelectionTargetKind.CableSegment);
        Assert.Contains(selectAll, reference =>
            reference.Kind == SelectionTargetKind.RingCabinet);
        session.SelectionManager.Replace(selectAll);

        CreateDeleteCoordinator(session).RemoveSelected();

        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.Empty(session.PersistenceSession.Domain.CableSegments);
        Assert.Empty(session.PersistenceSession.Domain.Connections);
        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.Layout.CustomerStationLayouts);
        Assert.Empty(session.Layout.RingCabinetLayouts);
        Assert.Empty(session.Layout.CableRouteGuides);
        Assert.Empty(session.SelectionManager.SelectionSet.SelectedReferences);
        Assert.Single(session.CommandStack.History);
    }

    [Fact]
    public void SelectionDeletePlanner_UndoRedoRestoresCustomerStationStableIdentities()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        CableSegment cable = AddCableToStation(session, feeder);
        session.Layout.SetCableRouteGuide(new CableRouteGuide(cable.Id, 260));
        session.RebuildScene();
        CustomerStationLayout stationLayout = session.Layout.CustomerStationLayouts[station.Id];
        Connection connection = session.PersistenceSession.Domain.Connections
            .Single(item => item.Id == cable.ConnectionId);
        Guid stationId = station.Id;
        Guid feederId = feeder.IncomingFeederId;
        Guid switchId = feeder.IsolationSwitch.Id;
        Guid cableTerminalId = feeder.CableTerminalId;
        Guid stationTerminalId = feeder.StationTerminalId;
        Guid cableId = cable.Id;
        Guid connectionId = connection.Id;
        session.SelectionManager.Replace([
            StationReference(station),
            SwitchReference(feeder),
            new SelectionReference(SelectionTargetKind.CableSegment, cableId)
        ]);

        CreateDeleteCoordinator(session).RemoveSelected();
        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.Single(session.CommandStack.History);

        Assert.True(session.CommandStack.Undo(session.RebuildScene));
        CustomerStation restored = Assert.Single(session.PersistenceSession.Domain.CustomerStations);
        IncomingFeeder restoredFeeder = Assert.Single(restored.IncomingFeeders);
        CableSegment restoredCable = Assert.Single(session.PersistenceSession.Domain.CableSegments);
        Assert.Equal(stationId, restored.Id);
        Assert.Equal(feederId, restoredFeeder.IncomingFeederId);
        Assert.Equal(switchId, restoredFeeder.IsolationSwitch.Id);
        Assert.Equal(cableTerminalId, restoredFeeder.CableTerminalId);
        Assert.Equal(stationTerminalId, restoredFeeder.StationTerminalId);
        Assert.Equal(cableId, restoredCable.Id);
        Assert.Equal(connectionId, restoredCable.ConnectionId);
        Assert.Same(connection, Assert.Single(session.PersistenceSession.Domain.Connections));
        Assert.Same(stationLayout, session.Layout.CustomerStationLayouts[stationId]);
        Assert.Equal(260, session.Layout.CableRouteGuides[cableId].HorizontalYMillimeters);
        Assert.Equal(0, session.CommandStack.CurrentIndex);

        Assert.True(session.CommandStack.Redo(session.RebuildScene));
        Assert.Empty(session.PersistenceSession.Domain.CustomerStations);
        Assert.Empty(session.PersistenceSession.Domain.CableSegments);
        Assert.Empty(session.PersistenceSession.Domain.Connections);
        Assert.DoesNotContain(stationId, session.Layout.CustomerStationLayouts.Keys);
        Assert.DoesNotContain(cableId, session.Layout.CableRouteGuides.Keys);
        Assert.Equal(1, session.CommandStack.CurrentIndex);
    }

    [Fact]
    public void SelectionDeletePlanner_RollsBackEarlierSelectedCableWhenAnotherCableBlocksAggregate()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(
            session,
            StationKind.IndoorStation,
            ["主供", "备供"]);
        IncomingFeeder[] feeders = station.IncomingFeeders.ToArray();
        CableSegment[] cables = feeders
            .Select(feeder => AddCableToStation(session, feeder))
            .ToArray();
        session.Layout.SetCableRouteGuide(new CableRouteGuide(cables[0].Id, 240));
        session.RebuildScene();
        SelectionReference[] selection = [
            StationReference(station),
            .. feeders.Select(SwitchReference),
            new SelectionReference(SelectionTargetKind.CableSegment, cables[0].Id)
        ];
        session.SelectionManager.Replace(selection);
        DomainSnapshot before = Capture(session);

        Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        AssertUnchanged(session, before);
        Assert.Equal(2, session.PersistenceSession.Domain.CableSegments.Count);
        Assert.Contains(cables[0].Id, session.Layout.CableRouteGuides.Keys);
    }

    [Fact]
    public void SelectionDeletePlanner_ProtectsUnselectedGroundingPointDependency()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(session, StationKind.BoxStation, ["箱站进线"]);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        var groundingPoint = session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(),
            feeder.CableTerminalId,
            "用户站进线",
            "S01");
        session.SelectionManager.Replace([
            StationReference(station),
            SwitchReference(feeder)
        ]);
        DomainSnapshot before = Capture(session);

        Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        AssertUnchanged(session, before);
        Assert.Contains(groundingPoint, session.PersistenceSession.Domain.GroundingPoints);
    }

    [Fact]
    public void SelectionDeletePlanner_ProtectsUnselectedWorkScopeDependency()
    {
        ProjectRuntimeSession session = CreateSession();
        CustomerStation station = AddStation(
            session,
            StationKind.IndoorStation,
            ["主供", "备供"]);
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        var workScope = session.PersistenceSession.Domain.CreateWorkScope(
            Guid.NewGuid(),
            new BoundaryPoint(first.IsolationSwitch.Id, first.CableTerminalId, "主供侧"),
            new BoundaryPoint(second.IsolationSwitch.Id, second.CableTerminalId, "备供侧"),
            "双电源站工作范围");
        session.SelectionManager.Replace([
            StationReference(station),
            SwitchReference(first),
            SwitchReference(second)
        ]);
        DomainSnapshot before = Capture(session);

        Assert.Throws<InvalidOperationException>(
            () => CreateDeleteCoordinator(session).RemoveSelected());

        AssertUnchanged(session, before);
        Assert.Contains(workScope, session.PersistenceSession.Domain.WorkScopes);
    }

    private ProjectRuntimeSession CreateSession()
    {
        var service = new ProjectService();
        return ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_projectPath, "CustomerStation selection deletion"),
            new DrawingSceneBuilder());
    }

    private static CustomerStation AddStation(
        ProjectRuntimeSession session,
        StationKind kind,
        IReadOnlyList<string> feederNames,
        DocumentPoint? position = null)
    {
        var command = new DeviceCommandFactory().CreateAddCustomerStation(
            session.PersistenceSession.Domain,
            session.Layout,
            kind,
            feederNames,
            position ?? new DocumentPoint(420, 80));
        command.Execute();
        return command.Creation.CustomerStation;
    }

    private static (RingCabinet Cabinet, CableSegment Cable) AddRingAndCable(
        ProjectRuntimeSession session,
        IncomingFeeder feeder)
    {
        var command = new DeviceCommandFactory().CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "删除测试环网柜",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(20, 80));
        command.Execute();
        RingCabinetInterval interval = command.Cabinet.Intervals
            .First(item => item.CableTerminalId is not null);
        CableSegment cable = AddCable(
            session,
            interval.CableTerminalId!.Value,
            feeder.CableTerminalId,
            "测试电缆");
        return (command.Cabinet, cable);
    }

    private static CableSegment AddCableToStation(
        ProjectRuntimeSession session,
        IncomingFeeder feeder)
    {
        var command = new DeviceCommandFactory().CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                $"电缆源-{Guid.NewGuid():N}",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    3)),
            new DocumentPoint(20, 80 + session.PersistenceSession.Domain.CableSegments.Count * 180));
        command.Execute();
        RingCabinetInterval interval = command.Cabinet.Intervals
            .First(item => item.CableTerminalId is not null);
        return AddCable(
            session,
            interval.CableTerminalId!.Value,
            feeder.CableTerminalId,
            $"测试电缆-{session.PersistenceSession.Domain.CableSegments.Count + 1}");
    }

    private static CableSegment AddCable(
        ProjectRuntimeSession session,
        Guid startTerminalId,
        Guid endTerminalId,
        string name)
    {
        Guid connectionId = Guid.NewGuid();
        var cable = new CableSegment(
            Guid.NewGuid(),
            name,
            "YJV22",
            100,
            "10kV",
            connectionId,
            startTerminalId,
            endTerminalId);
        session.PersistenceSession.Domain.AddCableSegment(
            cable,
            new Connection(
                connectionId,
                ConnectionType.Cable,
                startTerminalId,
                endTerminalId,
                name,
                "10kV"));
        return cable;
    }

    private static DrawingToolCoordinator CreateDeleteCoordinator(ProjectRuntimeSession session)
    {
        return new DrawingToolCoordinator(
            new PlacementController(() => session),
            new OverheadLineConnectionController(() => session),
            new CableTerminationAttachmentController(() => session),
            new CableConnectionController(() => session),
            new CableReconnectController(() => session),
            new PoleSwitchAttachmentController(() => session),
            () => session);
    }

    private static SelectionReference StationReference(CustomerStation station) =>
        new(SelectionTargetKind.Device, station.Id);

    private static SelectionReference SwitchReference(IncomingFeeder feeder) =>
        new(
            SelectionTargetKind.Device,
            feeder.IsolationSwitch.Id,
            feeder.IncomingFeederId);

    private static DomainSnapshot Capture(ProjectRuntimeSession session)
    {
        var document = session.PersistenceSession.Domain;
        return new DomainSnapshot(
            document.Devices.Select(item => item.Id).Order().ToArray(),
            document.Terminals.Select(item => item.Id).Order().ToArray(),
            document.Connections.Select(item => item.Id).Order().ToArray(),
            document.CableSegments.Select(item => item.Id).Order().ToArray(),
            document.CustomerStations.Select(item => item.Id).Order().ToArray(),
            document.Devices.OfType<RingCabinet>().Select(item => item.Id).Order().ToArray(),
            document.GroundingPoints.Select(item => item.GroundingPointId).Order().ToArray(),
            document.GroundingAccessPoints.Select(item => item.GroundingAccessPointId).Order().ToArray(),
            document.WorkScopes.Select(item => item.WorkScopeId).Order().ToArray(),
            session.Layout.CustomerStationLayouts.OrderBy(item => item.Key).ToArray(),
            session.Layout.RingCabinetLayouts.OrderBy(item => item.Key).ToArray(),
            session.Layout.CableRouteGuides.OrderBy(item => item.Key).ToArray(),
            session.Layout.GroundingPointLayouts.OrderBy(item => item.Key).ToArray(),
            session.SelectionManager.SelectionSet.SelectedReferences.ToArray(),
            session.CommandStack.History.Count,
            session.CommandStack.CurrentIndex);
    }

    private static void AssertUnchanged(ProjectRuntimeSession session, DomainSnapshot before)
    {
        Assert.Equal(before.DeviceIds,
            session.PersistenceSession.Domain.Devices.Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.TerminalIds,
            session.PersistenceSession.Domain.Terminals.Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.ConnectionIds,
            session.PersistenceSession.Domain.Connections.Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.CableIds,
            session.PersistenceSession.Domain.CableSegments.Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.CustomerStationIds,
            session.PersistenceSession.Domain.CustomerStations.Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.RingCabinetIds,
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>()
                .Select(item => item.Id).Order().ToArray());
        Assert.Equal(before.GroundingPointIds,
            session.PersistenceSession.Domain.GroundingPoints
                .Select(item => item.GroundingPointId).Order().ToArray());
        Assert.Equal(before.GroundingAccessPointIds,
            session.PersistenceSession.Domain.GroundingAccessPoints
                .Select(item => item.GroundingAccessPointId).Order().ToArray());
        Assert.Equal(before.WorkScopeIds,
            session.PersistenceSession.Domain.WorkScopes
                .Select(item => item.WorkScopeId).Order().ToArray());
        Assert.Equal(before.CustomerStationLayouts,
            session.Layout.CustomerStationLayouts.OrderBy(item => item.Key).ToArray());
        Assert.Equal(before.RingCabinetLayouts,
            session.Layout.RingCabinetLayouts.OrderBy(item => item.Key).ToArray());
        Assert.Equal(before.CableRouteGuides,
            session.Layout.CableRouteGuides.OrderBy(item => item.Key).ToArray());
        Assert.Equal(before.GroundingPointLayouts,
            session.Layout.GroundingPointLayouts.OrderBy(item => item.Key).ToArray());
        Assert.Equal(before.Selection,
            session.SelectionManager.SelectionSet.SelectedReferences.ToArray());
        Assert.Equal(before.HistoryCount, session.CommandStack.History.Count);
        Assert.Equal(before.CurrentIndex, session.CommandStack.CurrentIndex);
    }

    public void Dispose()
    {
        if (File.Exists(_projectPath))
        {
            File.Delete(_projectPath);
        }
    }

    private sealed record DomainSnapshot(
        Guid[] DeviceIds,
        Guid[] TerminalIds,
        Guid[] ConnectionIds,
        Guid[] CableIds,
        Guid[] CustomerStationIds,
        Guid[] RingCabinetIds,
        Guid[] GroundingPointIds,
        Guid[] GroundingAccessPointIds,
        Guid[] WorkScopeIds,
        KeyValuePair<Guid, CustomerStationLayout>[] CustomerStationLayouts,
        KeyValuePair<Guid, RingCabinetLayout>[] RingCabinetLayouts,
        KeyValuePair<Guid, CableRouteGuide>[] CableRouteGuides,
        KeyValuePair<Guid, GroundingPointLayout>[] GroundingPointLayouts,
        SelectionReference[] Selection,
        int HistoryCount,
        int CurrentIndex);
}
