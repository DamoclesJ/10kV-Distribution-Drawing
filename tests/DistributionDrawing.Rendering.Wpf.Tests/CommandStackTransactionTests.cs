using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CommandStackTransactionTests
{
    [Fact]
    public void ExecuteValidationFailure_UndoesCommandAndPreservesHistoryState()
    {
        var value = new TransactionState();
        var stack = new CommandStack();
        stack.MarkSaved();

        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(
            new ValueCommand(value, 0, 1),
            () => throw new InvalidOperationException("validation")));

        Assert.Equal(0, value.LayoutValue);
        Assert.Equal(0, value.SceneValue);
        Assert.Empty(stack.History);
        Assert.Equal(0, stack.CurrentIndex);
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(0, stack.CurrentStateId);
        Assert.False(stack.IsDirty);
    }

    [Fact]
    public void UndoValidationFailure_RedoesCommandAndPreservesCursorAndStateId()
    {
        var value = new TransactionState();
        var stack = new CommandStack();
        stack.ExecuteCommand(new ValueCommand(value, 0, 1));
        value.SceneValue = 1;
        int beforeIndex = stack.CurrentIndex;
        long beforeStateId = stack.CurrentStateId;
        long beforeSavedStateId = stack.SavedStateId;
        bool beforeDirty = stack.IsDirty;

        Assert.Throws<InvalidOperationException>(() => stack.Undo(
            () =>
            {
                value.SceneValue = 0;
                throw new InvalidOperationException("validation");
            },
            () => value.SceneValue = 1));

        Assert.Equal(1, value.LayoutValue);
        Assert.Equal(1, value.SceneValue);
        Assert.Equal(beforeIndex, stack.CurrentIndex);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(beforeStateId, stack.CurrentStateId);
        Assert.Equal(beforeSavedStateId, stack.SavedStateId);
        Assert.Equal(beforeDirty, stack.IsDirty);

        Assert.True(stack.Undo(() => value.SceneValue = value.LayoutValue));
        stack.ExecuteCommand(
            new ValueCommand(value, 0, 2),
            () => value.SceneValue = value.LayoutValue);
        Assert.Equal(2, value.LayoutValue);
        Assert.Equal(2, value.SceneValue);
    }

    [Fact]
    public void RedoValidationFailure_UndoesCommandAndPreservesCursorAndStateId()
    {
        var value = new TransactionState();
        var stack = new CommandStack();
        stack.ExecuteCommand(new ValueCommand(value, 0, 1));
        Assert.True(stack.Undo(() => value.SceneValue = value.LayoutValue));
        int beforeIndex = stack.CurrentIndex;
        long beforeStateId = stack.CurrentStateId;
        long beforeSavedStateId = stack.SavedStateId;
        bool beforeDirty = stack.IsDirty;

        Assert.Throws<InvalidOperationException>(() => stack.Redo(
            () =>
            {
                value.SceneValue = 1;
                throw new InvalidOperationException("validation");
            },
            () => value.SceneValue = 0));

        Assert.Equal(0, value.LayoutValue);
        Assert.Equal(0, value.SceneValue);
        Assert.Equal(beforeIndex, stack.CurrentIndex);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
        Assert.Equal(beforeStateId, stack.CurrentStateId);
        Assert.Equal(beforeSavedStateId, stack.SavedStateId);
        Assert.Equal(beforeDirty, stack.IsDirty);

        Assert.True(stack.Redo(() => value.SceneValue = value.LayoutValue));
        stack.ExecuteCommand(
            new ValueCommand(value, 1, 2),
            () => value.SceneValue = value.LayoutValue);
        Assert.Equal(2, value.LayoutValue);
        Assert.Equal(2, value.SceneValue);
    }

    [Fact]
    public void TransactionalExecute_PartialApplicationFailureRestoresSnapshotAndRemainsUsable()
    {
        var value = new TransactionState();
        var stack = new CommandStack();
        stack.MarkSaved();
        int stateChanges = 0;
        int dirtyChanges = 0;
        CommandTransactionFailureStage? failureStage = null;
        stack.StateChanged += (_, _) => stateChanges++;
        stack.DirtyChanged += (_, _) => dirtyChanges++;

        Assert.Throws<TestCommandApplicationException>(() => stack.ExecuteCommand(
            new PartiallyFailingCommand(value),
            () => value.SceneValue = value.LayoutValue,
            stage =>
            {
                failureStage = stage;
                value.LayoutValue = 0;
                value.SceneValue = 0;
            }));

        Assert.Equal(0, value.LayoutValue);
        Assert.Equal(0, value.SceneValue);
        Assert.Empty(stack.History);
        Assert.Equal(0, stack.CurrentIndex);
        Assert.Equal(0, stack.CurrentStateId);
        Assert.Equal(0, stack.SavedStateId);
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.False(stack.IsDirty);
        Assert.Equal(0, stateChanges);
        Assert.Equal(0, dirtyChanges);
        Assert.Equal(CommandTransactionFailureStage.Application, failureStage);

        stack.ExecuteCommand(
            new ValueCommand(value, 0, 2),
            () => value.SceneValue = value.LayoutValue,
            _ =>
            {
                value.LayoutValue = 0;
                value.SceneValue = 0;
            });
        Assert.True(stack.Undo(() => value.SceneValue = value.LayoutValue));
        Assert.True(stack.Redo(() => value.SceneValue = value.LayoutValue));
        Assert.Equal(2, value.LayoutValue);
        Assert.Equal(2, value.SceneValue);
    }

    [Fact]
    public void ValidatedUndoRedo_PreserveExistingSuccessfulBehavior()
    {
        var value = new TransactionState();
        var stack = new CommandStack();
        stack.ExecuteCommand(new ValueCommand(value, 0, 1), () =>
        {
            Assert.Equal(1, value.LayoutValue);
            value.SceneValue = value.LayoutValue;
        });
        Assert.True(stack.Undo(() =>
        {
            Assert.Equal(0, value.LayoutValue);
            value.SceneValue = value.LayoutValue;
        }));
        Assert.True(stack.Redo(() =>
        {
            Assert.Equal(1, value.LayoutValue);
            value.SceneValue = value.LayoutValue;
        }));

        Assert.Equal(1, stack.CurrentIndex);
        Assert.Equal(value.LayoutValue, value.SceneValue);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    private sealed class TransactionState
    {
        public int LayoutValue { get; set; }

        public int SceneValue { get; set; }
    }

    private sealed class ValueCommand(TransactionState value, int before, int after) : ICommand
    {
        public void Execute() => value.LayoutValue = after;

        public void Undo() => value.LayoutValue = before;

        public void Redo() => Execute();
    }

    private sealed class PartiallyFailingCommand(TransactionState value) : ICommand
    {
        public void Execute()
        {
            value.LayoutValue = 1;
            throw new TestCommandApplicationException();
        }

        public void Undo() => value.LayoutValue = 0;

        public void Redo() => Execute();
    }

    private sealed class TestCommandApplicationException : Exception;
}
