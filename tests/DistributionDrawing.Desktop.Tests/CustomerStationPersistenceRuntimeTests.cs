using System.IO;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class CustomerStationPersistenceRuntimeTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"customer-station-runtime-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void SaveReopen_PreservesDualAggregateLayoutAndGraph()
    {
        var service = new ProjectService();
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(
            service.CreateProject(_path, "dual customer station"));
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供", "备供"]);
        runtime.PersistenceSession.Domain.AddCustomerStation(station);
        runtime.PersistenceSession.Domain.ChangeSwitchState(
            station.IncomingFeeders[0].IsolationSwitch.Id,
            SwitchState.Closed);
        runtime.Layout.AddCustomerStation(
            new CustomerStationLayout(
                station.Id,
                new DocumentPoint(123.5, 456.25),
                [
                    new CustomerStationIncomingFeederLayout(
                        station.IncomingFeeders[0].IncomingFeederId,
                        true),
                    new CustomerStationIncomingFeederLayout(
                        station.IncomingFeeders[1].IncomingFeederId,
                        false)
                ]),
            station);

        ProjectSession saved = service.SaveProject(ProjectLayoutRuntimeMapper.ToSnapshot(
            runtime.PersistenceSession.Domain,
            runtime.Layout));
        ProjectRuntimeSession reopened = ProjectRuntimeSession.Load(
            new ProjectService(),
            saved.FilePath);

        CustomerStation restored = Assert.Single(
            reopened.PersistenceSession.Domain.CustomerStations);
        CustomerStationLayout restoredLayout = Assert.Single(
            reopened.Layout.CustomerStationLayouts).Value;
        Assert.Equal(station.Id, restored.Id);
        Assert.Equal(StationKind.IndoorStation, restored.StationKind);
        Assert.Equal(["主供", "备供"],
            restored.IncomingFeeders.Select(feeder => feeder.DisplayName).ToArray());
        Assert.Equal(
            station.IncomingFeeders.Select(Identity),
            restored.IncomingFeeders.Select(Identity));
        Assert.Equal(SwitchState.Closed, restored.IncomingFeeders[0].IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, restored.IncomingFeeders[1].IsolationSwitch.SwitchState);
        Assert.Equal(new DocumentPoint(123.5, 456.25), restoredLayout.Position);
        Assert.True(restoredLayout.IncomingFeeders[
            restored.IncomingFeeders[0].IncomingFeederId].ShowIncomingSwitch);
        Assert.False(restoredLayout.IncomingFeeders[
            restored.IncomingFeeders[1].IncomingFeederId].ShowIncomingSwitch);

        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(
                reopened.PersistenceSession.Domain));
        Assert.True(query.IsConnected(
            restored.IncomingFeeders[0].CableTerminalId,
            restored.IncomingFeeders[0].StationTerminalId));
        Assert.False(query.IsConnected(
            restored.IncomingFeeders[1].CableTerminalId,
            restored.IncomingFeeders[1].StationTerminalId));
        Assert.False(query.IsConnected(
            restored.IncomingFeeders[0].CableTerminalId,
            restored.IncomingFeeders[1].CableTerminalId));
        Assert.Equal(ProjectFileFormat.Version7,
            reopened.PersistenceSession.Manifest.FormatVersion);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static object Identity(IncomingFeeder feeder) => new
    {
        feeder.IncomingFeederId,
        feeder.Sequence,
        feeder.CableTerminalId,
        feeder.StationTerminalId,
        feeder.ElectricalNodeId,
        SwitchId = feeder.IsolationSwitch.Id,
        OwnerId = feeder.IsolationSwitch.ParentId
    };
}
