using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.TestSupport;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkScopeCorrectionCommandTests
{
    [Fact]
    public void Apply_PreservesIndependentFactsAndLegacyRangeItemsInOneSnapshot()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring();
        RingCabinet cabinet = fixture.Drawing.Devices.OfType<RingCabinet>().Single();
        Guid terminalId = cabinet.Intervals[0].CableTerminalId!.Value;
        WorkScope legacy = fixture.Drawing.CreateWorkScope(Guid.NewGuid(), [new([terminalId], [])], []);
        GroundingPoint ground = fixture.Drawing.CreateGroundingPoint(Guid.NewGuid(), terminalId, "工作地点", "G1");
        WorkTicketSession before = WorkTicketSession.Create() with
        {
            UserFacts = [new("note", "保留现场事实", [], true)],
            GroundingPointIds = [ground.GroundingPointId],
            WorkScopeItems = [new(WorkScopeItemKind.ElectricalRange, legacy.WorkScopeId),
                new(WorkScopeItemKind.Equipment, fixture.Scenario.Seeds[0].BoundaryDeviceId)]
        };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [before]);
        var analyzer = new CountingCorrectionAnalyzer();
        WorkScopeHandoffPlanningResult result = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(),
            new WorkScopeIsolationBoundaryProjector(), analyzer).PrepareFromAnalysis(
                fixture.Drawing, fixture.State, fixture.Scenario, tickets, before.Id, new("检修", "新地点"));
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        var stack = new CommandStack();
        stack.ExecuteCommand(new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan));
        Assert.Single(stack.History);
        Assert.Equal(before.UserFacts, plan.AfterTicket.UserFacts);
        Assert.Equal(before.GroundingPointIds, plan.AfterTicket.GroundingPointIds);
        Assert.Equal(before.WorkScopeItems, plan.AfterTicket.WorkScopeItems);
        Assert.Equal(new WorkTask("检修", "新地点"), plan.AfterTicket.Task);
        Assert.True(stack.Undo());
        Assert.Same(before, tickets.Selected(before.Id));
        Assert.Same(legacy, Assert.Single(fixture.Drawing.WorkScopes));
        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(before.Id));
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public void ApplyAndUndoRedo_UseOneHistoryEntryAndReplaySameIdsWithoutEaOrAnalyzer()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring();
        var analyzer = new CountingCorrectionAnalyzer();
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(),
            new WorkScopeIsolationBoundaryProjector(), analyzer);
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        WorkScopeHandoffPlanningResult prepared = planner.PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, tickets);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        var stack = new CommandStack();
        EnergizationResult ea = fixture.State.CurrentResult!;
        int eaChanges = 0;
        fixture.State.Changed += (_, _) => eaChanges++;
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        Assert.False(command.AffectsEnergization);

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        Assert.Equal(1, stack.CurrentIndex);
        Assert.Same(plan.AfterTicket, Assert.Single(tickets.Tickets));
        Assert.Equal([plan.AfterWorkScope.WorkScopeId], plan.AfterTicket.WorkScopeIds);
        Assert.Equal(2, plan.AfterTicket.IsolationBoundaries.Count);
        for (int replay = 0; replay < 2; replay++)
        {
            Assert.True(stack.Undo());
            Assert.Empty(tickets.Tickets);
            Assert.Empty(fixture.Drawing.WorkScopes);
            Assert.False(stack.IsDirty);
            Assert.True(stack.Redo());
            Assert.Same(plan.AfterTicket, tickets.Selected(plan.TicketId));
            WorkScope scope = Assert.Single(fixture.Drawing.WorkScopes);
            Assert.Equal(plan.AfterWorkScope.WorkScopeId, scope.WorkScopeId);
            Assert.Equal(plan.AfterWorkScope.Boundaries, scope.Boundaries);
            Assert.Equal(plan.AfterWorkScope.Regions.SelectMany(region => region.TerminalIds),
                scope.Regions.SelectMany(region => region.TerminalIds));
            Assert.Single(stack.History);
        }
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(0, eaChanges);
        Assert.Same(ea, fixture.State.CurrentResult);
    }
}
