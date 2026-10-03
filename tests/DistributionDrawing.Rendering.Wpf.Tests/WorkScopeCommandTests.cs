using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class WorkScopeCommandTests
{
    [Fact]
    public void AddChangeRemoveUndoRedoReplayCompleteCapturedSnapshots()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "commands");
        RingCabinet cabinet = Cabinet();
        document.AddDevice(cabinet);
        Guid scopeId = Guid.NewGuid();
        Guid first = cabinet.Intervals[0].CableTerminalId!.Value, second = cabinet.Intervals[1].CableTerminalId!.Value;
        Guid[] callerMembership = [first];
        WorkScopeRegion[] regions = [new(callerMembership, [cabinet.MainBusNodeId]), new([second], [])];
        WorkScopeBoundary[] boundaries = [new(cabinet.Id, BoundarySide.Line, first), new(cabinet.Id, BoundarySide.Bus)];
        var before = new WorkScopeCommandSnapshot(scopeId, regions, boundaries, null);
        callerMembership[0] = Guid.NewGuid();
        regions[0] = new([Guid.NewGuid()], []);
        boundaries[0] = new(Guid.NewGuid(), BoundarySide.Load);
        var stack = new CommandStack();
        stack.ExecuteCommand(new AddWorkScopeCommand(document, before));
        AssertSnapshot(before, document);
        Assert.True(stack.Undo()); Assert.Empty(document.WorkScopes);
        Assert.True(stack.Redo()); AssertSnapshot(before, document);
        var after = new WorkScopeCommandSnapshot(scopeId, [new([], [cabinet.MainBusNodeId])], [], "new");
        stack.ExecuteCommand(new ChangeWorkScopeCommand(document, before, after));
        AssertSnapshot(after, document);
        Assert.True(stack.Undo()); AssertSnapshot(before, document);
        // Redo restores the snapshot even if electrical state has since changed.
        document.ChangeSwitchState(cabinet.Intervals[0].SwitchDevices[0].Id, SwitchState.Closed);
        Assert.True(stack.Redo()); AssertSnapshot(after, document);
        stack.ExecuteCommand(new RemoveWorkScopeCommand(document, after));
        Assert.Empty(document.WorkScopes);
        Assert.True(stack.Undo()); AssertSnapshot(after, document);
        Assert.True(stack.Redo()); Assert.Empty(document.WorkScopes);
        Assert.Null(typeof(WorkScopeCommandSnapshot).GetProperty("GroundingPointIds"));
    }

    [Fact]
    public void FailedAddAndChangeDoNotMutateDocumentOrHistory()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "commands");
        RingCabinet cabinet = Cabinet(); document.AddDevice(cabinet);
        var stack = new CommandStack();
        var invalid = new WorkScopeCommandSnapshot(Guid.NewGuid(), [new([Guid.NewGuid()], [])], []);
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(new AddWorkScopeCommand(document, invalid)));
        Assert.Empty(document.WorkScopes); Assert.Empty(stack.History);
        var valid = new WorkScopeCommandSnapshot(invalid.WorkScopeId, [new([], [cabinet.MainBusNodeId])], [], null);
        stack.ExecuteCommand(new AddWorkScopeCommand(document, valid));
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(new ChangeWorkScopeCommand(document, valid, invalid)));
        Assert.Single(stack.History); Assert.Equal(1, stack.CurrentIndex); AssertSnapshot(valid, document);
    }

    private static void AssertSnapshot(WorkScopeCommandSnapshot expected, DrawingDocument document)
    {
        WorkScope actual = Assert.Single(document.WorkScopes);
        Assert.Equal(expected.WorkScopeId, actual.WorkScopeId);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Boundaries, actual.Boundaries);
        Assert.Equal(expected.Regions.Count, actual.Regions.Count);
        for (int i = 0; i < expected.Regions.Count; i++)
        {
            Assert.Equal(expected.Regions[i].TerminalIds, actual.Regions[i].TerminalIds);
            Assert.Equal(expected.Regions[i].ElectricalNodeIds, actual.Regions[i].ElectricalNodeIds);
        }
    }

    private static RingCabinet Cabinet() => RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "cabinet",
        [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
         RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
}
