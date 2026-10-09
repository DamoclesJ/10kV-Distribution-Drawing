using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.TestSupport;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkScopeCorrectionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Apply_UsesOnlySelectedSeedsDespiteTwentyFourGraphTransitions(int selectedCount)
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring(selectedCount);
        EnergizationResult ea = Assert.IsType<EnergizationResult>(fixture.State.CurrentResult);
        Assert.Equal(24, fixture.Drawing.Devices.OfType<SwitchDevice>().Count(device =>
            device.SwitchKind != SwitchKind.GroundSwitch &&
            ea.Terminals[device.FirstTerminalId].State != ea.Terminals[device.SecondTerminalId].State));
        WorkScopeCandidate candidate = new WorkScopeCandidateProjector()
            .Project(fixture.Drawing, fixture.State, fixture.Scenario).Candidate!;
        Assert.Equal(fixture.Scenario.Seeds.Select(seed => seed.BoundaryDeviceId).Order(),
            candidate.Boundaries.Select(boundary => boundary.SwitchDeviceId).Order());
        var analyzer = new CountingCorrectionAnalyzer();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        WorkScopeHandoffPlanningResult result = Planner(analyzer).PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, tickets, task: new("检修", "测试地点"));
        Assert.True(result.CanExecute, result.ConfirmationDiagnostic?.Message);
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Guid[] selected = fixture.Scenario.Seeds.Select(seed => seed.BoundaryDeviceId).Order().ToArray();
        Assert.Equal(selected, plan.AfterWorkScope.Boundaries.Select(boundary => boundary.DeviceId).Order());
        Assert.Equal(selected, plan.AfterTicket.IsolationBoundaries.Select(boundary => boundary.DeviceId).Order());
        Assert.All(plan.AfterWorkScope.Boundaries, boundary => Assert.Equal(BoundarySide.Line, boundary.Side));
        Assert.All(fixture.Scenario.Seeds, seed => Assert.Equal(EnergizationSide.Bus, seed.Side));
        AssertDeenergizedMembership(fixture, plan.AfterWorkScope);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal([plan.AfterWorkScope.WorkScopeId], plan.AfterTicket.WorkScopeIds);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);
    }

    [Fact]
    public void Apply_IncludesDeenergizedNodeEvenWhenItHasNoTerminal()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring();
        var isolatedNode = new ElectricalNode(Guid.NewGuid(), ElectricalNodeType.Circuit,
            TopologyOwnerType.Device, fixture.Scenario.Seeds[0].BoundaryDeviceId);
        fixture.Drawing.AddElectricalNode(isolatedNode);
        fixture.State.Execute(fixture.Drawing, fixture.Scenario);
        WorkScopeHandoffPlanningResult result = new WorkScopeHandoffPlanner().PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, new(fixture.Drawing.Id));
        Assert.True(result.CanExecute);
        Assert.Contains(isolatedNode.Id, result.Plan!.AfterWorkScope.Regions.SelectMany(region => region.ElectricalNodeIds));
        AssertDeenergizedMembership(fixture, result.Plan.AfterWorkScope);
    }

    [Fact]
    public void Apply_RejectsStaleEaAndUsesLatestSelectionAndResultAfterReanalysis()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring();
        WorkScopeCandidate previous = new WorkScopeCandidateProjector()
            .Project(fixture.Drawing, fixture.State, fixture.Scenario).Candidate!;
        foreach (EnergizedSeed seed in fixture.Scenario.Seeds.ToArray())
            fixture.Scenario.ReplaceSeed(new(seed.Id, seed.BoundaryDeviceId, EnergizationSide.Line));
        fixture.State.Invalidate();
        var analyzer = new CountingCorrectionAnalyzer();
        var tickets = new WorkTicketDataRoot(fixture.Drawing.Id);
        WorkScopeHandoffPlanner planner = Planner(analyzer);
        WorkScopeHandoffPlanningResult rejected = planner.PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, tickets);
        Assert.False(rejected.CanExecute);
        Assert.Contains(rejected.ConfirmationDiagnostic!.CandidateDiagnostics,
            diagnostic => diagnostic.Code == WorkScopeCandidateDiagnosticCode.StaleAnalysis);
        Assert.Equal(0, analyzer.Calls);
        Assert.Empty(fixture.Drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);

        fixture.State.Execute(fixture.Drawing, fixture.Scenario);
        Assert.Equal(WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch,
            planner.Prepare(fixture.Drawing, fixture.State, fixture.Scenario, previous, tickets)
                .ConfirmationDiagnostic!.Code);
        WorkScopeHandoffPlanningResult latest = planner.PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, tickets);
        Assert.True(latest.CanExecute);
        AssertDeenergizedMembership(fixture, latest.Plan!.AfterWorkScope);
        Assert.All(latest.Plan.AfterTicket.IsolationBoundaries, boundary => Assert.Equal(BoundarySide.Bus, boundary.Side));
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public void Apply_UnavailableSelectedWorkSideDoesNotSubstituteTwentyTwoOtherTransitions()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.Ring(selectedClosed: true);
        var analyzer = new CountingCorrectionAnalyzer();
        WorkScopeHandoffPlanningResult result = Planner(analyzer).PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, new(fixture.Drawing.Id));
        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, result.Status);
        Assert.True(result.CanExecute);
        Assert.Equal(fixture.Scenario.Seeds.Select(seed => seed.BoundaryDeviceId).Order(),
            result.Plan!.AfterWorkScope.Boundaries.Select(boundary => boundary.DeviceId).Order());
        Assert.All(result.Plan.AfterWorkScope.Boundaries, boundary => Assert.Equal(BoundarySide.Unknown, boundary.Side));
        Assert.Contains(result.Projection!.Diagnostics, diagnostic =>
            diagnostic.Code == WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedSelectedWorkSide);
        Assert.Empty(result.Plan.AfterTicket.IsolationBoundaries);
        Assert.Null(result.Plan.AfterTicket.Analysis);
        Assert.Equal(0, analyzer.Calls);
        AssertDeenergizedMembership(fixture, result.Plan.AfterWorkScope);
    }

    [Fact]
    public void Apply_OriginalBoundaryMaterializationAmbiguityNowConfirmsWithHandoffDiagnostic()
    {
        WorkScopeCorrectionFixture fixture = WorkScopeCorrectionFixture.PoleWithoutDownstreamConnection();
        var analyzer = new CountingCorrectionAnalyzer();
        WorkScopeHandoffPlanningResult result = Planner(analyzer).PrepareFromAnalysis(
            fixture.Drawing, fixture.State, fixture.Scenario, new(fixture.Drawing.Id));
        Assert.Equal(EnergizationValidity.Complete, fixture.State.CurrentResult!.Validity);
        Assert.True(result.CanExecute, result.ConfirmationDiagnostic?.Message);
        Assert.Null(result.ConfirmationDiagnostic);
        Assert.Equal(WorkScopeHandoffStatus.ConfirmedButUnrepresentable, result.Status);
        WorkScopeBoundary boundary = Assert.Single(result.Plan!.AfterWorkScope.Boundaries);
        Assert.Equal(fixture.Scenario.Seeds[0].BoundaryDeviceId, boundary.DeviceId);
        Assert.Equal(BoundarySide.Unknown, boundary.Side);
        Assert.NotNull(boundary.TerminalId);
        Assert.Empty(result.Plan.AfterTicket.IsolationBoundaries);
        Assert.Equal(0, analyzer.Calls);
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding, SwitchKind.IsolationSwitch)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding, SwitchKind.CircuitBreaker)]
    public void Apply_IntegratedFeederSelectedSwitchMapsEaDeenergizedTerminalThroughWtaResolver(
        GroundingStructureKind groundingStructure,
        SwitchKind selectedKind)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Integrated feeder EA work scope");
        SwitchState isolationState = groundingStructure == GroundingStructureKind.LowerLowerGrounding
            ? SwitchState.Closed : SwitchState.Open;
        SwitchState breakerState = groundingStructure == GroundingStructureKind.LowerLowerGrounding
            ? SwitchState.Open : SwitchState.Closed;
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "测试环网柜",
            [RingCabinetIntervalDefinition.CreateIntegratedFeeder(1, groundingStructure,
                isolationState, breakerState, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice selected = cabinet.Intervals.Single(interval =>
            interval.IntervalKind == IntervalKind.IntegratedFeederInterval)
            .SwitchDevices.Single(item => item.SwitchKind == selectedKind);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), selected.Id, EnergizationSide.Bus)]);
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        EnergizationResult result = Assert.IsType<EnergizationResult>(state.CurrentResult);
        Assert.Equal(EnergizationState.Energized, result.Terminals[selected.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, result.Terminals[selected.SecondTerminalId].State);

        var analyzer = new CountingCorrectionAnalyzer();
        WorkScopeHandoffPlanningResult handoff = Planner(analyzer).PrepareFromAnalysis(
            drawing, state, scenario, new WorkTicketDataRoot(drawing.Id));

        Assert.Contains(handoff.Status, new[]
        {
            WorkScopeHandoffStatus.ConfirmedAndAnalyzed,
            WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput
        });
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(handoff.Plan);
        WorkScopeBoundary boundary = Assert.Single(plan.AfterWorkScope.Boundaries);
        Assert.Equal(selected.Id, boundary.DeviceId);
        Assert.Equal(BoundarySide.Line, boundary.Side);
        Assert.Equal(selected.SecondTerminalId, boundary.TerminalId);
        IsolationBoundary isolation = Assert.Single(plan.AfterTicket.IsolationBoundaries);
        Assert.Equal(selected.Id, isolation.DeviceId);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, selected.Id, isolation.Side,
            out IsolationBoundary? resolved, out string issue), issue);
        Assert.Equal(resolved, isolation);
        WorkTicketRangeSetup.ValidateBoundaries(drawing, [isolation]);
        Assert.Equal(1, analyzer.Calls);
    }

    [Theory]
    [InlineData(EnergizationSide.SmallerNumber, BoundarySide.LargerNumber)]
    [InlineData(EnergizationSide.LargerNumber, BoundarySide.SmallerNumber)]
    public void Apply_PoleSeedDirectionMapsByDeenergizedTerminalAndActualConnection(
        EnergizationSide seedSide,
        BoundarySide expectedWorkSide)
    {
        (DrawingDocument drawing, SwitchDevice selected, EnergizationAnalysisState state,
            EnergizationScenario scenario, Guid expectedConnection) = PoleFixture(seedSide);
        var analyzer = new CountingCorrectionAnalyzer();

        WorkScopeHandoffPlanningResult handoff = Planner(analyzer).PrepareFromAnalysis(
            drawing, state, scenario, new WorkTicketDataRoot(drawing.Id));

        Assert.Contains(handoff.Status, new[]
        {
            WorkScopeHandoffStatus.ConfirmedAndAnalyzed,
            WorkScopeHandoffStatus.ConfirmedHandoffNeedsInput
        });
        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(handoff.Plan);
        WorkScopeBoundary workBoundary = Assert.Single(plan.AfterWorkScope.Boundaries);
        IsolationBoundary isolation = Assert.Single(plan.AfterTicket.IsolationBoundaries);
        Assert.Equal(expectedWorkSide, workBoundary.Side);
        Assert.Equal(expectedWorkSide, isolation.Side);
        Assert.Equal(workBoundary.TerminalId, isolation.TerminalId);
        Assert.Equal(expectedConnection, isolation.ConnectionId);
        Assert.Equal(selected.Id, isolation.DeviceId);
        WorkTicketRangeSetup.ValidateBoundaries(drawing, [isolation]);
        Assert.Equal(1, analyzer.Calls);
    }

    private static WorkScopeHandoffPlanner Planner(CountingCorrectionAnalyzer analyzer) =>
        new(new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer);

    private static (DrawingDocument Drawing, SwitchDevice Selected, EnergizationAnalysisState State,
        EnergizationScenario Scenario, Guid ExpectedConnection) PoleFixture(EnergizationSide seedSide)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Pole EA work scope");
        Pole smaller = new(Guid.NewGuid(), "P01");
        Pole current = new(Guid.NewGuid(), "P02");
        Pole larger = new(Guid.NewGuid(), "P03");
        drawing.AddDevice(smaller);
        drawing.AddDevice(current);
        drawing.AddDevice(larger);
        Terminal smallerAnchor = smaller.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = larger.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice selected = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.IsolationSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(selected);
        drawing.AddTerminal(new Terminal(selected.FirstTerminalId, TopologyOwnerType.Device,
            selected.Id, "SwitchLeftTerminal", "10kV", true, true, null,
            [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(selected.SecondTerminalId, TopologyOwnerType.Device,
            selected.Id, "SwitchRightTerminal", "10kV", true, true, null,
            [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), current.Id, selected.Id));
        Connection smallerConnection = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            smallerAnchor.Id, selected.FirstTerminalId, "P01-P02", "10kV");
        Connection largerConnection = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            selected.SecondTerminalId, largerAnchor.Id, "P02-P03", "10kV");
        drawing.AddConnection(smallerConnection);
        drawing.AddConnection(largerConnection);
        drawing.AddOverheadLine(new OverheadLine(smallerConnection.Id, "JKLYJ", [smaller.Id, current.Id]));
        drawing.AddOverheadLine(new OverheadLine(largerConnection.Id, "JKLYJ", [current.Id, larger.Id]));
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), selected.Id, seedSide)]);
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        Guid expectedConnection = seedSide == EnergizationSide.SmallerNumber
            ? largerConnection.Id : smallerConnection.Id;
        return (drawing, selected, state, scenario, expectedConnection);
    }

    private static void AssertDeenergizedMembership(WorkScopeCorrectionFixture fixture, WorkScope scope)
    {
        Guid[] earthNodes = fixture.Drawing.ElectricalNodes.Where(node => node.Type == ElectricalNodeType.Earth)
            .Select(node => node.Id).ToArray();
        Guid[] earthTerminals = fixture.Drawing.Terminals.Where(terminal =>
            terminal.ElectricalNodeId is Guid id && earthNodes.Contains(id)).Select(terminal => terminal.Id).ToArray();
        Assert.Equal(fixture.State.CurrentResult!.Terminals.Where(pair =>
                pair.Value.State == EnergizationState.Deenergized && !earthTerminals.Contains(pair.Key))
            .Select(pair => pair.Key).Order(), scope.Regions.SelectMany(region => region.TerminalIds).Order());
        Assert.Equal(fixture.State.CurrentResult.Nodes.Where(pair =>
                pair.Value.State == EnergizationState.Deenergized && !earthNodes.Contains(pair.Key))
            .Select(pair => pair.Key).Order(), scope.Regions.SelectMany(region => region.ElectricalNodeIds).Order());
    }
}
