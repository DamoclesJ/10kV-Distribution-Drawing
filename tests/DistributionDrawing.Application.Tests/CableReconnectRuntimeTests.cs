using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class CableReconnectRuntimeTests
{
    [Fact]
    public void Execute_ReconnectsCableAndReplacesConnection()
    {
        (DrawingDocument document, CableSegment cable, Guid startId, Guid endId, Guid newEndId) =
            CreateCableScenario();
        Guid originalSegmentId = cable.Id;
        Guid originalConnectionId = cable.ConnectionId;
        var command = new ReconnectCableCommand(
            document,
            cable.Id,
            startId,
            newEndId);

        command.Execute();

        CableReconnectResult result = Assert.IsType<CableReconnectResult>(command.Result);
        Assert.Equal(originalSegmentId, Assert.Single(document.CableSegments).Id);
        Assert.Equal(originalSegmentId, result.AfterCableSegment.Id);
        Assert.NotEqual(originalConnectionId, result.AfterConnection.Id);
        Assert.DoesNotContain(document.Connections, connection =>
            connection.Id == originalConnectionId);
        Assert.Contains(document.Connections, connection =>
            connection.Id == result.AfterConnection.Id &&
            connection.StartTerminalId == startId &&
            connection.EndTerminalId == newEndId);
        Assert.Equal(result.AfterConnection.Id, document.CableSegments.Single().ConnectionId);
        Assert.Equal(endId, result.BeforeConnection.EndTerminalId);
    }

    [Fact]
    public void UndoAndRedo_PreserveSegmentIdAndReuseAfterConnectionId()
    {
        (DrawingDocument document, CableSegment cable, Guid startId, _, Guid newEndId) =
            CreateCableScenario();
        Guid segmentId = cable.Id;
        var command = new ReconnectCableCommand(document, cable.Id, startId, newEndId);
        command.Execute();
        CableReconnectResult result = Assert.IsType<CableReconnectResult>(command.Result);
        Guid afterConnectionId = result.AfterConnection.Id;

        command.Undo();

        Assert.Equal(segmentId, Assert.Single(document.CableSegments).Id);
        Assert.Equal(result.BeforeConnection.Id, Assert.Single(document.Connections).Id);
        Assert.Equal(result.BeforeConnection.Id, document.CableSegments.Single().ConnectionId);

        command.Redo();

        Assert.Equal(segmentId, Assert.Single(document.CableSegments).Id);
        Assert.Equal(afterConnectionId, Assert.Single(document.Connections).Id);
        Assert.Equal(afterConnectionId, document.CableSegments.Single().ConnectionId);
    }

    [Fact]
    public void GraphQuery_ReflectsReconnectTopology()
    {
        (DrawingDocument document, CableSegment cable, Guid startId, Guid endId, Guid newEndId) =
            CreateCableScenario();
        Assert.True(CreateQuery(document).IsConnected(startId, endId));

        var command = new ReconnectCableCommand(document, cable.Id, startId, newEndId);
        command.Execute();

        ElectricalConnectivityQuery query = CreateQuery(document);
        Assert.True(query.IsConnected(startId, newEndId));
        Assert.False(query.IsConnected(startId, endId));
    }

    [Fact]
    public void InvalidReconnect_DoesNotChangeDocument()
    {
        (DrawingDocument document, CableSegment cable, Guid startId, Guid endId, _) =
            CreateCableScenario();
        int terminalCount = document.Terminals.Count;
        int connectionCount = document.Connections.Count;
        int segmentCount = document.CableSegments.Count;
        Guid connectionId = cable.ConnectionId;

        Assert.Throws<InvalidOperationException>(() =>
            new ReconnectCableCommand(document, Guid.NewGuid(), startId, endId).Execute());
        Assert.Throws<InvalidOperationException>(() =>
            new ReconnectCableCommand(document, cable.Id, startId, Guid.NewGuid()).Execute());
        Assert.Throws<ArgumentException>(() =>
            new ReconnectCableCommand(document, cable.Id, startId, startId));

        Assert.Equal(terminalCount, document.Terminals.Count);
        Assert.Equal(connectionCount, document.Connections.Count);
        Assert.Equal(segmentCount, document.CableSegments.Count);
        Assert.Equal(connectionId, document.CableSegments.Single().ConnectionId);
    }

    private static ElectricalConnectivityQuery CreateQuery(DrawingDocument document)
    {
        return new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));
    }

    private static (DrawingDocument Document, CableSegment Cable, Guid StartId, Guid EndId, Guid NewEndId)
        CreateCableScenario()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable reconnect runtime test");
        var terminalFactory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult start = terminalFactory.Create("起点");
        IntermediateTerminalCreationResult end = terminalFactory.Create("原终点");
        IntermediateTerminalCreationResult newEnd = terminalFactory.Create("新终点");
        foreach (IntermediateTerminalCreationResult result in new[] { start, end, newEnd })
        {
            new CreateIntermediateTerminalCommand(document, result).Execute();
        }

        CableSegmentCreationResult cable = new CableSegmentCreationFactory().Create(
            document,
            start.Terminal.Id,
            end.Terminal.Id,
            "工作票电缆",
            "10kV-Cable",
            25);
        new CreateCableSegmentCommand(document, cable).Execute();
        return (document, cable.CableSegment, start.Terminal.Id, end.Terminal.Id, newEnd.Terminal.Id);
    }
}
