using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class IntermediateTerminalRuntimeTests
{
    [Fact]
    public void Create_RegistersOwnerAndTerminalWithStableIds()
    {
        var document = CreateDocument();
        IntermediateTerminalCreationResult result =
            new IntermediateTerminalCreationFactory().Create("中间接续点");

        new CreateIntermediateTerminalCommand(document, result).Execute();

        IntermediateTerminal registered = Assert.Single(document.IntermediateTerminals);
        Terminal terminal = Assert.Single(document.Terminals);
        Assert.Same(result.IntermediateTerminal, registered);
        Assert.Same(result.Terminal, terminal);
        Assert.Equal(registered.TerminalId, terminal.Id);
        Assert.Equal(TopologyOwnerType.IntermediateTerminal, terminal.OwnerType);
        Assert.Equal(registered.Id, terminal.OwnerId);
        Assert.Null(terminal.ElectricalNodeId);
    }

    [Fact]
    public void Undo_RemovesOwnerAndTerminal()
    {
        var document = CreateDocument();
        IntermediateTerminalCreationResult result =
            new IntermediateTerminalCreationFactory().Create("中间接续点");
        var command = new CreateIntermediateTerminalCommand(document, result);
        command.Execute();

        command.Undo();

        Assert.Empty(document.IntermediateTerminals);
        Assert.Empty(document.Terminals);
        Assert.Null(document.FindIntermediateTerminal(result.IntermediateTerminal.Id));
    }

    [Fact]
    public void Redo_RestoresSameOwnerAndTerminalIds()
    {
        var document = CreateDocument();
        IntermediateTerminalCreationResult result =
            new IntermediateTerminalCreationFactory().Create("中间接续点");
        var command = new CreateIntermediateTerminalCommand(document, result);
        Guid ownerId = result.IntermediateTerminal.Id;
        Guid terminalId = result.Terminal.Id;
        command.Execute();
        command.Undo();

        command.Redo();

        Assert.Equal(ownerId, Assert.Single(document.IntermediateTerminals).Id);
        Assert.Equal(terminalId, Assert.Single(document.Terminals).Id);
        Assert.Same(result.IntermediateTerminal, document.FindIntermediateTerminal(ownerId));
        Assert.Same(result.Terminal, Assert.Single(document.Terminals));
    }

    [Fact]
    public void GraphQuery_UsesIntermediateTerminalChildAsNormalTerminal()
    {
        var document = CreateDocument();
        var factory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult start = factory.Create("起点");
        IntermediateTerminalCreationResult middle = factory.Create("中间点");
        IntermediateTerminalCreationResult end = factory.Create("终点");
        foreach (IntermediateTerminalCreationResult result in new[] { start, middle, end })
        {
            new CreateIntermediateTerminalCommand(document, result).Execute();
        }

        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            start.Terminal.Id,
            middle.Terminal.Id,
            "起点至中间点",
            "10kV"));
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            middle.Terminal.Id,
            end.Terminal.Id,
            "中间点至终点",
            "10kV"));

        var graph = new ElectricalConnectivityGraphBuilder().Build(document);
        var query = new ElectricalConnectivityQuery(graph);

        Assert.True(query.IsConnected(start.Terminal.Id, end.Terminal.Id));
    }

    [Fact]
    public void Remove_IsRejectedWhileTerminalIsReferencedByConnection()
    {
        var document = CreateDocument();
        var factory = new IntermediateTerminalCreationFactory();
        IntermediateTerminalCreationResult first = factory.Create("起点");
        IntermediateTerminalCreationResult second = factory.Create("终点");
        new CreateIntermediateTerminalCommand(document, first).Execute();
        new CreateIntermediateTerminalCommand(document, second).Execute();
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            first.Terminal.Id,
            second.Terminal.Id,
            "测试连接",
            "10kV"));

        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveIntermediateTerminal(first.IntermediateTerminal.Id));
        Assert.NotNull(document.FindIntermediateTerminal(first.IntermediateTerminal.Id));
    }

    private static DrawingDocument CreateDocument()
    {
        return new DrawingDocument(Guid.NewGuid(), "Intermediate terminal runtime test");
    }
}
