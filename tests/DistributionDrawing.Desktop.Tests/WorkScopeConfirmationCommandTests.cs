using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using System.IO;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkScopeConfirmationCommandTests
{
    [Fact]
    public void Confirm_ExistingTicketAddsOneScopeAndPreservesEveryOtherTicketFact()
    {
        Fixture fixture = CreateFixture();
        GroundingPoint groundingPoint = fixture.Drawing.CreateGroundingPoint(Guid.NewGuid(),
            fixture.Cabinet.Intervals[1].SwitchDevices[0].FirstTerminalId, "站内接地位置", "G-1");
        SwitchDevice boundarySwitch = fixture.Cabinet.Intervals[0].SwitchDevices[0];
        TicketReference deviceRef = new(TicketReferenceKind.Device, boundarySwitch.Id);
        MeasureFact measure = new(Guid.NewGuid(), "6.1", MeasureKind.OpenSwitch, "switch.open",
            [deviceRef], Origin: FactOrigin.UserAdded);
        var ticket = WorkTicketSession.Create() with
        {
            Task = new WorkTask("检修", "一号环网柜"),
            IsolationBoundaries = [new IsolationBoundary(boundarySwitch.Id, BoundarySide.Line,
                boundarySwitch.SecondTerminalId)],
            GroundingPointIds = [groundingPoint.GroundingPointId],
            UserFacts = [new UserTicketFact("note", "现场补充", [deviceRef], true)],
            Analysis = new WorkTicketAnalysis([measure], [measure],
                [new RetainedLivePart("邻近设备", [deviceRef], FactOrigin.UserAdded)],
                [new RiskItem("现场风险", "复核", FactOrigin.UserAdded)],
                [new RestorationMeasure(measure.Id, "switch.restore", [deviceRef], null, null,
                    FactOrigin.UserAdded)], ["保留分析问题"]) ,
            Draft = new WorkTicketDraft([new SectionDraft("6.1",
                [new DraftItem(Guid.NewGuid(), "生成内容", "用户编辑内容", DraftOrigin.UserEdited,
                    [deviceRef]) { Source = FactOrigin.UserAdded, IsUserEdited = true }],
                SectionCompletion.Completed)]),
            AnalyzedFingerprint = "fingerprint-before",
            RulePackVersion = "rule-before",
            PhraseLibraryVersion = "phrase-before",
            WorkScopeItems = [new WorkScopeItem(WorkScopeItemKind.Equipment, boundarySwitch.Id)]
        };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [ticket]);
        WorkScopeConfirmationPlan plan = Prepare(fixture, tickets, ticket.Id);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();
        EnergizationResult currentEa = fixture.State.CurrentResult!;

        stack.ExecuteCommand(command);

        WorkTicketSession confirmed = Assert.IsType<WorkTicketSession>(tickets.Selected(ticket.Id));
        Assert.Single(stack.History);
        Assert.Equal(1, stack.CurrentIndex);
        Assert.True(stack.IsDirty);
        Assert.Equal([plan.AfterWorkScope.WorkScopeId], confirmed.WorkScopeIds);
        Assert.Equal(ticket with { WorkScopeIds = confirmed.WorkScopeIds }, confirmed);
        Assert.Same(ticket.Analysis, confirmed.Analysis);
        Assert.Same(ticket.Draft, confirmed.Draft);
        Assert.Equal(ticket.IsolationBoundaries, confirmed.IsolationBoundaries);
        Assert.Equal(ticket.WorkScopeItems, confirmed.WorkScopeItems);
        Assert.Equal(ticket.GroundingPointIds, confirmed.GroundingPointIds);
        Assert.Equal(ticket.Task, confirmed.Task);
        Assert.Equal(ticket.UserFacts, confirmed.UserFacts);
        Assert.Equal(ticket.RulePackVersion, confirmed.RulePackVersion);
        Assert.Equal(ticket.PhraseLibraryVersion, confirmed.PhraseLibraryVersion);
        Assert.Equal(ticket.AnalyzedFingerprint, confirmed.AnalyzedFingerprint);
        Assert.Equal(currentEa, fixture.State.CurrentResult);
        WorkTicketReferenceGuard.Validate(fixture.Drawing, tickets);

        Assert.True(stack.Undo());
        Assert.Same(ticket, tickets.Selected(ticket.Id));
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Equal(0, stack.CurrentIndex);
        Assert.False(stack.IsDirty);
        Assert.Equal(currentEa, fixture.State.CurrentResult);

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(ticket.Id));
        Assert.Equal(plan.AfterWorkScope.WorkScopeId, Assert.Single(fixture.Drawing.WorkScopes).WorkScopeId);
        Assert.Equal(1, stack.CurrentIndex);
        Assert.True(stack.IsDirty);
        Assert.Equal(currentEa, fixture.State.CurrentResult);
        WorkTicketReferenceGuard.Validate(fixture.Drawing, tickets);
    }

    [Fact]
    public void Confirm_WithoutTargetTicketCreatesStableTicketAndScopeWithinOneHistoryEntry()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        Guid ticketId = Guid.NewGuid();
        Guid scopeId = Guid.NewGuid();
        WorkScopeConfirmationPlan plan = Prepare(fixture, tickets, null, ticketId, scopeId);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        Assert.Same(plan.AfterTicket, tickets.Selected(ticketId));
        Assert.Single(plan.AfterTicket.WorkScopeIds);
        Assert.Equal(scopeId, plan.AfterTicket.WorkScopeIds[0]);
        Assert.Equal(scopeId, Assert.Single(fixture.Drawing.WorkScopes).WorkScopeId);
        Assert.Empty(plan.AfterTicket.IsolationBoundaries);
        Assert.Empty(plan.AfterTicket.WorkScopeItems);
        Assert.True(stack.Undo());
        Assert.Empty(tickets.Tickets);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(ticketId));
        Assert.Equal(scopeId, fixture.Drawing.GetWorkScope(scopeId).WorkScopeId);
        Assert.Equal(ticketId, tickets.Selected(ticketId)!.Id);
        Assert.Equal(scopeId, Assert.Single(tickets.Selected(ticketId)!.WorkScopeIds));
    }

    [Fact]
    public void Confirm_ExclusiveReconfirmReplacesSnapshotPreservesIdAndDescriptionAcrossUndoRedo()
    {
        Fixture fixture = CreateFixture();
        WorkScope firstSnapshot = MaterializeForSetup(fixture.Candidate, Guid.NewGuid(), "人工描述");
        fixture.Drawing.AddWorkScope(firstSnapshot);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        { WorkScopeIds = [firstSnapshot.WorkScopeId] };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [ticket]);
        fixture.State.Execute(fixture.Drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(fixture.Switch, EnergizationSide.Line)], true));
        WorkScopeCandidate current = Project(fixture.Drawing, fixture.State);
        var planner = new WorkScopeConfirmationPlanner();
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(planner.Prepare(
            fixture.Drawing, fixture.State, current, tickets, ticket.Id).Plan);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();

        stack.ExecuteCommand(command);

        WorkScope after = fixture.Drawing.GetWorkScope(firstSnapshot.WorkScopeId);
        Assert.Same(firstSnapshot, after);
        Assert.Equal("人工描述", after.Description);
        Assert.Equal(current.Regions.Select(item => string.Join(',', item.TerminalIds)),
            after.Regions.Select(item => string.Join(',', item.TerminalIds)));
        Assert.Equal(current.Boundaries.Select(item => item.DeenergizedTerminalId),
            after.Boundaries.Select(item => item.TerminalId!.Value));
        Assert.Single(stack.History);
        Assert.True(stack.Undo());
        Assert.Same(firstSnapshot, fixture.Drawing.GetWorkScope(firstSnapshot.WorkScopeId));
        Assert.Equal("人工描述", firstSnapshot.Description);
        Assert.Equal(fixture.Candidate.Regions.Select(item => string.Join(',', item.TerminalIds)),
            firstSnapshot.Regions.Select(item => string.Join(',', item.TerminalIds)));
        Assert.True(stack.Redo());
        Assert.Equal("人工描述", firstSnapshot.Description);
        Assert.Equal(current.Regions.Select(item => string.Join(',', item.TerminalIds)),
            firstSnapshot.Regions.Select(item => string.Join(',', item.TerminalIds)));
    }

    [Fact]
    public void Confirm_SharedScopeRelinksOnlyCurrentTicketAndRedoReusesNewId()
    {
        Fixture fixture = CreateFixture();
        WorkScope shared = MaterializeForSetup(fixture.Candidate, Guid.NewGuid(), "共享描述");
        fixture.Drawing.AddWorkScope(shared);
        WorkTicketSession first = WorkTicketSession.Create() with { WorkScopeIds = [shared.WorkScopeId] };
        WorkTicketSession second = WorkTicketSession.Create() with { WorkScopeIds = [shared.WorkScopeId] };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [first, second]);
        fixture.State.Execute(fixture.Drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(fixture.Switch, EnergizationSide.Line)], true));
        WorkScopeCandidate current = Project(fixture.Drawing, fixture.State);
        Guid newScopeId = Guid.NewGuid();
        WorkScopeConfirmationPlan plan = Prepare(fixture, tickets, first.Id, newScopeId);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();
        WorkScope beforeShared = MaterializeForSetup(fixture.Candidate, shared.WorkScopeId, shared.Description);

        stack.ExecuteCommand(command);

        Assert.Equal(newScopeId, Assert.Single(tickets.Selected(first.Id)!.WorkScopeIds));
        Assert.Equal(shared.WorkScopeId, Assert.Single(tickets.Selected(second.Id)!.WorkScopeIds));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Same(shared, fixture.Drawing.GetWorkScope(shared.WorkScopeId));
        Assert.Equal(beforeShared.Description, shared.Description);
        Assert.Equal(beforeShared.Regions.Select(item => string.Join(',', item.TerminalIds)),
            shared.Regions.Select(item => string.Join(',', item.TerminalIds)));
        WorkScope newScope = fixture.Drawing.GetWorkScope(newScopeId);
        Assert.Equal(current.Regions.Select(item => string.Join(',', item.TerminalIds)),
            newScope.Regions.Select(item => string.Join(',', item.TerminalIds)));
        Assert.Equal("共享描述", newScope.Description);

        Assert.True(stack.Undo());
        Assert.Same(first, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Throws<InvalidOperationException>(() => fixture.Drawing.GetWorkScope(newScopeId));
        Assert.Same(shared, fixture.Drawing.GetWorkScope(shared.WorkScopeId));

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Equal(newScopeId, Assert.Single(tickets.Selected(first.Id)!.WorkScopeIds));
        Assert.Equal(newScopeId, fixture.Drawing.GetWorkScope(newScopeId).WorkScopeId);
    }

    [Theory]
    [InlineData((int)ConfirmWorkScopeCommandStage.ExecuteAfterScope)]
    [InlineData((int)ConfirmWorkScopeCommandStage.UndoAfterTicket)]
    [InlineData((int)ConfirmWorkScopeCommandStage.RedoAfterScope)]
    public void Command_RollsBackEveryInjectedPartialExecuteUndoAndRedoFailure(
        int failureStageValue)
    {
        ConfirmWorkScopeCommandStage failureStage = (ConfirmWorkScopeCommandStage)failureStageValue;
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        WorkScopeConfirmationPlan plan = Prepare(fixture, tickets);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan, stage =>
        {
            if (stage == failureStage) throw new InvalidOperationException("injected confirmation failure");
        });
        var stack = new CommandStack();

        if (failureStage == ConfirmWorkScopeCommandStage.ExecuteAfterScope)
        {
            Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(command));
            Assert.Empty(fixture.Drawing.WorkScopes);
            Assert.Empty(tickets.Tickets);
            Assert.Empty(stack.History);
            Assert.Equal(0, stack.CurrentIndex);
            Assert.False(stack.IsDirty);
        }
        else if (failureStage == ConfirmWorkScopeCommandStage.UndoAfterTicket)
        {
            stack.ExecuteCommand(command);
            WorkTicketSession afterTicket = Assert.IsType<WorkTicketSession>(tickets.Selected(plan.TicketId));
            Assert.Throws<InvalidOperationException>(() => stack.Undo());
            Assert.Same(afterTicket, tickets.Selected(plan.TicketId));
            Assert.Equal(plan.AfterWorkScope.WorkScopeId,
                Assert.Single(fixture.Drawing.WorkScopes).WorkScopeId);
            Assert.Single(stack.History);
            Assert.Equal(1, stack.CurrentIndex);
            Assert.True(stack.IsDirty);
        }
        else
        {
            stack.ExecuteCommand(command);
            Assert.True(stack.Undo());
            Assert.Throws<InvalidOperationException>(() => stack.Redo());
            Assert.Empty(fixture.Drawing.WorkScopes);
            Assert.Empty(tickets.Tickets);
            Assert.Single(stack.History);
            Assert.Equal(0, stack.CurrentIndex);
            Assert.False(stack.IsDirty);
        }
    }

    [Fact]
    public void Confirm_UndoRedoKeepRuntimeEaResultCurrentAndDoNotReanalyze()
    {
        Fixture fixture = CreateFixture();
        ProjectSession persistence = new ProjectService().CreateProject(
            Path.Combine(Path.GetTempPath(), $"wp-ws-confirm-{Guid.NewGuid():N}.kvdrawing"),
            "Confirm runtime");
        persistence.Domain.AddDevice(fixture.Cabinet);
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(persistence);
        runtime.Energization.Execute(persistence.Domain,
            new EnergizationScenario(Guid.NewGuid(), [Seed(fixture.Switch, EnergizationSide.Bus)], true));
        EnergizationResult before = runtime.Energization.CurrentResult!;
        WorkScopeCandidate candidate = Project(persistence.Domain, runtime.Energization);
        var plan = Assert.IsType<WorkScopeConfirmationPlan>(new WorkScopeConfirmationPlanner().Prepare(
            persistence.Domain, runtime.Energization, candidate, persistence.WorkTickets).Plan);
        var command = new ConfirmWorkScopeCommand(persistence.Domain, persistence.WorkTickets, plan);

        Assert.False(command.AffectsEnergization);
        runtime.CommandStack.ExecuteCommand(command);
        Assert.Same(before, runtime.Energization.CurrentResult);
        Assert.True(runtime.CommandStack.Undo());
        Assert.Same(before, runtime.Energization.CurrentResult);
        Assert.True(runtime.CommandStack.Redo());
        Assert.Same(before, runtime.Energization.CurrentResult);
    }

    private static Fixture CreateFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Confirm command fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Confirm command",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices[0];
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(seedSwitch, EnergizationSide.Bus)], true));
        return new Fixture(drawing, cabinet, seedSwitch, state, Project(drawing, state));
    }

    private static WorkScopeConfirmationPlan Prepare(
        Fixture fixture,
        WorkTicketDataRoot tickets,
        Guid? targetTicketId = null,
        params Guid[] generatedIds)
    {
        int index = 0;
        var planner = new WorkScopeConfirmationPlanner(() =>
            index < generatedIds.Length ? generatedIds[index++] : Guid.NewGuid());
        return Assert.IsType<WorkScopeConfirmationPlan>(planner.Prepare(
            fixture.Drawing, fixture.State, fixture.Candidate, tickets, targetTicketId).Plan);
    }

    private static WorkScopeCandidate Project(DrawingDocument drawing, EnergizationAnalysisState state) =>
        new WorkScopeCandidateProjector().Project(drawing, state).Candidate!;

    private static WorkScope MaterializeForSetup(WorkScopeCandidate candidate, Guid id, string? description) =>
        WorkScope.Create(id,
            candidate.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
            candidate.Boundaries.Select(boundary => new WorkScopeBoundary(boundary.SwitchDeviceId,
                BoundarySide.Line, boundary.DeenergizedTerminalId)), description);

    private static EnergizedSeed Seed(SwitchDevice device, EnergizationSide side) =>
        new(Guid.NewGuid(), device.Id, side);

    private sealed record Fixture(
        DrawingDocument Drawing,
        RingCabinet Cabinet,
        SwitchDevice Switch,
        EnergizationAnalysisState State,
        WorkScopeCandidate Candidate);
}
