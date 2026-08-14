using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class CableSplitRuntimeTests
{
    [Fact]
    public void Execute_ReplacesCableWithIntermediateTerminalAndTwoSegments()
    {
        (DrawingDocument document, CableSegment original) = CreateCableScenario();
        var command = new SplitCableCommand(document, original.Id, "中间接头点");

        command.Execute();

        CableSplitResult result = Assert.IsType<CableSplitResult>(command.Result);
        Assert.DoesNotContain(document.CableSegments, segment => segment.Id == original.Id);
        Assert.Equal(2, document.CableSegments.Count);
        Assert.Equal(2, document.Connections.Count);
        Assert.Contains(document.IntermediateTerminals, intermediateTerminal =>
            intermediateTerminal.Id == result.IntermediateTerminal.IntermediateTerminal.Id);
        Assert.Equal(
            result.IntermediateTerminal.Terminal.Id,
            result.IntermediateTerminal.IntermediateTerminal.TerminalId);
        Assert.Contains(document.CableSegments, segment =>
            segment.Id == result.FirstCableSegment.Id &&
            segment.EndTerminalId == result.IntermediateTerminal.Terminal.Id);
        Assert.Contains(document.CableSegments, segment =>
            segment.Id == result.SecondCableSegment.Id &&
            segment.StartTerminalId == result.IntermediateTerminal.Terminal.Id);
    }

    [Fact]
    public void UndoAndRedo_RestoreTheSameSplitStableIds()
    {
        (DrawingDocument document, CableSegment original) = CreateCableScenario();
        var command = new SplitCableCommand(document, original.Id, "中间接头点");
        command.Execute();
        CableSplitResult result = Assert.IsType<CableSplitResult>(command.Result);
        Guid intermediateId = result.IntermediateTerminal.IntermediateTerminal.Id;
        Guid terminalId = result.IntermediateTerminal.Terminal.Id;
        Guid firstSegmentId = result.FirstCableSegment.Id;
        Guid secondSegmentId = result.SecondCableSegment.Id;
        Guid firstConnectionId = result.FirstConnection.Id;
        Guid secondConnectionId = result.SecondConnection.Id;

        command.Undo();

        Assert.Single(document.CableSegments, original);
        Assert.Single(document.Connections, result.OriginalConnection);
        Assert.Equal(2, document.IntermediateTerminals.Count);

        command.Redo();

        Assert.Contains(document.IntermediateTerminals, intermediateTerminal =>
            intermediateTerminal.Id == intermediateId);
        Assert.Contains(document.Terminals, terminal => terminal.Id == terminalId);
        Assert.Contains(document.CableSegments, segment => segment.Id == firstSegmentId);
        Assert.Contains(document.CableSegments, segment => segment.Id == secondSegmentId);
        Assert.Contains(document.Connections, connection => connection.Id == firstConnectionId);
        Assert.Contains(document.Connections, connection => connection.Id == secondConnectionId);
        Assert.DoesNotContain(document.CableSegments, segment => segment.Id == original.Id);
    }

    [Fact]
    public void GraphQuery_RemainsConnectedThroughSplit()
    {
        (DrawingDocument document, CableSegment original) = CreateCableScenario();
        ElectricalConnectivityQuery queryBefore = CreateQuery(document);
        Assert.True(queryBefore.IsConnected(original.StartTerminalId, original.EndTerminalId));

        var command = new SplitCableCommand(document, original.Id, "中间接头点");
        command.Execute();
        CableSplitResult result = Assert.IsType<CableSplitResult>(command.Result);
        ElectricalConnectivityQuery queryAfter = CreateQuery(document);

        Assert.True(queryAfter.IsConnected(
            original.StartTerminalId,
            result.IntermediateTerminal.Terminal.Id));
        Assert.True(queryAfter.IsConnected(original.StartTerminalId, original.EndTerminalId));

        command.Undo();
        ElectricalConnectivityQuery queryAfterUndo = CreateQuery(document);
        Assert.True(queryAfterUndo.IsConnected(original.StartTerminalId, original.EndTerminalId));
    }

    [Fact]
    public void InvalidSplit_DoesNotChangeDocument()
    {
        (DrawingDocument document, CableSegment original) = CreateCableScenario();
        int originalTerminalCount = document.Terminals.Count;
        int originalConnectionCount = document.Connections.Count;
        int originalSegmentCount = document.CableSegments.Count;

        Assert.Throws<InvalidOperationException>(() =>
            new SplitCableCommand(document, Guid.NewGuid(), "中间接头点").Execute());

        Assert.Equal(originalTerminalCount, document.Terminals.Count);
        Assert.Equal(originalConnectionCount, document.Connections.Count);
        Assert.Equal(originalSegmentCount, document.CableSegments.Count);
        Assert.Contains(document.CableSegments, segment => segment.Id == original.Id);
    }

    private static ElectricalConnectivityQuery CreateQuery(DrawingDocument document)
    {
        return new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));
    }

    private static (DrawingDocument Document, CableSegment CableSegment)
        CreateCableScenario()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable split runtime test");
        var terminalFactory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult start = terminalFactory.Create("起点");
        IntermediateTerminalCreationResult end = terminalFactory.Create("终点");
        new CreateIntermediateTerminalCommand(document, start).Execute();
        new CreateIntermediateTerminalCommand(document, end).Execute();

        CableSegmentCreationResult cable = new CableSegmentCreationFactory().Create(
            document,
            start.Terminal.Id,
            end.Terminal.Id,
            "原电缆",
            "10kV-Cable",
            25);
        new CreateCableSegmentCommand(document, cable).Execute();
        return (document, cable.CableSegment);
    }
}
