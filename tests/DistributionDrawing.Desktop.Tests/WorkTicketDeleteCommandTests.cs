using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
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
    public void CompositeRestorePreflightRejectsBeforeFirstInverseMutation()
    {
        var first = new CounterCommand();
        var second = new CounterCommand();
        var stack = new CommandStack();
        var command = new CompositeDeleteCommand(
            [first, second],
            beforeUndo: () => throw new InvalidOperationException("GS rejected"));
        stack.ExecuteCommand(command);
        ICommand lastApplied = stack.LastAppliedCommand!;

        Assert.Throws<InvalidOperationException>(() => stack.Undo());

        Assert.Equal(1, first.Value);
        Assert.Equal(1, second.Value);
        Assert.Equal(1, stack.CurrentIndex);
        Assert.True(stack.IsDirty);
        Assert.Same(lastApplied, stack.LastAppliedCommand);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmedScopeReferencedByTicketCannotBeDeleted(bool electricalRangeAdapter)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "scope refs");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "c",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        document.AddDevice(cabinet);
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(), [new([], [cabinet.MainBusNodeId])], [], null);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            WorkScopeIds = electricalRangeAdapter ? [] : [scope.WorkScopeId],
            WorkScopeItems = electricalRangeAdapter ? [new(WorkScopeItemKind.ElectricalRange, scope.WorkScopeId)] : []
        };
        var root = new WorkTicketDataRoot(document.Id, [ticket]);
        var stack = new CommandStack();
        var delete = new RemoveWorkScopeCommand(document, WorkScopeCommandSnapshot.From(scope));
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(new WorkTicketGuardedDeleteCommand(delete, document, root)));
        Assert.Empty(stack.History);
        WorkScope restored = Assert.Single(document.WorkScopes);
        Assert.Equal(scope.WorkScopeId, restored.WorkScopeId);
        Assert.Null(restored.Description);
        Assert.Equal(scope.Regions[0].ElectricalNodeIds, restored.Regions[0].ElectricalNodeIds);
        WorkTicketReferenceGuard.Validate(document, root);
    }

    private sealed class CounterCommand : ICommand
    {
        public int Value { get; private set; }
        public void Execute() => Value++;
        public void Undo() => Value--;
        public void Redo() => Execute();
    }
}
