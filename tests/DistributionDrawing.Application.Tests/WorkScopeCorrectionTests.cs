using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Energization;
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

    private static WorkScopeHandoffPlanner Planner(CountingCorrectionAnalyzer analyzer) =>
        new(new WorkScopeConfirmationPlanner(), new WorkScopeIsolationBoundaryProjector(), analyzer);

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
