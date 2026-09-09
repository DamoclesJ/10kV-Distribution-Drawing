using System.IO;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WpEm05GroundingInteractionTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void RuntimeMapperAndSaveReopen_PreserveGroundingOffsetInV7()
    {
        string path = NewPath();
        var service = new ProjectService();
        ProjectRuntimeSession session = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(path, "WP-EM-05 persistence"));
        AddPoleCommand pole = new DeviceCommandFactory().CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(30, 40));
        pole.Execute();
        GroundingPoint point = session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(),
            pole.Terminal.Id,
            "测试位置",
            "S01");
        var expected = new GroundingPointLayout(
            point.GroundingPointId,
            new DocumentPoint(17.25, -8.5));
        session.Layout.SetGroundingPointLayout(expected);

        ProjectLayoutSnapshot snapshot = ProjectLayoutRuntimeMapper.ToSnapshot(
            session.PersistenceSession.Domain,
            session.Layout);
        ProjectGroundingPointLayoutDto dto = Assert.Single(snapshot.GroundingPointLayouts);
        Assert.Equal(point.GroundingPointId, dto.GroundingPointId);
        Assert.Equal(expected.SymbolOffset.XMillimeters, dto.SymbolOffset.XMillimeters);
        Assert.Equal(expected.SymbolOffset.YMillimeters, dto.SymbolOffset.YMillimeters);

        service.SaveProject(snapshot);
        var reopenedService = new ProjectService();
        ProjectRuntimeSession reopened = ProjectRuntimeSession.Load(reopenedService, path);

        Assert.Equal(ProjectFileFormat.Version7, reopened.PersistenceSession.OpenedFormatVersion);
        Assert.Equal(ProjectFileFormat.Version7, ProjectFileFormat.CurrentVersion);
        Assert.Equal(
            expected,
            reopened.Layout.GroundingPointLayouts[point.GroundingPointId]);
        ProjectLayoutSnapshot reopenedSnapshot = ProjectLayoutRuntimeMapper.ToSnapshot(
            reopened.PersistenceSession.Domain,
            reopened.Layout);
        Assert.Single(reopenedSnapshot.GroundingPointLayouts);
    }

    [Fact]
    public void MissingLayoutRecord_LeavesRuntimeWithoutManualOverride()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "No manual layout");
        RuntimeLayoutDocument runtime = ProjectLayoutRuntimeMapper.ToRuntime(
            document,
            ProjectLayoutSnapshot.Empty(document.Id));

        Assert.Empty(runtime.GroundingPointLayouts);
    }

    [Fact]
    public void GapTargetPicker_UsesToleranceReportsOccupiedAndKeepsIndependentIdentity()
    {
        string path = NewPath();
        ProjectRuntimeSession session = ProjectRuntimeSession.CreateEmpty(
            new ProjectService().CreateProject(path, "target picker"),
            new DrawingSceneBuilder());
        var devices = new DeviceCommandFactory();
        AddPoleCommand start = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(10, 10));
        AddPoleCommand end = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(150, 10));
        start.Execute();
        end.Execute();
        Guid connectionId = Guid.NewGuid();
        session.PersistenceSession.Domain.AddConnection(new Connection(
            connectionId,
            ConnectionType.OverheadLine,
            start.Terminal.Id,
            end.Terminal.Id,
            "测试线路",
            "10kV"));
        session.PersistenceSession.Domain.AddOverheadLine(new OverheadLine(
            connectionId,
            "JKLYJ",
            [start.Pole.Id, end.Pole.Id]));
        session.Layout.DrawingLayout.Add(new OverheadLineLayout(
            connectionId,
            start.Layout.Position,
            end.Layout.Position));
        GroundingAccessPoint gap = session.PersistenceSession.Domain.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            connectionId,
            start.Pole.Id,
            end.Pole.Id,
            GroundingAccessLineSide.LargerNumberSide);
        session.RebuildScene();
        SceneEllipse marker = Assert.Single(session.Scene.Elements.OfType<SceneEllipse>(),
            element => element.TargetId == gap.GroundingAccessPointId);
        DocumentPoint center = new(
            marker.Bounds.XMillimeters + marker.Bounds.WidthMillimeters / 2,
            marker.Bounds.YMillimeters + marker.Bounds.HeightMillimeters / 2);
        var picker = new GroundingTargetPicker();

        GroundingTargetCandidate candidate = Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            new DocumentPoint(center.XMillimeters + 1, center.YMillimeters),
            2));
        Assert.Equal(GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId), candidate.Target);
        Assert.False(candidate.IsOccupied);
        Assert.Null(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            new DocumentPoint(center.XMillimeters + 3, center.YMillimeters),
            2));

        session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(), candidate.Target, "测试位置", "L01");
        GroundingTargetCandidate occupied = Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            center,
            2));
        Assert.True(occupied.IsOccupied);
        Assert.Single(picker.CreateAffordance(occupied));
    }

    [Fact]
    public void CableTargetPicker_UsesFrozenWhitelistAndDeterministicNearestCandidate()
    {
        string path = NewPath();
        ProjectRuntimeSession session = ProjectRuntimeSession.CreateEmpty(
            new ProjectService().CreateProject(path, "cable target picker"),
            new DrawingSceneBuilder());
        var devices = new DeviceCommandFactory();
        AddPoleCommand pole = devices.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 20));
        pole.Execute();
        AddCableTerminationAttachmentCommand termination =
            devices.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "终端",
                new DocumentPoint(10, -15));
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
        session.RebuildScene();
        Guid terminationCableId = termination.Creation.CableSideTerminal.Id;
        Guid ringCableId = cabinet.Cabinet.Intervals[0].CableTerminalId!.Value;
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            session.PersistenceSession.Domain,
            session.Layout.DrawingLayout,
            session.Layout.RingCabinetLayouts);
        Assert.True(anchors.TryGet(terminationCableId, out TerminalAnchor terminationAnchor));
        Assert.True(anchors.TryGet(ringCableId, out TerminalAnchor ringAnchor));
        Assert.True(anchors.TryGet(pole.Terminal.Id, out TerminalAnchor illegalAnchor));
        var picker = new GroundingTargetPicker();

        GroundingTargetCandidate terminationCandidate =
            Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
                session.PersistenceSession.Domain,
                session.Layout,
                session.Scene,
                terminationAnchor.Position,
                1));
        Assert.Equal(GroundingTarget.ForTerminal(terminationCableId), terminationCandidate.Target);
        GroundingTargetCandidate ringCandidate =
            Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
                session.PersistenceSession.Domain,
                session.Layout,
                session.Scene,
                ringAnchor.Position,
                1));
        Assert.Equal(GroundingTarget.ForTerminal(ringCableId), ringCandidate.Target);
        Assert.Null(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            illegalAnchor.Position,
            0.5));

        DocumentPoint nearerRing = new(
            ringAnchor.Position.XMillimeters - 0.25,
            ringAnchor.Position.YMillimeters);
        GroundingTargetCandidate first = Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            nearerRing,
            500));
        GroundingTargetCandidate second = Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            nearerRing,
            500));
        Assert.Equal(GroundingTarget.ForTerminal(ringCableId), first.Target);
        Assert.Equal(first, second);

        session.PersistenceSession.Domain.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForTerminal(ringCableId),
            "环网柜电缆侧",
            "S01");
        GroundingTargetCandidate occupied = Assert.IsType<GroundingTargetCandidate>(picker.Resolve(
            session.PersistenceSession.Domain,
            session.Layout,
            session.Scene,
            ringAnchor.Position,
            1));
        Assert.True(occupied.IsOccupied);
    }

    private string NewPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wp-em-05-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists)) File.Delete(path);
    }
}
