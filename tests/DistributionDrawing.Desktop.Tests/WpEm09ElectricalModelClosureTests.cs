using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Workspace;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WpEm09ElectricalModelClosureTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"wp-em-09-closure-{Guid.NewGuid():N}.kvdrawing");
    private readonly string _legacyPath = Path.Combine(
        Path.GetTempPath(),
        $"wp-em-09-v6-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void FullPostV1Document_SaveCloseAndOpenWithFreshRuntime_PreservesElectricalClosureAndScene()
    {
        var service = new ProjectService();
        ProjectRuntimeSession session = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "WP-EM-09 full closure"),
            new DrawingSceneBuilder());
        var sourceDialogs = new LifecycleDialogs();
        using var sourceWorkspace = new ProjectWorkspaceController(
            sourceDialogs,
            new DrawingSceneBuilder());
        sourceWorkspace.Workspace.AddSession(new DocumentSession(service, session));
        var devices = new DeviceCommandFactory();
        AddRingCabinetCommand cabinetCommand = devices.CreateAddRingCabinet(
            session.PersistenceSession.Domain,
            session.Layout,
            new RingCabinetCreationConfiguration(
                "RC-closure",
                new RingCabinetCreationTemplateFactory().Create(
                    RingCabinetTemplateType.Conventional,
                    4),
                "10kV closure"),
            new DocumentPoint(20, 20));
        cabinetCommand.Execute();
        RingCabinet cabinet = cabinetCommand.Cabinet;
        RingCabinetInterval absentInterval = cabinet.Intervals[1];
        cabinet.SetIntervalCableTerminal(absentInterval.IntervalId, null);
        session.PersistenceSession.Domain.SynchronizeRingCabinetAggregate(cabinet);

        AddTransformerCommand indoorTransformer = devices.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicIndoor,
            new DocumentPoint(330, 20),
            "闭环公变");
        indoorTransformer.Execute();
        AddCustomerStationWithLayoutCommand boxStation = devices.CreateAddCustomerStation(
            session.PersistenceSession.Domain,
            session.Layout,
            StationKind.BoxStation,
            ["箱变主供"],
            new DocumentPoint(330, 180));
        boxStation.Execute();
        AddCustomerStationWithLayoutCommand dualStation = devices.CreateAddCustomerStation(
            session.PersistenceSession.Domain,
            session.Layout,
            StationKind.IndoorStation,
            ["室内主供", "室内备供"],
            new DocumentPoint(330, 340));
        dualStation.Execute();
        CustomerStation box = boxStation.Creation.CustomerStation;
        CustomerStation dual = dualStation.Creation.CustomerStation;
        session.PersistenceSession.Domain.ChangeSwitchState(
            box.IncomingFeeders[0].IsolationSwitch.Id,
            SwitchState.Closed);
        session.PersistenceSession.Domain.ChangeSwitchState(
            dual.IncomingFeeders[0].IsolationSwitch.Id,
            SwitchState.Closed);

        RingCabinetInterval[] cableIntervals = cabinet.Intervals
            .Where(interval => interval.HasCableTerminal)
            .ToArray();
        CableSegment transformerCable = AddCable(
            session,
            cableIntervals[0].CableTerminalId!.Value,
            indoorTransformer.Creation.HvTerminal.Id,
            "RC-T");
        CableSegment boxCable = AddCable(
            session,
            cableIntervals[1].CableTerminalId!.Value,
            box.IncomingFeeders[0].CableTerminalId,
            "RC-BOX");
        CableSegment dualCable = AddCable(
            session,
            cableIntervals[2].CableTerminalId!.Value,
            dual.IncomingFeeders[1].CableTerminalId,
            "RC-DUAL-BACKUP");
        session.Layout.SetCableRouteGuide(new CableRouteGuide(transformerCable.Id, 120));
        session.Layout.SetCableRouteGuide(new CableRouteGuide(boxCable.Id, 260));
        session.Layout.SetCableRouteGuide(new CableRouteGuide(dualCable.Id, 420));

        AddPoleCommand pole = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 560));
        pole.Execute();
        AddPoleSwitchAttachmentCommand fuse = devices.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand poleTransformer = devices.CreateAddTransformer(
            session.PersistenceSession.Domain,
            session.Layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(330, 560),
            "闭环柱上公变");
        poleTransformer.Execute();
        AddOverheadLineCommand overhead = new OverheadLineCommandFactory().CreateAdd(
            session.PersistenceSession.Domain,
            session.Layout,
            fuse.Creation.SecondTerminal.Id,
            poleTransformer.Creation.HvTerminal.Id,
            new DocumentPoint(35, 560),
            poleTransformer.Creation.Layout.Position);
        overhead.Execute();
        GroundingAccessPoint gap = session.PersistenceSession.Domain.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            overhead.Connection.Id,
            pole.Pole.Id,
            GroundingAdjacentEndpoint.ForTerminal(poleTransformer.Creation.HvTerminal.Id),
            GroundingAccessLineSide.TransformerSide,
            GroundingAccessPlacementSide.AdjacentEndpointSide);
        GroundingPoint grounding = session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "柱上公变侧",
            "S01");
        session.Layout.SetGroundingPointLayout(
            new GroundingPointLayout(grounding.GroundingPointId, new DocumentPoint(12, -8)));
        session.RebuildScene();
        Assert.Equal(4, session.Scene.Routes.Count);

        Guid cabinetId = cabinet.Id;
        Guid absentIntervalId = absentInterval.IntervalId;
        (Guid IntervalId, Guid CableTerminalId)[] presentIntervals = cableIntervals
            .Select(interval => (interval.IntervalId, interval.CableTerminalId!.Value))
            .ToArray();
        Guid indoorTransformerId = indoorTransformer.Creation.Transformer.Id;
        Guid indoorTransformerTerminalId = indoorTransformer.Creation.HvTerminal.Id;
        TransformerKind indoorTransformerKind = indoorTransformer.Creation.Transformer
            .TransformerKind;
        string indoorTransformerName = indoorTransformer.Creation.Transformer.DisplayName!;
        Guid poleTransformerId = poleTransformer.Creation.Transformer.Id;
        Guid poleTransformerTerminalId = poleTransformer.Creation.HvTerminal.Id;
        Guid boxStationId = box.Id;
        FeederIdentity boxFeeder = Identity(box.IncomingFeeders[0]);
        Guid dualStationId = dual.Id;
        FeederIdentity[] dualFeeders = dual.IncomingFeeders.Select(Identity).ToArray();
        Assert.Equal(SwitchState.Closed, boxFeeder.SwitchState);
        Assert.Equal(SwitchState.Closed, dualFeeders[0].SwitchState);
        Assert.Equal(SwitchState.Open, dualFeeders[1].SwitchState);
        CableIdentity[] cables =
        [
            Identity(transformerCable, 120),
            Identity(boxCable, 260),
            Identity(dualCable, 420)
        ];
        Guid poleId = pole.Pole.Id;
        Guid overheadConnectionId = overhead.Connection.Id;
        Guid overheadStartTerminalId = overhead.Connection.StartTerminalId;
        Guid overheadEndTerminalId = overhead.Connection.EndTerminalId;
        GapIdentity gapIdentity = new(
            gap.GroundingAccessPointId,
            gap.ConnectionId,
            gap.PoleId,
            gap.AdjacentEndpoint,
            gap.PlacementSide,
            gap.LineSide);
        Guid groundingPointId = grounding.GroundingPointId;
        GroundingTarget groundingTarget = grounding.Target;
        DocumentPoint cabinetPosition = cabinetCommand.Layout.Position;
        DocumentPoint indoorTransformerPosition = indoorTransformer.Creation.Layout.Position;
        DocumentPoint boxStationPosition = boxStation.Creation.Layout.Position;
        DocumentPoint dualStationPosition = dualStation.Creation.Layout.Position;
        DocumentPoint polePosition = pole.Layout.Position;
        DocumentPoint poleTransformerPosition = poleTransformer.Creation.Layout.Position;
        DocumentPoint groundingOffset = session.Layout.GroundingPointLayouts[
            groundingPointId].SymbolOffset;

        Assert.True(sourceWorkspace.SaveProject());
        Assert.True(sourceWorkspace.CloseCurrentProject());
        Assert.Null(sourceWorkspace.CurrentSession);
        Assert.Empty(sourceWorkspace.Workspace.Sessions);

        var reopenDialogs = new LifecycleDialogs();
        reopenDialogs.OpenPaths.Enqueue(_path);
        using var reopenedWorkspace = new ProjectWorkspaceController(
            reopenDialogs,
            new DrawingSceneBuilder());
        Assert.True(reopenedWorkspace.OpenProject());
        ProjectRuntimeSession reopened = Assert.IsType<ProjectRuntimeSession>(
            reopenedWorkspace.CurrentSession);
        reopened.RebuildScene();

        RingCabinet restoredCabinet = Assert.Single(
            reopened.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        Assert.Equal(cabinetId, restoredCabinet.Id);
        Assert.Null(restoredCabinet.Intervals.Single(interval =>
            interval.IntervalId == absentIntervalId).CableTerminalId);
        Assert.Equal(3, restoredCabinet.Intervals.Count(interval => interval.HasCableTerminal));
        Assert.Equal(
            presentIntervals,
            restoredCabinet.Intervals.Where(interval => interval.HasCableTerminal)
                .Select(interval => (interval.IntervalId, interval.CableTerminalId!.Value))
                .ToArray());
        Transformer restoredIndoor = reopened.PersistenceSession.Domain.Transformers.Single(
            transformer => transformer.Id == indoorTransformerId);
        Assert.Equal(indoorTransformerTerminalId, restoredIndoor.HvTerminalId);
        Assert.Equal(indoorTransformerName, restoredIndoor.DisplayName);
        Assert.Equal(indoorTransformerKind, restoredIndoor.TransformerKind);
        Transformer restoredPoleTransformer = reopened.PersistenceSession.Domain.Transformers
            .Single(transformer => transformer.Id == poleTransformerId);
        Assert.Equal(poleTransformerTerminalId, restoredPoleTransformer.HvTerminalId);
        Assert.Equal(TransformerKind.PublicPoleMounted,
            restoredPoleTransformer.TransformerKind);
        CustomerStation restoredBox = reopened.PersistenceSession.Domain.CustomerStations.Single(
            station => station.Id == boxStationId);
        Assert.Equal(StationKind.BoxStation, restoredBox.StationKind);
        Assert.Equal(boxFeeder, Identity(Assert.Single(restoredBox.IncomingFeeders)));
        CustomerStation restoredDual = reopened.PersistenceSession.Domain.CustomerStations.Single(
            station => station.Id == dualStationId);
        Assert.Equal(StationKind.IndoorStation, restoredDual.StationKind);
        Assert.Equal(dualFeeders, restoredDual.IncomingFeeders.Select(Identity).ToArray());
        foreach (CableIdentity expected in cables)
        {
            CableSegment restoredCable = reopened.PersistenceSession.Domain.CableSegments.Single(
                cable => cable.Id == expected.SegmentId);
            Assert.Equal(expected.ConnectionId, restoredCable.ConnectionId);
            Assert.Equal(expected.StartTerminalId, restoredCable.StartTerminalId);
            Assert.Equal(expected.EndTerminalId, restoredCable.EndTerminalId);
            Connection restoredConnection = reopened.PersistenceSession.Domain.Connections.Single(
                connection => connection.Id == expected.ConnectionId);
            Assert.Equal(expected.StartTerminalId, restoredConnection.StartTerminalId);
            Assert.Equal(expected.EndTerminalId, restoredConnection.EndTerminalId);
            Assert.Equal(expected.GuideY, reopened.Layout.CableRouteGuides[
                expected.SegmentId].HorizontalYMillimeters);
        }
        OverheadLine restoredLine = reopened.PersistenceSession.Domain.OverheadLines.Single(
            line => line.ConnectionId == overheadConnectionId);
        Assert.Equal(overheadConnectionId, restoredLine.ConnectionId);
        Assert.Contains(poleId, restoredLine.SupportPoleIds);
        Connection restoredOverheadConnection = reopened.PersistenceSession.Domain.Connections
            .Single(connection => connection.Id == overheadConnectionId);
        Assert.Equal(overheadStartTerminalId, restoredOverheadConnection.StartTerminalId);
        Assert.Equal(overheadEndTerminalId, restoredOverheadConnection.EndTerminalId);
        GroundingAccessPoint restoredGap = Assert.Single(
            reopened.PersistenceSession.Domain.GroundingAccessPoints);
        Assert.Equal(gapIdentity.GroundingAccessPointId,
            restoredGap.GroundingAccessPointId);
        Assert.Equal(gapIdentity.ConnectionId, restoredGap.ConnectionId);
        Assert.Equal(gapIdentity.PoleId, restoredGap.PoleId);
        Assert.Equal(gapIdentity.AdjacentEndpoint, restoredGap.AdjacentEndpoint);
        Assert.Equal(gapIdentity.PlacementSide, restoredGap.PlacementSide);
        Assert.Equal(gapIdentity.LineSide, restoredGap.LineSide);
        GroundingPoint restoredGrounding = Assert.Single(
            reopened.PersistenceSession.Domain.GroundingPoints);
        Assert.Equal(groundingPointId, restoredGrounding.GroundingPointId);
        Assert.Equal(groundingTarget.Kind, restoredGrounding.Target.Kind);
        Assert.Equal(groundingTarget.TargetId, restoredGrounding.Target.TargetId);
        Assert.Equal(groundingOffset, reopened.Layout.GroundingPointLayouts[
            groundingPointId].SymbolOffset);
        Assert.Equal(3, reopened.Layout.CableRouteGuides.Count);
        Assert.Equal(cabinetPosition,
            reopened.Layout.RingCabinetLayouts[cabinetId].Position);
        Assert.Equal(indoorTransformerPosition,
            reopened.Layout.TransformerLayouts[indoorTransformerId].Position);
        Assert.Equal(poleTransformerPosition,
            reopened.Layout.TransformerLayouts[poleTransformerId].Position);
        Assert.Equal(boxStationPosition,
            reopened.Layout.CustomerStationLayouts[boxStationId].Position);
        Assert.Equal(dualStationPosition,
            reopened.Layout.CustomerStationLayouts[dualStationId].Position);
        Assert.Equal(polePosition, reopened.Layout.DrawingLayout.Poles[poleId].Position);
        Assert.Equal(4, reopened.Scene.Routes.Count);
        Guid[] expectedRouteIds = [.. cables.Select(cable => cable.ConnectionId), overheadConnectionId];
        Assert.Equal(expectedRouteIds.OrderBy(id => id),
            reopened.Scene.Routes.Select(route => route.ConnectionId).OrderBy(id => id));
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            reopened.PersistenceSession.Domain,
            reopened.Layout.DrawingLayout,
            reopened.Layout.RingCabinetLayouts,
            reopened.PersistenceSession.Domain.Connections,
            reopened.PersistenceSession.Domain.CableSegments,
            reopened.Layout.TransformerLayouts,
            reopened.Layout.CustomerStationLayouts);
        foreach (Connection connection in reopened.PersistenceSession.Domain.Connections)
        {
            Assert.True(anchors.TryGet(connection.StartTerminalId, out TerminalAnchor start));
            Assert.True(anchors.TryGet(connection.EndTerminalId, out TerminalAnchor end));
            OrthogonalRoute route = reopened.Scene.Routes.Single(item =>
                item.ConnectionId == connection.Id);
            Assert.Contains(start.Position, route.Points);
            Assert.Contains(end.Position, route.Points);
        }
        Assert.Equal(ProjectFileFormat.Version8,
            reopened.PersistenceSession.Manifest.FormatVersion);
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

    private static JsonObject ReadJson(ZipArchive archive, string entryName)
    {
        using Stream stream = archive.GetEntry(entryName)!.Open();
        return Assert.IsType<JsonObject>(JsonNode.Parse(stream));
    }

    private static void ReplaceJson(ZipArchive archive, string entryName, JsonObject value)
    {
        archive.GetEntry(entryName)!.Delete();
        using Stream stream = archive.CreateEntry(entryName).Open();
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }

    private static FeederIdentity Identity(IncomingFeeder feeder) => new(
        feeder.IncomingFeederId,
        feeder.CableTerminalId,
        feeder.Sequence,
        feeder.IsolationSwitch.SwitchState);

    private static CableIdentity Identity(CableSegment cable, double guideY) => new(
        cable.Id,
        cable.ConnectionId,
        cable.StartTerminalId,
        cable.EndTerminalId,
        guideY);

    private sealed record FeederIdentity(
        Guid IncomingFeederId,
        Guid CableTerminalId,
        int Sequence,
        SwitchState? SwitchState);

    private sealed record CableIdentity(
        Guid SegmentId,
        Guid ConnectionId,
        Guid StartTerminalId,
        Guid EndTerminalId,
        double GuideY);

    private sealed record GapIdentity(
        Guid GroundingAccessPointId,
        Guid ConnectionId,
        Guid PoleId,
        GroundingAdjacentEndpoint AdjacentEndpoint,
        GroundingAccessPlacementSide PlacementSide,
        GroundingAccessLineSide LineSide);

    private sealed class LifecycleDialogs : IProjectWorkspaceDialogs
    {
        public Queue<string?> OpenPaths { get; } = new();

        public List<string> Errors { get; } = [];

        public NewProjectRequest? RequestNewProject() => null;

        public string? ChooseOpenProject() =>
            OpenPaths.Count == 0 ? null : OpenPaths.Dequeue();

        public string? ChooseSaveAs(string? currentFilePath) => null;

        public DirtyDecision ConfirmDirty(string operation) => DirtyDecision.Cancel;

        public void ShowError(string title, string message) => Errors.Add($"{title}: {message}");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_legacyPath)) File.Delete(_legacyPath);
    }
}
