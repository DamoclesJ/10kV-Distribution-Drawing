using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkTicketRangeCommandTests
{
    [Fact]
    public void RangeChangeStalesDraftAndUndoRedoRestoresExpectedCompletion()
    {
        (DrawingDocument drawing, _, SwitchDevice[] switches) = Cabinet();
        IsolationBoundary[] boundaries = switches.Select(device =>
        {
            Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Line,
                out IsolationBoundary? boundary, out string issue), issue);
            return boundary!;
        }).ToArray();
        var tickets = new WorkTicketDataRoot(drawing.Id);
        var stack = new CommandStack();
        WorkTask task = new("更换并验收测试", "一号环网柜");
        WorkTicketSession analyzed = new WorkTicketAnalyzer().Analyze(drawing,
            WorkTicketSession.Create() with
            {
                Task = task,
                IsolationBoundaries = [boundaries[0], boundaries[1]]
            });
        SectionDraft[] completedSections = analyzed.Draft!.Sections.Select(section =>
            section.Code == "6.1" ? section with { Completion = SectionCompletion.Completed } : section).ToArray();
        WorkTicketSession before = analyzed with { Draft = new WorkTicketDraft(completedSections) };
        tickets.Add(before);

        WorkTicketSession stale = WorkTicketRangeCommit.Apply(drawing, tickets, stack, before.Id,
            [boundaries[1]], task);
        Assert.Equal(SectionCompletion.Stale, stale.Draft!.Section("6.1").Completion);
        Assert.True(stack.Undo());
        Assert.Same(before, tickets.Selected(before.Id));
        Assert.Equal(SectionCompletion.Completed, tickets.Selected(before.Id)!.Draft!.Section("6.1").Completion);
        Assert.True(stack.Redo());
        Assert.Same(stale, tickets.Selected(before.Id));
        Assert.Equal(SectionCompletion.Stale, tickets.Selected(before.Id)!.Draft!.Section("6.1").Completion);
    }

    [Fact]
    public void ConfirmRangeCreatesAndUpdatesTheSameTicketWithUndoRedo()
    {
        (DrawingDocument drawing, _, SwitchDevice[] switches) = Cabinet();
        IsolationBoundary[] boundaries = switches.Take(2).Select(device =>
        {
            Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Line,
                out IsolationBoundary? boundary, out string issue), issue);
            return boundary!;
        }).ToArray();
        var tickets = new WorkTicketDataRoot(drawing.Id);
        var stack = new CommandStack();
        WorkTask task = new("更换并验收测试", "一号环网柜");

        var pendingSlots = new TicketBoundarySlotCollection();
        pendingSlots.Load([], _ => null);
        pendingSlots.Add();
        pendingSlots.Discard();
        Assert.Empty(tickets.Tickets);
        Assert.False(stack.CanUndo);
        Assert.Throws<InvalidOperationException>(() => WorkTicketRangeCommit.Apply(drawing,
            tickets, stack, null,
            [boundaries[0] with { TerminalId = switches[0].FirstTerminalId }], task));
        Assert.Empty(tickets.Tickets);
        Assert.False(stack.CanUndo);
        WorkTicketSession created = WorkTicketRangeCommit.Apply(drawing, tickets, stack, null,
            boundaries.Cast<IsolationBoundary?>().ToArray(), task);

        Assert.Same(created, tickets.Selected(created.Id));
        Assert.Equal(task, created.Task);
        Assert.Equal(2, created.IsolationBoundaries.Count);
        Assert.Equal(boundaries.Select(item => item.DeviceId),
            created.IsolationBoundaries.Select(item => item.DeviceId));
        Assert.Equal(boundaries.Select(item => item.Side),
            created.IsolationBoundaries.Select(item => item.Side));
        Assert.Equal(boundaries.Select(item => item.TerminalId),
            created.IsolationBoundaries.Select(item => item.TerminalId));
        Assert.Empty(created.EquipmentScopeIds);
        Assert.Single(stack.History);

        WorkTicketSession updated = WorkTicketRangeCommit.Apply(drawing, tickets, stack, created.Id,
            [boundaries[1]], task with { Content = "仅更换" });
        Assert.Same(updated, tickets.Selected(created.Id));
        Assert.Equal(created.Id, updated.Id);
        Assert.Single(updated.IsolationBoundaries);
        Assert.Equal("仅更换", updated.Task.Content);
        Assert.Equal(2, stack.History.Count);

        Assert.True(stack.Undo());
        Assert.Same(created, tickets.Selected(created.Id));
        Assert.True(stack.Undo());
        Assert.Empty(tickets.Tickets);
        Assert.True(stack.Redo());
        Assert.Same(created, tickets.Selected(created.Id));
        Assert.True(stack.Redo());
        Assert.Same(updated, tickets.Selected(created.Id));
    }

    private static (DrawingDocument, RingCabinet, SwitchDevice[]) Cabinet()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "范围面板提交");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            Enumerable.Range(1, 2).Select(index => RingCabinetIntervalDefinition.CreateLoadSwitch(
                index, SwitchState.Closed, SwitchState.Open)).ToArray()));
        drawing.AddDevice(cabinet);
        SwitchDevice[] switches = cabinet.Intervals.Select(interval =>
            interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch)).ToArray();
        return (drawing, cabinet, switches);
    }
}
