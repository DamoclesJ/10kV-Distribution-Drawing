using DistributionDrawing.TestSupport;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Energization;
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

public sealed class WorkScopeConfirmationPlannerTests
{
    [Fact]
    public void Prepare_MaterializesReviewedRingCandidateAndRejectsConfirmationDiagnostics()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate candidate = Project(drawing, state);
        var tickets = new WorkTicketDataRoot(drawing.Id);
        Guid ticketId = Guid.NewGuid();
        Guid scopeId = Guid.NewGuid();
        WorkScopeConfirmationPlanningResult result = Planner(ticketId, scopeId)
            .Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets);

        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Null(result.Diagnostic);
        Assert.True(result.CanConfirm);
        Assert.Equal(ticketId, plan.TicketId);
        Assert.Equal(scopeId, plan.AfterWorkScope.WorkScopeId);
        Assert.Null(plan.AfterWorkScope.Description);
        Assert.Single(plan.AfterTicket.WorkScopeIds);
        Assert.Equal(scopeId, plan.AfterTicket.WorkScopeIds[0]);
        Assert.Empty(plan.AfterTicket.IsolationBoundaries);
        Assert.Equal(candidate.Regions.Select(region => string.Join(',', region.TerminalIds) + "/" +
                string.Join(',', region.ElectricalNodeIds)),
            plan.AfterWorkScope.Regions.Select(region => string.Join(',', region.TerminalIds) + "/" +
                string.Join(',', region.ElectricalNodeIds)));
        WorkScopeCandidateBoundary candidateBoundary = Assert.Single(candidate.Boundaries);
        WorkScopeBoundary boundary = Assert.Single(plan.AfterWorkScope.Boundaries);
        Assert.Equal(candidateBoundary.SwitchDeviceId, boundary.DeviceId);
        Assert.Equal(BoundarySide.Line, boundary.Side);
        Assert.Equal(candidateBoundary.DeenergizedTerminalId, boundary.TerminalId);
        Assert.Null(boundary.ConnectionId);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);

        state.Invalidate();
        WorkScopeConfirmationPlanningResult stale = Planner().Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets);
        Assert.Equal(WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable, stale.Diagnostic!.Code);
        Assert.Contains(stale.Diagnostic.CandidateDiagnostics,
            item => item.Code == WorkScopeCandidateDiagnosticCode.StaleAnalysis);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);
    }

    [Fact]
    public void Prepare_RejectsNoCurrentNoSeedAndFailedEa()
    {
        (DrawingDocument drawing, RingCabinet cabinet, _) = RingFixture();
        var tickets = new WorkTicketDataRoot(drawing.Id);
        WorkScopeCandidate reviewed = Project(drawing,
            Analyze(drawing, cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Bus));

        var absent = new EnergizationAnalysisState();
        AssertGate(absent, WorkScopeCandidateDiagnosticCode.CurrentResultUnavailable);

        var noSeed = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(noSeed, drawing, new EnergizationScenario(Guid.NewGuid(), []));
        AssertGate(noSeed, WorkScopeCandidateDiagnosticCode.NoSeeds);

        var failed = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(failed, drawing, new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus)]));
        AssertGate(failed, WorkScopeCandidateDiagnosticCode.FailedAnalysis);

        void AssertGate(EnergizationAnalysisState state, WorkScopeCandidateDiagnosticCode expected)
        {
            WorkScopeConfirmationPlanningResult result = Planner().Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), reviewed, tickets);
            Assert.False(result.CanConfirm);
            Assert.Equal(WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable, result.Diagnostic!.Code);
            Assert.Contains(result.Diagnostic.CandidateDiagnostics, item => item.Code == expected);
            Assert.Empty(drawing.WorkScopes);
            Assert.Empty(tickets.Tickets);
        }
    }

    [Fact]
    public void Prepare_RejectsReviewedCandidateWhenCurrentEaProjectsAnotherSide()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate reviewed = Project(drawing, state);
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Line)], true));
        var tickets = new WorkTicketDataRoot(drawing.Id);

        WorkScopeConfirmationPlanningResult result = Planner().Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), reviewed, tickets);

        Assert.Equal(WorkScopeConfirmationFailureCode.ReviewedCandidateMismatch, result.Diagnostic!.Code);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);
    }

    [Fact]
    public void Prepare_RejectsBlockingCurrentProjectorDiagnostic()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate reviewed = Project(drawing, state);
        AddIsolatedOpenSwitch(drawing);
        var tickets = new WorkTicketDataRoot(drawing.Id);

        WorkScopeConfirmationPlanningResult result = Planner().Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), reviewed, tickets);

        Assert.Equal(WorkScopeConfirmationFailureCode.CurrentCandidateUnavailable, result.Diagnostic!.Code);
        Assert.Contains(result.Diagnostic.CandidateDiagnostics,
            item => item.Code == WorkScopeCandidateDiagnosticCode.IdentityMismatch);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(tickets.Tickets);
    }

    [Fact]
    public void Prepare_RejectsEmptyCandidateWithoutChangingTicketOrDrawing()
    {
        (DrawingDocument drawing, RingCabinet cabinet, _) = RingFixture(SwitchState.Closed);
        EnergizationAnalysisState state = Analyze(drawing,
            cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Bus);
        WorkScopeCandidate candidate = Project(drawing, state);
        var tickets = new WorkTicketDataRoot(drawing.Id);
        WorkTicketSession ticket = WorkTicketSession.Create();
        tickets.Add(ticket);

        WorkScopeConfirmationPlanningResult result = Planner().Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets, ticket.Id);

        Assert.Equal(WorkScopeConfirmationFailureCode.EmptyCandidate, result.Diagnostic!.Code);
        Assert.Single(tickets.Tickets);
        Assert.Same(ticket, tickets.Selected(ticket.Id));
        Assert.Empty(drawing.WorkScopes);
    }

    [Fact]
    public void Prepare_PreservesSelectedDeviceWithoutTransitionAndMapsMultipleSelectedBoundaries()
    {
        (DrawingDocument noBoundaryDrawing, RingCabinet closedCabinet, _) = RingFixture(SwitchState.Closed);
        SwitchDevice isolated = AddIsolatedOpenSwitch(noBoundaryDrawing);
        EnergizationAnalysisState noBoundaryState = Analyze(noBoundaryDrawing,
            closedCabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Bus);
        WorkScopeCandidate noBoundary = Project(noBoundaryDrawing, noBoundaryState);
        Assert.NotEmpty(noBoundary.Regions);
        Assert.Empty(noBoundary.Boundaries);
        WorkScopeConfirmationPlanningResult noBoundaryPlan = Planner(Guid.NewGuid())
            .Prepare(noBoundaryDrawing, noBoundaryState, WorkScopeAnalysisFixture.ScenarioFor(noBoundaryState), noBoundary,
                new WorkTicketDataRoot(noBoundaryDrawing.Id));
        Assert.True(noBoundaryPlan.CanConfirm);
        WorkScopeBoundary unavailable = Assert.Single(noBoundaryPlan.Plan!.AfterWorkScope.Boundaries);
        Assert.Equal(closedCabinet.Intervals[0].SwitchDevices[0].Id, unavailable.DeviceId);
        Assert.Equal(BoundarySide.Unknown, unavailable.Side);
        Assert.Equal(RegionSignature(noBoundary), RegionSignature(noBoundaryPlan.Plan.AfterWorkScope));
        Assert.Contains(noBoundary.Regions.SelectMany(item => item.TerminalIds),
            id => id == isolated.FirstTerminalId || id == isolated.SecondTerminalId);

        (DrawingDocument multipleDrawing, RingCabinet cabinet, EnergizationAnalysisState multipleState) =
            MultiBoundaryFixture();
        WorkScopeCandidate multiple = Project(multipleDrawing, multipleState);
        Assert.Single(multiple.Regions);
        Assert.Equal(2, multiple.Boundaries.Count);
        WorkScopeConfirmationPlanningResult multiplePlan = Planner(Guid.NewGuid())
            .Prepare(multipleDrawing, multipleState, WorkScopeAnalysisFixture.ScenarioFor(multipleState), multiple,
                new WorkTicketDataRoot(multipleDrawing.Id));
        Assert.True(multiplePlan.CanConfirm);
        Assert.Equal(2, multiplePlan.Plan!.AfterWorkScope.Boundaries.Count);
        Assert.Equal(RegionSignature(multiple), RegionSignature(multiplePlan.Plan.AfterWorkScope));
        Assert.Equal(cabinet.Intervals.Select(interval => interval.SwitchDevices[0].Id).Order(),
            multiplePlan.Plan.AfterWorkScope.Boundaries.Select(item => item.DeviceId).Order());
        foreach (WorkScopeCandidateBoundary candidateBoundary in multiple.Boundaries)
        {
            WorkScopeBoundary materialized = Assert.Single(multiplePlan.Plan.AfterWorkScope.Boundaries,
                item => item.DeviceId == candidateBoundary.SwitchDeviceId);
            Terminal deenergizedTerminal = multipleDrawing.Terminals.Single(item =>
                item.Id == candidateBoundary.DeenergizedTerminalId);
            Assert.Equal(deenergizedTerminal.Role == "BusSide" ? BoundarySide.Bus : BoundarySide.Line,
                materialized.Side);
            Assert.Equal(candidateBoundary.DeenergizedTerminalId, materialized.TerminalId);
            Assert.Null(materialized.ConnectionId);
        }
    }

    [Fact]
    public void Prepare_MaterializesEveryDisconnectedCandidateRegionWithoutRecalculation()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Disconnected candidate fixture");
        RingCabinet[] cabinets = Enumerable.Range(1, 2).Select(index => RingCabinet.Create(
            RingCabinetDefinition.Create(Guid.NewGuid(), $"Region {index}",
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)])))
            .ToArray();
        foreach (RingCabinet cabinet in cabinets) drawing.AddDevice(cabinet);
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(), cabinets.Select(cabinet =>
            Seed(cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Bus)).ToArray(), true));
        WorkScopeCandidate candidate = Project(drawing, state);
        Assert.Equal(2, candidate.Regions.Count);

        WorkScopeConfirmationPlanningResult result = Planner(Guid.NewGuid()).Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, new WorkTicketDataRoot(drawing.Id));

        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Equal(RegionSignature(candidate), RegionSignature(plan.AfterWorkScope));
        Assert.Equal(2, plan.AfterWorkScope.Regions.Count);
    }

    [Fact]
    public void Prepare_MapsPoleDeenergizedConnectionSideWithoutUsingWtaResolver()
    {
        (DrawingDocument drawing, SwitchDevice device) = PoleFixture();
        EnergizationAnalysisState state = Analyze(drawing, device, EnergizationSide.SmallerNumber);
        WorkScopeCandidate candidate = Project(drawing, state);
        WorkScopeCandidateBoundary candidateBoundary = Assert.Single(candidate.Boundaries);

        WorkScopeConfirmationPlanningResult result = Planner(Guid.NewGuid()).Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, new WorkTicketDataRoot(drawing.Id));

        WorkScopeBoundary boundary = Assert.Single(Assert.IsType<WorkScopeConfirmationPlan>(result.Plan)
            .AfterWorkScope.Boundaries);
        Assert.Equal(BoundarySide.LargerNumber, boundary.Side);
        Assert.Equal(candidateBoundary.DeenergizedTerminalId, boundary.TerminalId);
        Assert.Contains(candidateBoundary.RelatedConnectionIds, id => id == boundary.ConnectionId);
    }

    [Fact]
    public void Prepare_PreservesCustomerStationMembershipWithoutAddingItsUnselectedSwitch()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) =
            RingFixture(SwitchState.Closed);
        CustomerStation station = new CustomerStationCreationFactory().Create(StationKind.BoxStation, ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        Connection cable = new(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value, feeder.CableTerminalId, "Feeder A", "10kV");
        drawing.AddConnection(cable);
        state = Analyze(drawing, cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Bus);
        WorkScopeCandidate candidate = Project(drawing, state);

        WorkScopeConfirmationPlanningResult result = Planner(Guid.NewGuid()).Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, new WorkTicketDataRoot(drawing.Id));

        Assert.True(result.CanConfirm, result.Diagnostic?.Message);
        Assert.DoesNotContain(result.Plan!.AfterWorkScope.Boundaries,
            item => item.DeviceId == feeder.IsolationSwitch.Id);
        Assert.Equal(cabinet.Intervals[0].SwitchDevices[0].Id,
            Assert.Single(result.Plan.AfterWorkScope.Boundaries).DeviceId);
        Assert.DoesNotContain(result.Plan.AfterWorkScope.Regions.SelectMany(item => item.TerminalIds),
            id => id == feeder.CableTerminalId);
        Assert.Contains(result.Plan.AfterWorkScope.Regions.SelectMany(item => item.TerminalIds),
            id => id == feeder.StationTerminalId);
    }

    [Fact]
    public void Prepare_PreservesDescriptionAndRejectsAmbiguousTicketScopeStates()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate first = Project(drawing, state);
        Guid oldId = Guid.NewGuid();
        WorkScope oldScope = WorkScope.Create(oldId,
            first.Regions.Select(item => new WorkScopeRegion(item.TerminalIds, item.ElectricalNodeIds)),
            first.Boundaries.Select(item => new WorkScopeBoundary(item.SwitchDeviceId, BoundarySide.Line,
                item.DeenergizedTerminalId)), "保留的描述");
        drawing.AddWorkScope(oldScope);
        WorkTicketSession ticket = WorkTicketSession.Create() with { WorkScopeIds = [oldId] };
        var tickets = new WorkTicketDataRoot(drawing.Id, [ticket]);

        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(cabinet.Intervals[0].SwitchDevices[0], EnergizationSide.Line)], true));
        WorkScopeCandidate second = Project(drawing, state);
        WorkScopeConfirmationPlanningResult exclusive = Planner(Guid.NewGuid())
            .Prepare(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), second, tickets, ticket.Id);
        Assert.True(exclusive.CanConfirm, exclusive.Diagnostic?.Message);
        Assert.Equal(oldId, exclusive.Plan!.AfterWorkScope.WorkScopeId);
        Assert.Equal("保留的描述", exclusive.Plan.AfterWorkScope.Description);
        Assert.Equal(exclusive.Plan.AfterWorkScope.Regions.Select(item => string.Join(',', item.TerminalIds)),
            second.Regions.Select(item => string.Join(',', item.TerminalIds)));

        WorkTicketSession multi = ticket with { WorkScopeIds = [oldId, Guid.NewGuid()] };
        var multiTickets = new WorkTicketDataRoot(drawing.Id, [multi]);
        WorkScopeConfirmationPlanningResult multiple = Planner().Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), second, multiTickets, multi.Id);
        Assert.Equal(WorkScopeConfirmationFailureCode.MultipleWorkScopes, multiple.Diagnostic!.Code);

        WorkTicketSession legacy = ticket with
        {
            WorkScopeItems = [new WorkScopeItem(WorkScopeItemKind.ElectricalRange, oldId)]
        };
        var legacyTickets = new WorkTicketDataRoot(drawing.Id, [legacy]);
        WorkScopeConfirmationPlanningResult ambiguous = Planner().Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), second, legacyTickets, legacy.Id);
        Assert.True(ambiguous.CanConfirm, ambiguous.Diagnostic?.Message);
        Assert.Equal(legacy.WorkScopeItems, ambiguous.Plan!.AfterTicket.WorkScopeItems);
        Assert.Same(oldScope, drawing.GetWorkScope(oldId));
    }

    [Fact]
    public void Prepare_RejectsUnknownTicketAndMismatchedTicketRoot()
    {
        (DrawingDocument drawing, _, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate candidate = Project(drawing, state);
        WorkScopeConfirmationPlanningResult missing = Planner().Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, new WorkTicketDataRoot(drawing.Id), Guid.NewGuid());
        Assert.Equal(WorkScopeConfirmationFailureCode.MissingTargetTicket, missing.Diagnostic!.Code);
        WorkScopeConfirmationPlanningResult mismatched = Planner().Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, new WorkTicketDataRoot(Guid.NewGuid()));
        Assert.Equal(WorkScopeConfirmationFailureCode.MissingTargetTicket, mismatched.Diagnostic!.Code);
    }

    [Fact]
    public void Prepare_SkipsIdentifiersOwnedByNestedSwitchDevices()
    {
        (DrawingDocument drawing, RingCabinet cabinet, EnergizationAnalysisState state) = RingFixture();
        WorkScopeCandidate candidate = Project(drawing, state);
        WorkTicketSession ticket = WorkTicketSession.Create();
        var tickets = new WorkTicketDataRoot(drawing.Id, [ticket]);
        Guid scopeId = Guid.NewGuid();
        Guid[] generated = [cabinet.Intervals[0].SwitchDevices[0].Id, scopeId];
        int index = 0;
        var planner = new WorkScopeConfirmationPlanner(() => generated[index++]);

        WorkScopeConfirmationPlanningResult result = planner.Prepare(
            drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state), candidate, tickets, ticket.Id);

        WorkScopeConfirmationPlan plan = Assert.IsType<WorkScopeConfirmationPlan>(result.Plan);
        Assert.Equal(scopeId, plan.AfterWorkScope.WorkScopeId);
        Assert.NotEqual(generated[0], plan.AfterWorkScope.WorkScopeId);
    }

    private static WorkScopeConfirmationPlanner Planner(params Guid[] ids)
    {
        int index = 0;
        return new WorkScopeConfirmationPlanner(() => index < ids.Length ? ids[index++] : Guid.NewGuid());
    }

    private static WorkScopeCandidate Project(DrawingDocument drawing, EnergizationAnalysisState state) =>
        new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

    private static string[] RegionSignature(WorkScopeCandidate candidate) => candidate.Regions
        .Select(region => string.Join(',', region.TerminalIds.Order()) + "/" +
            string.Join(',', region.ElectricalNodeIds.Order()))
        .Order(StringComparer.Ordinal).ToArray();

    private static string[] RegionSignature(WorkScope scope) => scope.Regions
        .Select(region => string.Join(',', region.TerminalIds.Order()) + "/" +
            string.Join(',', region.ElectricalNodeIds.Order()))
        .Order(StringComparer.Ordinal).ToArray();

    private static (DrawingDocument Drawing, RingCabinet Cabinet, EnergizationAnalysisState State) RingFixture(
        SwitchState firstSwitchState = SwitchState.Open)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Confirm ring fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Confirm ring",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, firstSwitchState, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice seedSwitch = cabinet.Intervals[0].SwitchDevices[0];
        return (drawing, cabinet, Analyze(drawing, seedSwitch, EnergizationSide.Bus));
    }

    private static (DrawingDocument Drawing, RingCabinet Cabinet, EnergizationAnalysisState State)
        MultiBoundaryFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Multiple boundary fixture");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Two boundaries",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        EnergizedSeed[] seeds = cabinet.Intervals.Select(interval => Seed(interval.SwitchDevices[0],
            EnergizationSide.Line)).ToArray();
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(), seeds, true));
        return (drawing, cabinet, state);
    }

    private static (DrawingDocument Drawing, SwitchDevice Switch) PoleFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Confirm pole fixture");
        Pole smaller = new(Guid.NewGuid(), "P01");
        Pole attached = new(Guid.NewGuid(), "P02");
        Pole larger = new(Guid.NewGuid(), "P03");
        drawing.AddDevice(smaller);
        drawing.AddDevice(attached);
        drawing.AddDevice(larger);
        Terminal smallerAnchor = smaller.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = larger.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device, device.Id,
            "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device, device.Id,
            "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), attached.Id, device.Id));
        Connection left = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            smallerAnchor.Id, device.FirstTerminalId, "left", "10kV");
        Connection right = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            device.SecondTerminalId, largerAnchor.Id, "right", "10kV");
        drawing.AddConnection(left);
        drawing.AddConnection(right);
        drawing.AddOverheadLine(new OverheadLine(left.Id, "JKLYJ", [smaller.Id, attached.Id]));
        drawing.AddOverheadLine(new OverheadLine(right.Id, "JKLYJ", [attached.Id, larger.Id]));
        return (drawing, device);
    }

    private static SwitchDevice AddIsolatedOpenSwitch(DrawingDocument drawing)
    {
        SwitchDevice device = SwitchDevice.CreateForPole(
            Guid.NewGuid(), SwitchKind.LoadSwitch, Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
            device.Id, "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
            device.Id, "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        return device;
    }

    private static EnergizationAnalysisState Analyze(
        DrawingDocument drawing,
        SwitchDevice switchDevice,
        EnergizationSide side)
    {
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(), [Seed(switchDevice, side)], true));
        return state;
    }

    private static EnergizedSeed Seed(SwitchDevice device, EnergizationSide side) =>
        new(Guid.NewGuid(), device.Id, side);
}
