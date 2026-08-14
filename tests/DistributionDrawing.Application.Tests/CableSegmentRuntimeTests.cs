using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class CableSegmentRuntimeTests
{
    [Fact]
    public void CreateCableSegment_RegistersSegmentAndConnection()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        var command = new CreateCableSegmentCommand(document, result);

        command.Execute();

        Assert.Same(result.CableSegment, Assert.Single(document.CableSegments));
        Assert.Same(result.Connection, Assert.Single(document.Connections));
        Assert.Equal(result.Connection.Id, result.CableSegment.ConnectionId);
        Assert.Equal(ConnectionType.Cable, result.Connection.Type);
        Assert.Equal(startTerminalId, result.CableSegment.StartTerminalId);
        Assert.Equal(endTerminalId, result.CableSegment.EndTerminalId);
    }

    [Fact]
    public void Undo_RemovesCableSegmentAndConnection()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        var command = new CreateCableSegmentCommand(document, result);
        command.Execute();

        command.Undo();

        Assert.Empty(document.CableSegments);
        Assert.Empty(document.Connections);
    }

    [Fact]
    public void Redo_RestoresSameObjectsAndStableIds()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        Guid segmentId = result.CableSegment.Id;
        Guid connectionId = result.Connection.Id;
        var command = new CreateCableSegmentCommand(document, result);
        command.Execute();
        command.Undo();
        command.Redo();

        Assert.Equal(segmentId, Assert.Single(document.CableSegments).Id);
        Assert.Equal(connectionId, Assert.Single(document.Connections).Id);
        Assert.Same(result.CableSegment, Assert.Single(document.CableSegments));
        Assert.Same(result.Connection, Assert.Single(document.Connections));
    }

    [Fact]
    public void GraphAndQuery_SeeCableConnection()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        new CreateCableSegmentCommand(document, result).Execute();

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.Contains(graph.Edges, edge =>
            edge.Type == ElectricalConnectivityEdgeType.Connection &&
            edge.SourceId == result.Connection.Id);
        Assert.True(query.IsConnected(startTerminalId, endTerminalId));
    }

    [Fact]
    public void Undo_RemovesCablePathFromGraphQuery()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        var command = new CreateCableSegmentCommand(document, result);
        command.Execute();
        command.Undo();

        ElectricalConnectivityGraph graph = new ElectricalConnectivityGraphBuilder()
            .Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.False(query.IsConnected(startTerminalId, endTerminalId));
    }

    [Fact]
    public void DomainRejectsDirectConnectionRemovalWhileSegmentExists()
    {
        (DrawingDocument document, Guid startTerminalId, Guid endTerminalId) =
            CreateCableScenario();
        CableSegmentCreationResult result = CreateResult(
            document,
            startTerminalId,
            endTerminalId);
        new CreateCableSegmentCommand(document, result).Execute();

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveConnection(result.Connection.Id));
    }

    private static CableSegmentCreationResult CreateResult(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId)
    {
        return new CableSegmentCreationFactory().Create(
            document,
            startTerminalId,
            endTerminalId,
            "柜间电缆",
            "10kV-Cable",
            25);
    }

    private static (DrawingDocument Document, Guid StartTerminalId, Guid EndTerminalId)
        CreateCableScenario()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable segment runtime test");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    1,
                    SwitchState.Open,
                    SwitchState.Open,
                    "负1间隔"),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    2,
                    SwitchState.Open,
                    SwitchState.Open,
                    "负2间隔"),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    3,
                    SwitchState.Open,
                    SwitchState.Open,
                    "负3间隔")
            ]));
        document.AddDevice(cabinet);

        PoleCreationResult poleResult = new PoleCreationFactory().CreateWithAttachments(
            "P-020",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        new CreatePoleCommand(document, poleResult).Execute();

        Guid startTerminalId = cabinet.Intervals[0].ExternalTerminalId;
        CableTermination cableTermination = Assert.IsType<CableTermination>(
            Assert.Single(poleResult.Devices));
        return (document, startTerminalId, cableTermination.CableSideTerminalId);
    }
}
