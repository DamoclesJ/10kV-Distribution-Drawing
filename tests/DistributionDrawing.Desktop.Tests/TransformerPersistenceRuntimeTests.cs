using System.IO;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Professional;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class TransformerPersistenceRuntimeTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"transformer-runtime-{Guid.NewGuid():N}.kvdrawing");

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical)]
    public void SaveReopen_PreservesTransformerAggregateAndLayout(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, kind.ToString()));
        TransformerCreation creation = new TransformerCreationFactory().Create(
            kind,
            new DocumentPoint(123.5, 456.25),
            orientation);
        new AddTransformerCommand(
            runtime.PersistenceSession.Domain,
            runtime.Layout,
            creation).Execute();

        ProjectLayoutSnapshot snapshot = ProjectLayoutRuntimeMapper.ToSnapshot(
            runtime.PersistenceSession.Domain,
            runtime.Layout);
        ProjectSession saved = service.SaveProject(snapshot);
        ProjectSession reopened = new ProjectService().LoadProject(saved.FilePath);
        RuntimeLayoutDocument reopenedLayout = ProjectLayoutRuntimeMapper.ToRuntime(
            reopened.Domain,
            reopened.Layout);

        Transformer transformer = Assert.IsType<Transformer>(Assert.Single(reopened.Domain.Devices));
        Assert.Equal(creation.Transformer.Id, transformer.Id);
        Assert.Equal(creation.Transformer.HvTerminalId, transformer.HvTerminalId);
        Assert.Equal(kind, transformer.TransformerKind);
        Assert.Equal(creation.HvTerminal.Id, Assert.Single(reopened.Domain.Terminals).Id);
        TransformerLayout layout = Assert.Single(reopenedLayout.TransformerLayouts).Value;
        Assert.Equal(creation.Layout.Position, layout.Position);
        Assert.Equal(orientation, layout.Orientation);
        Assert.Equal(ProjectFileFormat.Version7, reopened.Manifest.FormatVersion);
    }

    [Fact]
    public void InspectorOrientationCommand_SaveReopenPersistsPublicIndoorVerticalLayout()
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "transformer orientation"));
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(40, 50));
        new AddTransformerCommand(
            runtime.PersistenceSession.Domain,
            runtime.Layout,
            creation).Execute();
        runtime.CommandStack.ExecuteCommand(
            new SetTransformerOrientationCommand(
                runtime.Layout,
                creation.Transformer,
                TransformerOrientation.Vertical),
            runtime.RebuildScene);

        ProjectSession saved = service.SaveProject(ProjectLayoutRuntimeMapper.ToSnapshot(
            runtime.PersistenceSession.Domain,
            runtime.Layout));
        ProjectRuntimeSession reopened = ProjectRuntimeSession.Load(
            new ProjectService(),
            saved.FilePath);

        Transformer restored = Assert.Single(reopened.PersistenceSession.Domain.Transformers);
        Assert.Equal(creation.Transformer.Id, restored.Id);
        Assert.Equal(creation.HvTerminal.Id, restored.HvTerminalId);
        Assert.Equal(TransformerKind.PublicIndoor, restored.TransformerKind);
        Assert.Equal(
            TransformerOrientation.Vertical,
            reopened.Layout.TransformerLayouts[restored.Id].Orientation);
        Assert.Equal(ProjectFileFormat.Version7, reopened.PersistenceSession.Manifest.FormatVersion);
    }

    [Fact]
    public void SaveReopen_PreservesCableEndpointAndGraphConnectivity()
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "transformer graph"));
        TransformerCreation first = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor, new DocumentPoint(10, 20));
        TransformerCreation second = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor, new DocumentPoint(90, 20));
        new AddTransformerCommand(runtime.PersistenceSession.Domain, runtime.Layout, first).Execute();
        new AddTransformerCommand(runtime.PersistenceSession.Domain, runtime.Layout, second).Execute();
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            first.HvTerminal.Id,
            second.HvTerminal.Id,
            "10kV cable",
            "10kV");
        runtime.PersistenceSession.Domain.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(), "cable", "YJV", 80, "10kV", connectionId,
                first.HvTerminal.Id, second.HvTerminal.Id),
            connection);

        ProjectSession saved = service.SaveProject(ProjectLayoutRuntimeMapper.ToSnapshot(
            runtime.PersistenceSession.Domain,
            runtime.Layout));
        ProjectSession reopened = new ProjectService().LoadProject(saved.FilePath);
        ProjectRuntimeSession reopenedRuntime = ProjectRuntimeSession.Create(reopened);
        Connection restoredConnection = Assert.Single(reopened.Domain.Connections);
        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(reopened.Domain));

        Assert.Equal(first.HvTerminal.Id, restoredConnection.StartTerminalId);
        Assert.Equal(second.HvTerminal.Id, restoredConnection.EndTerminalId);
        Assert.True(query.IsConnected(first.HvTerminal.Id, second.HvTerminal.Id));
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            reopened.Domain,
            reopenedRuntime.Layout.DrawingLayout,
            reopenedRuntime.Layout.RingCabinetLayouts,
            reopened.Domain.Connections,
            reopened.Domain.CableSegments,
            reopenedRuntime.Layout.TransformerLayouts);
        TerminalAnchor firstAnchor = AssertAnchor(anchors, first.HvTerminal.Id);
        TerminalAnchor secondAnchor = AssertAnchor(anchors, second.HvTerminal.Id);
        Assert.Contains(firstAnchor.Position, Assert.Single(reopenedRuntime.Scene.Routes).Points);
        Assert.Contains(secondAnchor.Position, Assert.Single(reopenedRuntime.Scene.Routes).Points);
    }

    [Fact]
    public void SaveReopen_PreservesOverheadEndpointAndGraphConnectivityWithoutFakeAnchor()
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "transformer overhead graph"));
        AddPoleCommand pole = new DeviceCommandFactory().CreateAddPole(
            runtime.PersistenceSession.Domain,
            runtime.Layout,
            new DocumentPoint(10, 20));
        pole.Execute();
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(90, 20));
        new AddTransformerCommand(
            runtime.PersistenceSession.Domain,
            runtime.Layout,
            transformer).Execute();
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.OverheadLine,
            pole.Terminal.Id,
            transformer.HvTerminal.Id,
            "10kV overhead",
            "10kV");
        new AddOverheadLineCommand(
            runtime.PersistenceSession.Domain,
            runtime.Layout,
            connection,
            new OverheadLine(connectionId, "JKLYJ", [pole.Pole.Id]),
            new OverheadLineLayout(
                connectionId,
                pole.Layout.Position,
                transformer.Layout.Position)).Execute();

        ProjectLayoutSnapshot currentSnapshot = ProjectLayoutRuntimeMapper.ToSnapshot(
            runtime.PersistenceSession.Domain,
            runtime.Layout);
        ProjectOverheadLineLayoutDto currentLine = Assert.Single(currentSnapshot.OverheadLines);
        var sliceBLayout = new ProjectLayoutSnapshot(currentSnapshot.Data with
        {
            OverheadLines =
            [
                currentLine with
                {
                    Start = new ProjectPointDto(
                        pole.Layout.Position.XMillimeters,
                        pole.Layout.Position.YMillimeters),
                    End = new ProjectPointDto(
                        transformer.Layout.Position.XMillimeters,
                        transformer.Layout.Position.YMillimeters)
                }
            ]
        });
        ProjectSession saved = service.SaveProject(sliceBLayout);
        var reopenedService = new ProjectService();
        ProjectSession reopened = reopenedService.LoadProject(saved.FilePath);
        ProjectRuntimeSession reopenedRuntime = ProjectRuntimeSession.Create(reopened);
        Connection restoredConnection = Assert.Single(reopened.Domain.Connections);
        OverheadLine restoredLine = Assert.Single(reopened.Domain.OverheadLines);
        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(reopened.Domain));

        Assert.Equal(transformer.HvTerminal.Id, restoredConnection.EndTerminalId);
        Assert.Equal(pole.Pole.Id, Assert.Single(restoredLine.SupportPoleIds));
        Assert.True(query.IsConnected(pole.Terminal.Id, transformer.HvTerminal.Id));
        Assert.DoesNotContain(transformer.Transformer.Id, restoredLine.SupportPoleIds);
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            reopened.Domain,
            reopenedRuntime.Layout.DrawingLayout,
            reopenedRuntime.Layout.RingCabinetLayouts,
            reopened.Domain.Connections,
            reopened.Domain.CableSegments,
            reopenedRuntime.Layout.TransformerLayouts);
        TerminalAnchor transformerAnchor = AssertAnchor(anchors, transformer.HvTerminal.Id);
        Assert.Contains(transformerAnchor.Position, Assert.Single(reopenedRuntime.Scene.Routes).Points);
        ProjectSession resaved = reopenedService.SaveProject(
            ProjectLayoutRuntimeMapper.ToSnapshot(
                reopenedRuntime.PersistenceSession.Domain,
                reopenedRuntime.Layout));
        ProjectOverheadLineLayoutDto persistedLine = Assert.Single(resaved.Layout.OverheadLines);
        Assert.Equal(transformerAnchor.Position.XMillimeters, persistedLine.End.XMillimeters);
        Assert.Equal(transformerAnchor.Position.YMillimeters, persistedLine.End.YMillimeters);
        Assert.NotEqual(transformer.Layout.Position, transformerAnchor.Position);
    }

    private static TerminalAnchor AssertAnchor(TerminalAnchorIndex anchors, Guid terminalId)
    {
        Assert.True(anchors.TryGet(terminalId, out TerminalAnchor anchor));
        return anchor;
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
