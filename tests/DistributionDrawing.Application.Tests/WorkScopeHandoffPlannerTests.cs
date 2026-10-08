using DistributionDrawing.TestSupport;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkScopeHandoffPlannerTests
{
    [Fact]
    public void Prepare_CompleteProjectionAnalyzesFinalTicketOnceAndCapturesSnapshot()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var projection = new CountingProjectionService(new WorkScopeIsolationBoundaryProjector());
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);
        var task = new WorkTask("校验集成确认任务", "测试工作对象");

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets, task: task);

        Assert.Equal(WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput, result.Status);
        Assert.True(result.CanExecute);
        Assert.Equal(WorkScopeHandoffDiagnosticCode.AnalyzerNeedsInput, result.Diagnostic!.Code);
        Assert.Equal(1, projection.Calls);
        Assert.Equal(1, analyzer.Calls);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Equal(plan.AfterWorkScope.WorkScopeId, Assert.Single(plan.AfterTicket.WorkScopeIds));
        Assert.Equal(result.Projection!.IsolationBoundaries, plan.AfterTicket.IsolationBoundaries);
        Assert.NotNull(plan.AfterTicket.Analysis);
        Assert.NotNull(plan.AfterTicket.Draft);
        Assert.NotNull(analyzer.LastInput);
        Assert.Equal(plan.AfterTicket.Id, analyzer.LastInput!.Id);
        Assert.Equal(task, analyzer.LastInput.Task);
        Assert.Equal(plan.AfterTicket.WorkScopeIds, analyzer.LastInput.WorkScopeIds);
        Assert.Null(analyzer.LastInput.Analysis);
        Assert.Equal(plan.AfterTicket.IsolationBoundaries, analyzer.LastInput!.IsolationBoundaries);
    }

    [Fact]
    public void Prepare_SelectedDeviceWithoutWorkSideConfirmsWithoutAnalyzerOrManualFallback()
    {
        Fixture fixture = CreateFixture(withOpenBoundary: false);
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(),
            new CountingProjectionService(new WorkScopeIsolationBoundaryProjector()), analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);

        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, result.Status);
        Assert.True(result.CanExecute);
        Assert.Empty(result.Projection!.IsolationBoundaries);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Empty(plan.AfterTicket.IsolationBoundaries);
        Assert.Equal(0, analyzer.Calls);
        Assert.Null(plan.AfterTicket.Draft);
        Assert.Equal(BoundarySide.Unknown, Assert.Single(plan.AfterWorkScope.Boundaries).Side);
        Assert.Throws<InvalidOperationException>(() => new WorkTicketAnalyzer().Analyze(
            fixture.Drawing, WorkTicketSession.Create()));
    }

    [Fact]
    public void Prepare_ConfirmationRejectionDoesNotCallPhase4OrAnalyzer()
    {
        Fixture fixture = CreateFixture();
        fixture.State.Invalidate();
        var projection = new CountingProjectionService(new WorkScopeIsolationBoundaryProjector());
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, new WorkTicketDataRoot(fixture.Drawing.Id));

        Assert.Equal(WorkScopeHandoffStatus.ConfirmationRejected, result.Status);
        Assert.False(result.CanExecute);
        Assert.NotNull(result.ConfirmationDiagnostic);
        Assert.Null(result.Projection);
        Assert.Equal(0, projection.Calls);
        Assert.Equal(0, analyzer.Calls);
        Assert.Empty(fixture.Drawing.WorkScopes);
    }

    [Fact]
    public void Prepare_AnalyzerExceptionReturnsPreparationFailureWithoutMutation()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer((_, _) => throw new InvalidOperationException("analysis fault"));
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(),
            new CountingProjectionService(new WorkScopeIsolationBoundaryProjector()), analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);

        Assert.Equal(WorkScopeHandoffStatus.PreparationFailed, result.Status);
        Assert.False(result.CanExecute);
        Assert.Equal(WorkScopeHandoffDiagnosticCode.AnalyzerPreparationFailed, result.Diagnostic!.Code);
        Assert.Equal(1, analyzer.Calls);
        Assert.Empty(tickets.Tickets);
        Assert.Empty(fixture.Drawing.WorkScopes);
    }

    [Fact]
    public void Prepare_InvalidPhase4FreshSnapshotFailsBeforeAnalyzerAndCommandCanRun()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var projection = new CountingProjectionService((_, _) =>
        {
            var diagnostic = new WorkScopeBoundaryProjectionDiagnostic(
                WorkScopeBoundaryProjectionDiagnosticCode.InvalidWorkScope,
                "Synthetic invalid Phase 4 fixture.");
            return new WorkScopeIsolationBoundaryProjection(
                WorkScopeIsolationBoundaryProjectionStatus.Invalid, [], [], [diagnostic]);
        });
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);

        Assert.Equal(WorkScopeHandoffStatus.PreparationFailed, result.Status);
        Assert.False(result.CanExecute);
        Assert.Equal(WorkScopeHandoffDiagnosticCode.ProjectionInvalid, result.Diagnostic!.Code);
        Assert.Equal(1, projection.Calls);
        Assert.Equal(0, analyzer.Calls);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);
    }

    [Fact]
    public void Prepare_ReviewedCandidateMismatchDoesNotCallPhase4OrAnalyzer()
    {
        Fixture current = CreateFixture();
        Fixture other = CreateFixture();
        var projection = new CountingProjectionService(new WorkScopeIsolationBoundaryProjector());
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(current.Drawing, current.State, WorkScopeAnalysisFixture.ScenarioFor(current.State),
            other.Candidate, new WorkTicketDataRoot(current.Drawing.Id));

        Assert.Equal(WorkScopeHandoffStatus.ConfirmationRejected, result.Status);
        Assert.Equal(WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch,
            result.ConfirmationDiagnostic!.Code);
        Assert.Equal(0, projection.Calls);
        Assert.Equal(0, analyzer.Calls);
        Assert.Empty(current.Drawing.WorkScopes);
    }

    [Fact]
    public void Prepare_DoesNotSubstituteUnselectedCustomerStationForUnavailableSelectedDevice()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "CustomerStation handoff fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Source",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        CustomerStation station = new CustomerStationCreationFactory().Create(StationKind.BoxStation,
            ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        Connection intertie = new(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value, feeder.CableTerminalId, "C1", "10kV");
        drawing.AddConnection(intertie);
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), seedSwitch.Id, EnergizationSide.Bus)], true));
        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;
        Assert.DoesNotContain(candidate.Boundaries, item => item.SwitchDeviceId == feeder.IsolationSwitch.Id);
        Assert.Equal(seedSwitch.Id, Assert.Single(candidate.SelectedSeeds).BoundaryDeviceId);

        WorkTicketSession oldTicket = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [new IsolationBoundary(seedSwitch.Id, BoundarySide.Bus)],
            Analysis = new WorkTicketAnalysis([], [], [], [], [], ["old analysis"]),
            Draft = new WorkTicketDraft([new SectionDraft("6.1",
                [new DraftItem(Guid.NewGuid(), "old", "user edit", DraftOrigin.UserEdited, [])
                    { IsUserEdited = true, Source = FactOrigin.UserAdded }], SectionCompletion.Completed)]),
            AnalyzedFingerprint = "old fingerprint",
            RulePackVersion = "old rules",
            PhraseLibraryVersion = "old phrases",
            UserFacts = [new UserTicketFact("note", "keep", [], true)],
            GroundingPointIds = [Guid.NewGuid()],
            WorkScopeItems = [new WorkScopeItem(WorkScopeItemKind.Equipment, seedSwitch.Id)]
        };
        var tickets = new WorkTicketDataRoot(drawing.Id, [oldTicket]);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var projection = new CountingProjectionService(new WorkScopeIsolationBoundaryProjector());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets, oldTicket.Id);

        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, result.Status);
        Assert.True(result.CanExecute);
        Assert.Equal(1, projection.Calls);
        Assert.Equal(0, analyzer.Calls);
        Assert.Contains(result.Projection!.Diagnostics, item =>
            item.Code == WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedSelectedWorkSide);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Equal(plan.AfterWorkScope.WorkScopeId, Assert.Single(plan.AfterTicket.WorkScopeIds));
        Assert.Empty(plan.AfterTicket.IsolationBoundaries);
        Assert.Null(plan.AfterTicket.Analysis);
        Assert.Null(plan.AfterTicket.AnalyzedFingerprint);
        Assert.Null(plan.AfterTicket.RulePackVersion);
        Assert.Null(plan.AfterTicket.PhraseLibraryVersion);
        Assert.Equal(SectionCompletion.Stale, plan.AfterTicket.Draft!.Section("6.1").Completion);
        Assert.Equal(oldTicket.Draft!.Section("6.1").Items, plan.AfterTicket.Draft.Section("6.1").Items);
        Assert.Equal(oldTicket.UserFacts, plan.AfterTicket.UserFacts);
        Assert.Equal(oldTicket.GroundingPointIds, plan.AfterTicket.GroundingPointIds);
        Assert.Equal(oldTicket.WorkScopeItems, plan.AfterTicket.WorkScopeItems);
    }

    [Fact]
    public void Prepare_WholeSetWtaRejectionConfirmsScopeButNeverHandsOffPartialBoundaries()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var projection = new CountingProjectionService((drawing, scope) =>
        {
            WorkScopeBoundary source = Assert.Single(scope.Boundaries);
            IsolationBoundary partial = new(source.DeviceId, source.Side, source.TerminalId, source.ConnectionId);
            var sourceResult = new WorkScopeBoundaryProjection(source,
                WorkScopeBoundaryClassification.A, partial, []);
            var diagnostic = new WorkScopeBoundaryProjectionDiagnostic(
                WorkScopeBoundaryProjectionDiagnosticCode.WtaBoundarySetRejected,
                "The WTA resolver rejected the complete boundary set.", source);
            return new WorkScopeIsolationBoundaryProjection(
                WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable,
                [sourceResult], [new ProjectedIsolationBoundary(partial, [source])], [diagnostic]);
        });
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult result = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);

        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, result.Status);
        Assert.True(result.CanExecute);
        Assert.Equal(1, projection.Calls);
        Assert.Equal(0, analyzer.Calls);
        Assert.Contains(result.Projection!.Diagnostics, item =>
            item.Code == WorkScopeBoundaryProjectionDiagnosticCode.WtaBoundarySetRejected);
        Assert.Empty(result.Plan!.AfterTicket.IsolationBoundaries);
        Assert.Null(result.Plan.AfterTicket.Analysis);
    }

    [Fact]
    public void Prepare_ReconfirmCompleteReplacesOldDerivedBoundariesAndAnalyzesOncePerAction()
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(),
            new CountingProjectionService(new WorkScopeIsolationBoundaryProjector()), analyzer);
        WorkScopeHandoffPlanningResult first = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);
        WorkScopeConfirmationPlan firstPlan = Assert.IsType<WorkScopeConfirmationPlan>(first.Plan);
        fixture.Drawing.AddWorkScope(firstPlan.AfterWorkScope);
        tickets.Add(firstPlan.AfterTicket);
        IsolationBoundary stale = new(Guid.NewGuid(), BoundarySide.Line);
        WorkTicketSession beforeSecond = firstPlan.AfterTicket with { IsolationBoundaries = [stale] };
        tickets.Replace(beforeSecond);

        WorkScopeHandoffPlanningResult second = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets, beforeSecond.Id);

        Assert.Equal(WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput, second.Status);
        Assert.Equal(2, analyzer.Calls);
        WorkScopeConfirmationPlan secondPlan = Assert.IsType<WorkScopeConfirmationPlan>(second.Plan);
        Assert.Equal(firstPlan.AfterWorkScope.WorkScopeId, secondPlan.AfterWorkScope.WorkScopeId);
        Assert.NotSame(firstPlan.AfterTicket.Analysis, secondPlan.AfterTicket.Analysis);
        Assert.NotSame(firstPlan.AfterTicket.Draft, secondPlan.AfterTicket.Draft);
        Assert.Equal(second.Projection!.IsolationBoundaries, secondPlan.AfterTicket.IsolationBoundaries);
        Assert.DoesNotContain(stale, secondPlan.AfterTicket.IsolationBoundaries);
        Assert.Equal(beforeSecond.UserFacts, secondPlan.AfterTicket.UserFacts);
        Assert.Equal(beforeSecond.GroundingPointIds, secondPlan.AfterTicket.GroundingPointIds);
        Assert.Equal(beforeSecond.WorkScopeItems, secondPlan.AfterTicket.WorkScopeItems);
        Assert.Equal(secondPlan.AfterTicket.IsolationBoundaries, analyzer.LastInput!.IsolationBoundaries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prepare_ReconfirmRepresentabilityTransitionsInvalidateOrRebuildDerivedSnapshot(bool startsUnrepresentable)
    {
        Fixture fixture = CreateFixture();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        var analyzer = new CountingAnalyzer(new WorkTicketAnalyzer());
        int projectionCalls = 0;
        var projection = new CountingProjectionService((drawing, scope) =>
            startsUnrepresentable == (projectionCalls++ == 0)
                ? Unrepresentable(scope)
                : new WorkScopeIsolationBoundaryProjector().Project(drawing, scope));
        var planner = new WorkScopeHandoffPlanner(new WorkScopeConfirmationPlanner(), projection, analyzer);

        WorkScopeHandoffPlanningResult first = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets);
        WorkScopeConfirmationPlan firstPlan = Assert.IsType<WorkScopeConfirmationPlan>(first.Plan);
        fixture.Drawing.AddWorkScope(firstPlan.AfterWorkScope);
        tickets.Add(firstPlan.AfterTicket);
        WorkScopeHandoffPlanningResult second = planner.Prepare(fixture.Drawing, fixture.State, WorkScopeAnalysisFixture.ScenarioFor(fixture.State),
            fixture.Candidate, tickets, firstPlan.TicketId);

        WorkScopeHandoffPlanningResult unrepresentable = startsUnrepresentable ? first : second;
        WorkScopeHandoffPlanningResult complete = startsUnrepresentable ? second : first;
        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, unrepresentable.Status);
        Assert.Empty(unrepresentable.Plan!.AfterTicket.IsolationBoundaries);
        Assert.Null(unrepresentable.Plan.AfterTicket.Analysis);
        Assert.Equal(WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput, complete.Status);
        Assert.NotEmpty(complete.Plan!.AfterTicket.IsolationBoundaries);
        Assert.NotNull(complete.Plan.AfterTicket.Analysis);
        Assert.Equal(1, analyzer.Calls);
    }

    private static Fixture CreateFixture(bool withOpenBoundary = true)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "WorkScope handoff fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Handoff ring",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                withOpenBoundary ? SwitchState.Open : SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        if (!withOpenBoundary)
        {
            SwitchDevice isolated = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
                Guid.NewGuid(), Guid.NewGuid());
            drawing.AddDevice(isolated);
            drawing.AddTerminal(new Terminal(isolated.FirstTerminalId, TopologyOwnerType.Device,
                isolated.Id, "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
            drawing.AddTerminal(new Terminal(isolated.SecondTerminalId, TopologyOwnerType.Device,
                isolated.Id, "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        }
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices[0];
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), seedSwitch.Id, EnergizationSide.Bus)], true));
        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;
        return new Fixture(drawing, state, candidate);
    }

    private sealed record Fixture(
        DrawingDocument Drawing,
        EnergizationAnalysisState State,
        WorkScopeCandidate Candidate);

    private sealed class CountingProjectionService(IWorkScopeIsolationBoundaryProjectionService inner)
        : IWorkScopeIsolationBoundaryProjectionService
    {
        private readonly Func<DrawingDocument, WorkScope, WorkScopeIsolationBoundaryProjection> _project =
            inner.Project;

        public CountingProjectionService(
            Func<DrawingDocument, WorkScope, WorkScopeIsolationBoundaryProjection> project) : this(
            new WorkScopeIsolationBoundaryProjector())
        {
            _project = project;
        }

        public int Calls { get; private set; }
        public WorkScopeIsolationBoundaryProjection Project(DrawingDocument drawing, WorkScope confirmedSnapshot)
        {
            Calls++;
            return _project(drawing, confirmedSnapshot);
        }
    }

    private static WorkScopeIsolationBoundaryProjection Unrepresentable(WorkScope scope)
    {
        WorkScopeBoundary[] boundaries = scope.Boundaries.ToArray();
        WorkScopeBoundaryProjection[] perBoundary = boundaries.Select(source =>
            new WorkScopeBoundaryProjection(source, WorkScopeBoundaryClassification.C, null,
                [new WorkScopeBoundaryProjectionDiagnostic(
                    WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedCustomerStationBoundary,
                    "Synthetic Phase 4 unrepresentable fixture.", source)])).ToArray();
        WorkScopeBoundaryProjectionDiagnostic[] diagnostics = perBoundary.SelectMany(item => item.Diagnostics).ToArray();
        return new WorkScopeIsolationBoundaryProjection(
            WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable,
            perBoundary, [], diagnostics);
    }

    private sealed class CountingAnalyzer : IWorkTicketHandoffAnalyzer
    {
        private readonly Func<DrawingDocument, WorkTicketSession, WorkTicketSession> _analyze;

        public CountingAnalyzer(WorkTicketAnalyzer inner)
        {
            _analyze = inner.AnalyzeConfirmedWorkScopeHandoff;
        }

        public CountingAnalyzer(Func<DrawingDocument, WorkTicketSession, WorkTicketSession> analyze)
        {
            _analyze = analyze;
        }

        public int Calls { get; private set; }
        public WorkTicketSession? LastInput { get; private set; }

        public WorkTicketSession AnalyzeConfirmedWorkScopeHandoff(DrawingDocument drawing, WorkTicketSession ticket)
        {
            Calls++;
            LastInput = ticket;
            return _analyze(drawing, ticket);
        }
    }
}
