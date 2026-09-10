using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class CustomerStationApplicationTests
{
    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CreationFactory_CreatesFrozenAggregate(
        StationKind stationKind,
        int feederCount)
    {
        string[] names = Enumerable.Range(1, feederCount)
            .Select(index => $"  进线 {index}  ")
            .ToArray();

        CustomerStation station = new CustomerStationCreationFactory().Create(
            stationKind,
            names);

        Assert.Equal(stationKind, station.StationKind);
        Assert.Null(station.DisplayName);
        Assert.Equal(feederCount, station.IncomingFeeders.Count);
        Assert.Equal(
            Enumerable.Range(1, feederCount),
            station.IncomingFeeders.Select(feeder => feeder.Sequence));
        Assert.Equal(
            Enumerable.Range(1, feederCount).Select(index => $"进线 {index}"),
            station.IncomingFeeders.Select(feeder => feeder.DisplayName));
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.Equal(SwitchState.Open, feeder.IsolationSwitch.SwitchState);
            Assert.Equal(SwitchKind.IsolationSwitch, feeder.IsolationSwitch.SwitchKind);
            Assert.Equal(
                SwitchInstallationType.CustomerStationIncomingFeeder,
                feeder.IsolationSwitch.InstallationType);
            Assert.Equal(feeder.IncomingFeederId, feeder.IsolationSwitch.ParentId);
            Assert.Null(feeder.ElectricalNode.ElectricalState);
        });
        Guid[] ids = [
            station.Id,
            .. station.IncomingFeeders.SelectMany(feeder => new[]
            {
                feeder.IncomingFeederId,
                feeder.IsolationSwitch.Id,
                feeder.CableTerminalId,
                feeder.StationTerminalId,
                feeder.ElectricalNodeId
            })
        ];
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void CreationFactory_RejectsInvalidCountsAndBlankName()
    {
        var factory = new CustomerStationCreationFactory();

        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(StationKind.BoxStation, []));
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(StationKind.BoxStation, ["A", "B"]));
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(StationKind.IndoorStation, []));
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(StationKind.IndoorStation, ["A", "B", "C"]));
        Assert.Throws<ArgumentException>(() =>
            factory.Create(StationKind.IndoorStation, ["A", " "]));
    }

    [Fact]
    public void AddCommand_ExecuteUndoRedoPreservesAllStableIds()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = CreateDualStation();
        Guid[] stableIds = CaptureStableIds(station);
        var command = new AddCustomerStationCommand(document, station);

        command.Execute();
        AssertPresent(document, station);
        command.Undo();
        AssertAbsent(document, station);
        command.Redo();
        AssertPresent(document, station);

        Assert.Equal(stableIds, CaptureStableIds(station));
        Assert.Same(station, Assert.Single(document.CustomerStations));
    }

    [Fact]
    public void RemoveCommand_ExecuteUndoRedoPreservesIdsStatesAndNames()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = CreateDualStation();
        document.AddCustomerStation(station);
        IncomingFeeder first = station.IncomingFeeders[0];
        document.ChangeSwitchState(first.IsolationSwitch.Id, SwitchState.Closed);
        station.RenameIncomingFeeder(first.IncomingFeederId, "主供");
        Guid[] stableIds = CaptureStableIds(station);
        var command = new RemoveCustomerStationCommand(document, station.Id);

        command.Execute();
        AssertAbsent(document, station);
        command.Undo();
        AssertPresent(document, station);
        Assert.Equal(SwitchState.Closed, first.IsolationSwitch.SwitchState);
        Assert.Equal("主供", first.DisplayName);
        Assert.Equal(stableIds, CaptureStableIds(station));
        command.Redo();
        AssertAbsent(document, station);
    }

    [Fact]
    public void RenameCommand_TrimsAndChangesOnlySelectedFeederAcrossUndoRedo()
    {
        CustomerStation station = CreateDualStation();
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        string secondName = second.DisplayName;
        var command = new RenameIncomingFeederCommand(
            station,
            first.IncomingFeederId,
            "  备用电源  ");

        command.Execute();
        Assert.Equal("备用电源", first.DisplayName);
        Assert.Equal(secondName, second.DisplayName);
        command.Undo();
        Assert.Equal("进线 A", first.DisplayName);
        Assert.Equal(secondName, second.DisplayName);
        command.Redo();
        Assert.Equal("备用电源", first.DisplayName);
        Assert.Equal(secondName, second.DisplayName);
        Assert.Throws<ArgumentException>(() => new RenameIncomingFeederCommand(
            station,
            first.IncomingFeederId,
            " "));
    }

    [Fact]
    public void ChangeSwitchStateCommand_ChangesFeedersIndependentlyAndSupportsUndoRedo()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = CreateDualStation();
        document.AddCustomerStation(station);
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        var command = new ChangeSwitchStateCommand(
            document,
            first.IsolationSwitch.Id,
            SwitchState.Closed);

        command.Execute();
        Assert.Equal(SwitchState.Closed, first.IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, second.IsolationSwitch.SwitchState);
        command.Undo();
        Assert.Equal(SwitchState.Open, first.IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, second.IsolationSwitch.SwitchState);
        command.Redo();
        Assert.Equal(SwitchState.Closed, first.IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, second.IsolationSwitch.SwitchState);
    }

    [Fact]
    public void ConnectivityGraph_ReusesGenericClosedSwitchConduction()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = CreateDualStation();
        document.AddCustomerStation(station);
        IncomingFeeder first = station.IncomingFeeders[0];
        IncomingFeeder second = station.IncomingFeeders[1];
        var builder = new ElectricalConnectivityGraphBuilder();

        ElectricalConnectivityGraph openGraph = builder.Build(document);
        Assert.DoesNotContain(openGraph.Edges, edge =>
            edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch);

        document.ChangeSwitchState(first.IsolationSwitch.Id, SwitchState.Closed);
        ElectricalConnectivityGraph closedGraph = builder.Build(document);
        Assert.Contains(closedGraph.Edges, edge =>
            edge.Type == ElectricalConnectivityEdgeType.ClosedSwitch &&
            edge.SourceId == first.IsolationSwitch.Id &&
            edge.Connects(first.CableTerminalId, first.StationTerminalId));
        Assert.DoesNotContain(closedGraph.Edges, edge =>
            edge.SourceId == second.IsolationSwitch.Id ||
            edge.Connects(first.StationTerminalId, second.StationTerminalId));
    }

    private static DrawingDocument CreateDocument() =>
        new(Guid.NewGuid(), "Customer station application tests");

    private static CustomerStation CreateDualStation() =>
        new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["进线 A", "进线 B"]);

    private static Guid[] CaptureStableIds(CustomerStation station)
    {
        return [
            station.Id,
            .. station.IncomingFeeders.SelectMany(feeder => new[]
            {
                feeder.IncomingFeederId,
                feeder.IsolationSwitch.Id,
                feeder.CableTerminalId,
                feeder.StationTerminalId,
                feeder.ElectricalNodeId
            })
        ];
    }

    private static void AssertPresent(
        DrawingDocument document,
        CustomerStation station)
    {
        Assert.Contains(station, document.CustomerStations);
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.Contains(feeder.IsolationSwitch, document.Devices);
            Assert.Contains(feeder.CableTerminal, document.Terminals);
            Assert.Contains(feeder.StationTerminal, document.Terminals);
            Assert.Contains(feeder.ElectricalNode, document.ElectricalNodes);
        });
    }

    private static void AssertAbsent(
        DrawingDocument document,
        CustomerStation station)
    {
        Assert.DoesNotContain(station, document.CustomerStations);
        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.DoesNotContain(feeder.IsolationSwitch, document.Devices);
            Assert.DoesNotContain(feeder.CableTerminal, document.Terminals);
            Assert.DoesNotContain(feeder.StationTerminal, document.Terminals);
            Assert.DoesNotContain(feeder.ElectricalNode, document.ElectricalNodes);
        });
    }
}
