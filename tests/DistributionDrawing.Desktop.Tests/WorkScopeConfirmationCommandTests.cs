using DistributionDrawing.TestSupport;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;
using System.IO;
using System.Text.Json;
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
        WorkScope firstSnapshot = MaterializeForSetup(
            fixture.Drawing, fixture.Candidate, Guid.NewGuid(), "人工描述");
        fixture.Drawing.AddWorkScope(firstSnapshot);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        { WorkScopeIds = [firstSnapshot.WorkScopeId] };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [ticket]);
        WorkScopeAnalysisFixture.Execute(fixture.State,fixture.Drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(fixture.Switch, EnergizationSide.Line)], true));
        WorkScopeCandidate current = Project(fixture.Drawing, fixture.State);
        var planner = new WorkScopeConfirmationPlanner();
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(planner.Prepare(
            fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), current, tickets, ticket.Id).Plan);
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
        WorkScopeAnalysisFixture.Execute(fixture.State,fixture.Drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(fixture.Switch, EnergizationSide.Line)], true));
        EnergizationResult currentEa = Assert.IsType<EnergizationResult>(fixture.State.CurrentResult);
        WorkScopeCandidate beforeSnapshotMutation = Project(fixture.Drawing, fixture.State);
        WorkScope shared = MaterializeForSetup(
            fixture.Drawing, beforeSnapshotMutation, Guid.NewGuid(), "共享描述");
        fixture.Drawing.AddWorkScope(shared);
        WorkTicketSession first = WorkTicketSession.Create() with { WorkScopeIds = [shared.WorkScopeId] };
        WorkTicketSession second = WorkTicketSession.Create() with { WorkScopeIds = [shared.WorkScopeId] };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [first, second]);
        WorkScopeCandidate reviewed = Project(fixture.Drawing, fixture.State);
        WorkScopeCandidate current = Project(fixture.Drawing, fixture.State);
        Assert.Equal(CandidateSignature(beforeSnapshotMutation), CandidateSignature(reviewed));
        Assert.Equal(CandidateSignature(reviewed), CandidateSignature(current));
        Assert.Equal(EnergizationFreshness.Current, fixture.State.Freshness);
        Assert.Same(currentEa, fixture.State.CurrentResult);
        Guid newScopeId = Guid.NewGuid();
        var planning = new WorkScopeConfirmationPlanner(() => newScopeId).Prepare(
            fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), reviewed, tickets, first.Id);
        Assert.True(planning.CanConfirm, planning.Diagnostic?.Message);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(planning.Plan);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();
        WorkScope beforeShared = CloneForAssertion(shared);

        stack.ExecuteCommand(command);

        WorkTicketSession confirmedFirst = Assert.IsType<WorkTicketSession>(tickets.Selected(first.Id));
        WorkTicketSession confirmedSecond = Assert.IsType<WorkTicketSession>(tickets.Selected(second.Id));
        Assert.Equal(newScopeId, Assert.Single(confirmedFirst.WorkScopeIds));
        Assert.Equal(TicketSignature(first with { WorkScopeIds = [newScopeId] }),
            TicketSignature(confirmedFirst));
        Assert.Equal(shared.WorkScopeId, Assert.Single(confirmedSecond.WorkScopeIds));
        Assert.Equal(TicketSignature(second), TicketSignature(confirmedSecond));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Same(shared, fixture.Drawing.GetWorkScope(shared.WorkScopeId));
        Assert.Equal(WorkScopeSignature(beforeShared), WorkScopeSignature(shared));
        WorkScope newScope = fixture.Drawing.GetWorkScope(newScopeId);
        Assert.NotEqual(shared.WorkScopeId, newScope.WorkScopeId);
        Assert.Equal(CandidateRegionSignature(current), WorkScopeRegionSignature(newScope));
        Assert.Equal(ExpectedWorkScopeBoundarySignature(fixture.Drawing, current),
            WorkScopeBoundarySignature(newScope));
        Assert.Equal("共享描述", newScope.Description);
        Assert.Single(stack.History);

        Assert.True(stack.Undo());
        Assert.Same(first, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Throws<InvalidOperationException>(() => fixture.Drawing.GetWorkScope(newScopeId));
        Assert.Same(shared, fixture.Drawing.GetWorkScope(shared.WorkScopeId));
        Assert.Equal(WorkScopeSignature(beforeShared),
            WorkScopeSignature(fixture.Drawing.GetWorkScope(shared.WorkScopeId)));

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Equal(newScopeId, Assert.Single(tickets.Selected(first.Id)!.WorkScopeIds));
        Assert.Equal(newScopeId, fixture.Drawing.GetWorkScope(newScopeId).WorkScopeId);
        Assert.Equal(CandidateRegionSignature(current),
            WorkScopeRegionSignature(fixture.Drawing.GetWorkScope(newScopeId)));
        Assert.Equal(ExpectedWorkScopeBoundarySignature(fixture.Drawing, current),
            WorkScopeBoundarySignature(fixture.Drawing.GetWorkScope(newScopeId)));
        Assert.Equal(currentEa, fixture.State.CurrentResult);
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
        ProjectSession persistence = new ProjectService().CreateProject(
            Path.Combine(Path.GetTempPath(), $"wp-ws-confirm-{Guid.NewGuid():N}.kvdrawing"),
            "Confirm runtime");
        ProjectRuntimeSession runtime = ProjectRuntimeSession.CreateEmpty(persistence);
        AddRingCabinetCommand addCabinet = new DeviceCommandFactory().CreateAddRingCabinet(
            persistence.Domain,
            runtime.Layout,
            new RingCabinetCreationConfiguration(
                "Confirm runtime cabinet",
                new RingCabinetCreationTemplateFactory().Create(RingCabinetTemplateType.Conventional, 4),
                "10kV"),
            new DocumentPoint(20, 20));
        runtime.CommandStack.ExecuteCommand(addCabinet);
        SwitchDevice seedSwitch = addCabinet.Cabinet.Intervals[0].SwitchDevices[0];
        WorkScopeAnalysisFixture.Execute(runtime.Energization,persistence.Domain,
            new EnergizationScenario(Guid.NewGuid(), [Seed(seedSwitch, EnergizationSide.Bus)], true));
        EnergizationResult before = Assert.IsType<EnergizationResult>(runtime.Energization.CurrentResult);
        WorkScopeCandidateProjection reviewedProjection = new WorkScopeCandidateProjector()
            .Project(persistence.Domain, runtime.Energization, WorkScopeAnalysisFixture.ScenarioFor(runtime.Energization));
        Assert.True(reviewedProjection.IsValid);
        WorkScopeCandidate reviewedCandidate = Assert.IsType<WorkScopeCandidate>(reviewedProjection.Candidate);
        Assert.NotEmpty(reviewedCandidate.Regions);
        Assert.Empty(reviewedProjection.Diagnostics);
        WorkScopeCandidate currentCandidate = Project(persistence.Domain, runtime.Energization);
        Assert.Equal(CandidateSignature(reviewedCandidate), CandidateSignature(currentCandidate));
        var tickets = persistence.WorkTickets;
        var planResult = new WorkScopeConfirmationPlanner().Prepare(
            persistence.Domain, runtime.Energization, WorkScopeAnalysisFixture.ScenarioFor(runtime.Energization), reviewedCandidate, tickets);
        Assert.True(planResult.CanConfirm, planResult.Diagnostic?.Message);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(planResult.Plan);
        var command = new ConfirmWorkScopeCommand(persistence.Domain, persistence.WorkTickets, plan);
        int eaChangedEvents = 0;
        runtime.Energization.Changed += (_, _) => eaChangedEvents++;

        Assert.False(command.AffectsEnergization);
        runtime.CommandStack.ExecuteCommand(command);
        Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
        Assert.Same(before, runtime.Energization.CurrentResult);
        Guid ticketId = plan.TicketId;
        Guid workScopeId = plan.AfterWorkScope.WorkScopeId;
        Assert.Equal([workScopeId], tickets.Selected(ticketId)!.WorkScopeIds);
        Assert.Equal(workScopeId, persistence.Domain.GetWorkScope(workScopeId).WorkScopeId);
        Assert.Equal(0, eaChangedEvents);

        Assert.True(runtime.CommandStack.Undo());
        Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
        Assert.Same(before, runtime.Energization.CurrentResult);
        Assert.Null(tickets.Selected(ticketId));
        Assert.Throws<InvalidOperationException>(() => persistence.Domain.GetWorkScope(workScopeId));
        Assert.Equal(0, eaChangedEvents);

        Assert.True(runtime.CommandStack.Redo());
        Assert.Equal(EnergizationFreshness.Current, runtime.Energization.Freshness);
        Assert.Same(before, runtime.Energization.CurrentResult);
        Assert.Same(plan.AfterTicket, tickets.Selected(ticketId));
        Assert.Equal(workScopeId, persistence.Domain.GetWorkScope(workScopeId).WorkScopeId);
        Assert.Equal(ticketId, tickets.Selected(ticketId)!.Id);
        Assert.Equal(0, eaChangedEvents);
    }

    [Fact]
    public void Handoff_CompleteConfirmUsesOneHistoryEntryAndRedoDoesNotReanalyze()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingHandoffAnalyzer();
        var projection = new CountingProjectionService(new WorkScopeIsolationBoundaryProjector());
        WorkScopeHandoffPlanningResult prepared = new WorkScopeHandoffPlanner(
            new WorkScopeConfirmationPlanner(), projection, analyzer)
            .Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), fixture.Candidate, tickets);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();
        EnergizationResult eaResult = Assert.IsType<EnergizationResult>(fixture.State.CurrentResult);

        Assert.False(command.AffectsEnergization);
        Assert.True(prepared.CanExecute);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(1, projection.Calls);
        Assert.NotNull(analyzer.LastInput);
        Assert.Equal(plan.AfterTicket.IsolationBoundaries, analyzer.LastInput!.IsolationBoundaries);
        Assert.Null(analyzer.LastInput.Analysis);

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        WorkTicketSession committed = Assert.IsType<WorkTicketSession>(tickets.Selected(plan.TicketId));
        Assert.Equal(plan.AfterWorkScope.WorkScopeId, Assert.Single(committed.WorkScopeIds));
        Assert.Equal(prepared.Projection!.IsolationBoundaries, committed.IsolationBoundaries);
        Assert.NotNull(committed.Analysis);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(1, projection.Calls);
        Assert.Same(eaResult, fixture.State.CurrentResult);

        Assert.True(stack.Undo());
        Assert.Empty(tickets.Tickets);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(1, projection.Calls);
        Assert.Same(eaResult, fixture.State.CurrentResult);

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(plan.TicketId));
        Assert.Equal(plan.AfterWorkScope.WorkScopeId,
            fixture.Drawing.GetWorkScope(plan.AfterWorkScope.WorkScopeId).WorkScopeId);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(1, projection.Calls);
        Assert.Same(eaResult, fixture.State.CurrentResult);
    }

    [Fact]
    public void Handoff_SelectedDeviceWithoutWorkSideConfirmsInOneHistoryEntryWithoutAnalyzer()
    {
        Fixture fixture = CreateFixture(withOpenBoundary: false);
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingHandoffAnalyzer();
        WorkScopeHandoffPlanningResult prepared = new WorkScopeHandoffPlanner(
            new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer)
            .Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), fixture.Candidate, tickets);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        WorkTicketSession committed = Assert.IsType<WorkTicketSession>(tickets.Selected(plan.TicketId));
        Assert.Empty(committed.IsolationBoundaries);
        Assert.Equal(0, analyzer.Calls);
        Assert.Null(committed.Draft);
        Assert.Equal(BoundarySide.Unknown, Assert.Single(plan.AfterWorkScope.Boundaries).Side);
        Assert.Throws<InvalidOperationException>(() => new WorkTicketAnalyzer().Analyze(
            fixture.Drawing, WorkTicketSession.Create()));
    }

    [Fact]
    public void Handoff_SharedScopeReconfirmUpdatesOnlyTargetTicketAndUndoRedoPreservesOtherTicket()
    {
        Fixture fixture = CreateFixture();
        WorkScope shared = MaterializeForSetup(fixture.Drawing, fixture.Candidate, Guid.NewGuid(), "shared scope");
        fixture.Drawing.AddWorkScope(shared);
        WorkTicketSession first = WorkTicketSession.Create() with { WorkScopeIds = [shared.WorkScopeId] };
        WorkTicketSession second = WorkTicketSession.Create() with
        {
            WorkScopeIds = [shared.WorkScopeId],
            UserFacts = [new UserTicketFact("other-ticket", "untouched", [], true)]
        };
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id, [first, second]);
        var analyzer = new CountingHandoffAnalyzer();
        WorkScopeHandoffPlanningResult prepared = new WorkScopeHandoffPlanner(
            new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer)
            .Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), fixture.Candidate, tickets, first.Id);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        Assert.NotEqual(shared.WorkScopeId, plan.AfterWorkScope.WorkScopeId);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan);
        var stack = new CommandStack();

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        Assert.Same(plan.AfterTicket, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Equal(shared.WorkScopeId, Assert.Single(second.WorkScopeIds));
        Assert.Same(shared, fixture.Drawing.GetWorkScope(shared.WorkScopeId));
        Assert.NotNull(tickets.Selected(first.Id)!.Analysis);
        Assert.Equal(1, analyzer.Calls);

        Assert.True(stack.Undo());
        Assert.Same(first, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Throws<InvalidOperationException>(() => fixture.Drawing.GetWorkScope(plan.AfterWorkScope.WorkScopeId));

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(first.Id));
        Assert.Same(second, tickets.Selected(second.Id));
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public void Handoff_ExecuteFailureRollsBackFullAnalyzedAfterSnapshotAndDoesNotRecordHistory()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingHandoffAnalyzer();
        WorkScopeHandoffPlanningResult prepared = new WorkScopeHandoffPlanner(
            new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer)
            .Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), fixture.Candidate, tickets);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        var command = new ConfirmWorkScopeCommand(fixture.Drawing, tickets, plan, stage =>
        {
            if (stage == ConfirmWorkScopeCommandStage.ExecuteAfterScope)
                throw new InvalidOperationException("injected handoff command failure");
        });
        var stack = new CommandStack();

        Assert.NotNull(plan.AfterTicket.Analysis);
        Assert.NotNull(plan.AfterTicket.Draft);
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(command));

        Assert.Empty(tickets.Tickets);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Empty(stack.History);
        Assert.Equal(0, stack.CurrentIndex);
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public void Handoff_UnrepresentableConfirmClearsStaleDerivedStateAndUndoRestoresBeforeSnapshot()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Unrepresentable handoff");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Source",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        CustomerStation station = new DistributionDrawing.Application.Devices.CustomerStations.CustomerStationCreationFactory()
            .Create(StationKind.BoxStation,
            ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        drawing.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value, feeder.CableTerminalId, "C1", "10kV"));
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(seedSwitch, EnergizationSide.Bus)], true));
        WorkScopeCandidate candidate = Project(drawing, state);
        Assert.DoesNotContain(candidate.Boundaries, item => item.SwitchDeviceId == feeder.IsolationSwitch.Id);
        var oldAnalysis = new WorkTicketAnalysis([], [], [], [], [], ["stale"]);
        var oldDraft = new WorkTicketDraft([new SectionDraft("6.1",
            [new DraftItem(Guid.NewGuid(), "generated", "edited", DraftOrigin.UserEdited, [])
                { IsUserEdited = true, Source = FactOrigin.UserAdded }], SectionCompletion.Completed)]);
        WorkTicketSession before = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [new IsolationBoundary(seedSwitch.Id, BoundarySide.Bus)],
            Analysis = oldAnalysis,
            Draft = oldDraft,
            AnalyzedFingerprint = "stale fingerprint",
            RulePackVersion = "stale rules",
            PhraseLibraryVersion = "stale phrases",
            UserFacts = [new UserTicketFact("note", "preserve", [], true)],
            GroundingPointIds = [Guid.NewGuid()]
        };
        var tickets = new WorkTicketDataRoot(drawing.Id, [before]);
        var analyzer = new CountingHandoffAnalyzer();
        WorkScopeHandoffPlanningResult prepared = new WorkScopeHandoffPlanner(
            new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer)
            .Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets, before.Id);
        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, prepared.Status);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(prepared.Plan);
        var command = new ConfirmWorkScopeCommand(drawing, tickets, plan);
        var stack = new CommandStack();

        stack.ExecuteCommand(command);

        Assert.Single(stack.History);
        Assert.Same(plan.AfterTicket, tickets.Selected(before.Id));
        Assert.Single(plan.AfterTicket.WorkScopeIds);
        Assert.Empty(plan.AfterTicket.IsolationBoundaries);
        Assert.Null(plan.AfterTicket.Analysis);
        Assert.Null(plan.AfterTicket.AnalyzedFingerprint);
        Assert.Equal(SectionCompletion.Stale, plan.AfterTicket.Draft!.Section("6.1").Completion);
        Assert.Equal(oldDraft.Section("6.1").Items, plan.AfterTicket.Draft.Section("6.1").Items);
        Assert.Equal(before.UserFacts, plan.AfterTicket.UserFacts);
        Assert.Equal(before.GroundingPointIds, plan.AfterTicket.GroundingPointIds);
        Assert.Equal(0, analyzer.Calls);

        Assert.True(stack.Undo());
        Assert.Same(before, tickets.Selected(before.Id));
        Assert.Empty(drawing.WorkScopes);
        Assert.Same(oldAnalysis, tickets.Selected(before.Id)!.Analysis);

        Assert.True(stack.Redo());
        Assert.Same(plan.AfterTicket, tickets.Selected(before.Id));
        Assert.Equal(0, analyzer.Calls);
    }

    private static Fixture CreateFixture(bool withOpenBoundary = true)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Confirm command fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Confirm command",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                 withOpenBoundary ? SwitchState.Open : SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        if (!withOpenBoundary) _ = AddIsolatedOpenSwitch(drawing);
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices[0];
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
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
            fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State), fixture.Candidate, tickets, targetTicketId).Plan);
    }

    private static WorkScopeCandidate Project(DrawingDocument drawing, EnergizationAnalysisState state) =>
        new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

    private static WorkScope MaterializeForSetup(
        DrawingDocument drawing,
        WorkScopeCandidate candidate,
        Guid id,
        string? description) =>
        WorkScope.Create(id,
            candidate.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
            candidate.Boundaries.Select(boundary => new WorkScopeBoundary(boundary.SwitchDeviceId,
                drawing.Terminals.Single(item => item.Id == boundary.DeenergizedTerminalId).Role switch
                {
                    "BusSide" => BoundarySide.Bus,
                    "CircuitSide" => BoundarySide.Line,
                    _ => BoundarySide.Unknown
                }, boundary.DeenergizedTerminalId)), description);

    private static WorkScope CloneForAssertion(WorkScope scope) => WorkScope.Create(
        scope.WorkScopeId,
        scope.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
        scope.Boundaries,
        scope.Description);

    private static string CandidateSignature(WorkScopeCandidate candidate) =>
        $"{string.Join(";", CandidateRegionSignature(candidate))}|" +
        $"{string.Join(";", CandidateBoundarySignature(candidate))}|" +
        string.Join(";", candidate.Diagnostics.Select(item => $"{item.Code}:{item.Identity}:{item.Detail}"));

    private static string[] CandidateRegionSignature(WorkScopeCandidate candidate) => candidate.Regions
        .Select(region => $"{string.Join(',', region.TerminalIds)} / {string.Join(',', region.ElectricalNodeIds)}")
        .ToArray();

    private static string[] CandidateBoundarySignature(WorkScopeCandidate candidate) => candidate.Boundaries
        .Select(item => $"{item.SwitchDeviceId:N}:{item.SwitchKind}:{item.InstallationType}:" +
            $"{item.DeenergizedTerminalId:N}:{item.EnergizedTerminalId:N}:{item.TopologyParentId:N}:" +
            $"{item.AttachedPoleId:N}:{string.Join(',', item.RelatedConnectionIds)}")
        .ToArray();

    private static string[] WorkScopeRegionSignature(WorkScope scope) => scope.Regions
        .Select(region => $"{string.Join(',', region.TerminalIds)} / {string.Join(',', region.ElectricalNodeIds)}")
        .ToArray();

    private static string[] WorkScopeBoundarySignature(WorkScope scope) => scope.Boundaries
        .Select(item => $"{item.DeviceId:N}:{item.Side}:{item.TerminalId:N}:{item.ConnectionId:N}")
        .ToArray();

    private static string[] ExpectedWorkScopeBoundarySignature(
        DrawingDocument drawing,
        WorkScopeCandidate candidate) => candidate.Boundaries.Select(item =>
        {
            Terminal deenergized = drawing.Terminals.Single(terminal =>
                terminal.Id == item.DeenergizedTerminalId);
            BoundarySide side = deenergized.Role switch
            {
                "BusSide" => BoundarySide.Bus,
                "CircuitSide" => BoundarySide.Line,
                _ => BoundarySide.Unknown
            };
            Guid? connectionId = item.RelatedConnectionIds.Select(id =>
                    drawing.Connections.SingleOrDefault(connection => connection.Id == id))
                .SingleOrDefault(connection => connection?.UsesTerminal(deenergized.Id) == true)?.Id;
            return $"{item.SwitchDeviceId:N}:{side}:{deenergized.Id:N}:{connectionId:N}";
        }).ToArray();

    private static string WorkScopeSignature(WorkScope scope) =>
        $"{scope.WorkScopeId:N}|{scope.Description}|" +
        $"{string.Join(";", WorkScopeRegionSignature(scope))}|" +
        string.Join(";", WorkScopeBoundarySignature(scope));

    private static string TicketSignature(WorkTicketSession ticket) =>
        JsonSerializer.Serialize(ticket);

    private static SwitchDevice AddIsolatedOpenSwitch(DrawingDocument drawing)
    {
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
            device.Id, "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
            device.Id, "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        return device;
    }

    private static EnergizedSeed Seed(SwitchDevice device, EnergizationSide side) =>
        new(Guid.NewGuid(), device.Id, side);

    private sealed class CountingHandoffAnalyzer : IWorkTicketHandoffAnalyzer
    {
        private readonly WorkTicketAnalyzer _inner = new();
        public int Calls { get; private set; }
        public WorkTicketSession? LastInput { get; private set; }

        public WorkTicketSession AnalyzeConfirmedWorkScopeHandoff(
            DrawingDocument drawing, WorkTicketSession ticket)
        {
            Calls++;
            LastInput = ticket;
            return _inner.AnalyzeConfirmedWorkScopeHandoff(drawing, ticket);
        }
    }

    private sealed class CountingProjectionService(IWorkScopeIsolationBoundaryProjectionService inner)
        : IWorkScopeIsolationBoundaryProjectionService
    {
        public int Calls { get; private set; }

        public WorkScopeIsolationBoundaryProjection Project(DrawingDocument drawing, WorkScope confirmedSnapshot)
        {
            Calls++;
            return inner.Project(drawing, confirmedSnapshot);
        }
    }

    private sealed record Fixture(
        DrawingDocument Drawing,
        RingCabinet Cabinet,
        SwitchDevice Switch,
        EnergizationAnalysisState State,
        WorkScopeCandidate Candidate);
}
