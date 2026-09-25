using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkTicketDeleteCommandTests
{
    [Fact]
    public void ReferencedDeletionRollsBackAndDoesNotEnterCommandHistory()
    {
        var first = new CounterCommand();
        var second = new CounterCommand();
        var stack = new CommandStack();
        var command = new CompositeDeleteCommand([first, second], () =>
            throw new InvalidOperationException("该对象正在被工作票引用"));

        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(command));
        Assert.Equal(0, first.Value);
        Assert.Equal(0, second.Value);
        Assert.Empty(stack.History);
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void UnreferencedDeletionSupportsUndoRedo()
    {
        var counter = new CounterCommand();
        var stack = new CommandStack();
        stack.ExecuteCommand(new CompositeDeleteCommand([counter], () => { }));
        Assert.Equal(1, counter.Value);
        stack.Undo();
        Assert.Equal(0, counter.Value);
        stack.Redo();
        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public void DirectDeletePathRollsBackWhenTicketReferenceBreaks()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "删除保护");
        var tickets = new WorkTicketDataRoot(drawing.Id,
            [WorkTicketSession.Create() with { WorkScopeIds = [Guid.NewGuid()] }]);
        var counter = new CounterCommand();
        var stack = new CommandStack();
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(
            new WorkTicketGuardedDeleteCommand(counter, drawing, tickets)));
        Assert.Equal(0, counter.Value);
        Assert.Empty(stack.History);
    }

    private sealed class CounterCommand : ICommand
    {
        public int Value { get; private set; }
        public void Execute() => Value++;
        public void Undo() => Value--;
        public void Redo() => Execute();
    }
}
