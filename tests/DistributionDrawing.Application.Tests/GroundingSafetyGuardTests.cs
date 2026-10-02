using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.GroundingSafety;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class GroundingSafetyGuardTests
{
    [Fact]
    public void Evaluate_RejectsEveryEnergizedTerminalGroundingAndAllowsDeenergized()
    {
        Fixture fixture = CreateOverheadFixture();
        fixture.Drawing.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(fixture.StartTerminal.Id),
            "杆端", "S01");
        fixture.Drawing.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(fixture.EndTerminal.Id),
            "另一杆端", "S02");
        var guard = new GroundingSafetyGuard();

        GroundingSafetyDecision energized = guard.Evaluate(
            fixture.Drawing, CreateResult(fixture.Drawing, EnergizationState.Energized),
            CurrentSwitchStateView.Instance);
        GroundingSafetyDecision deenergized = guard.Evaluate(
            fixture.Drawing, CreateResult(fixture.Drawing, EnergizationState.Deenergized),
            CurrentSwitchStateView.Instance);

        Assert.False(energized.IsAllowed);
        Assert.Equal(2, energized.Findings.Count);
        Assert.All(energized.Findings, finding => Assert.Equal(
            GroundingSafetyFindingCode.EnergizedGroundedLocation, finding.Code));
        Assert.True(deenergized.IsAllowed);
    }

    [Theory]
    [InlineData(EnergizationState.Energized, false)]
    [InlineData(EnergizationState.Deenergized, true)]
    public void Evaluate_ResolvesGroundingAccessPointByConnection(
        EnergizationState state,
        bool expectedAllowed)
    {
        Fixture fixture = CreateOverheadFixture();
        GroundingAccessPoint gap = fixture.Drawing.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.EndPole.Id, fixture.StartPole.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        fixture.Drawing.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "架空线接地点", "L01");

        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            fixture.Drawing, CreateResult(fixture.Drawing, state), CurrentSwitchStateView.Instance);

        Assert.Equal(expectedAllowed, decision.IsAllowed);
        if (!expectedAllowed)
        {
            GroundingSafetyFinding finding = Assert.Single(decision.Findings);
            Assert.Equal(GroundingElectricalIdentityKind.Connection, finding.Identity!.Kind);
            Assert.Equal(fixture.Connection.Id, finding.Identity.Id);
        }
    }

    [Fact]
    public void Evaluate_ResolvesProspectiveGapAndGroundingPointWithoutDomainMutation()
    {
        Fixture fixture = CreateOverheadFixture();
        var gap = new GroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.EndPole.Id,
            GroundingAdjacentEndpoint.ForPole(fixture.StartPole.Id),
            GroundingAccessLineSide.SmallerNumberSide);
        GroundingPoint pending = GroundingPoint.Create(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "候选接地点", "L02");

        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            fixture.Drawing,
            CreateResult(fixture.Drawing, EnergizationState.Energized),
            CurrentSwitchStateView.Instance,
            [pending],
            [gap]);

        Assert.False(decision.IsAllowed);
        Assert.Single(decision.Findings);
        Assert.Empty(fixture.Drawing.GroundingAccessPoints);
        Assert.Empty(fixture.Drawing.GroundingPoints);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding)]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void Evaluate_RejectsEnergizedEffectiveGroundingAtExactCableSide(
        GroundingStructureKind? structure)
    {
        RingCabinet cabinet = CreateCabinet(structure);
        var drawing = new DrawingDocument(Guid.NewGuid(), "effective grounding guard");
        drawing.AddDevice(cabinet);
        RingCabinetInterval interval = structure is null
            ? cabinet.Intervals[0]
            : cabinet.Intervals.Single(item => item.IntervalKind == IntervalKind.IntegratedFeederInterval);
        if (structure is not null)
            cabinet.SetIntervalCableTerminal(interval.IntervalId, null);
        interval = cabinet.Intervals.Single(item => item.IntervalId == interval.IntervalId);
        SwitchDevice groundSwitch = interval.SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.GroundSwitch);
        var overrides = new Dictionary<Guid, SwitchState>
        {
            [groundSwitch.Id] = SwitchState.Closed
        };
        if (structure == GroundingStructureKind.UpperIsolationGrounding)
        {
            SwitchDevice breaker = interval.SwitchDevices.Single(item =>
                item.SwitchKind == SwitchKind.CircuitBreaker);
            overrides[breaker.Id] = SwitchState.Closed;
        }
        var candidate = CandidateElectricalState.Create(
            drawing, overrides, new EnergizationScenario(Guid.NewGuid()));

        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            drawing, CreateResult(drawing, EnergizationState.Energized), candidate);

        GroundingSafetyFinding finding = Assert.Single(decision.Findings);
        Assert.Equal(GroundingSafetyFindingCode.EnergizedGroundedLocation, finding.Code);
        Assert.Equal(structure is null
            ? interval.CableTerminalId
            : interval.CircuitNodeId, finding.Identity!.Id);
        Assert.Equal(structure is null
            ? GroundingElectricalIdentityKind.Terminal
            : GroundingElectricalIdentityKind.ElectricalNode, finding.Identity.Kind);
        Assert.Equal(SwitchState.Open, groundSwitch.SwitchState);
    }

    [Fact]
    public void Evaluate_FailsClosedWhenEAIsUnavailableOrTargetCannotResolve()
    {
        Fixture fixture = CreateOverheadFixture();
        EnergizationResult failed = new(
            EnergizationValidity.Failed,
            new Dictionary<Guid, EnergizationPointResult>(),
            new Dictionary<Guid, EnergizationPointResult>(),
            [], [], []);

        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            fixture.Drawing, failed, CurrentSwitchStateView.Instance);

        Assert.False(decision.IsAllowed);
        Assert.Equal(GroundingSafetyFindingCode.AnalysisUnavailable,
            Assert.Single(decision.Findings).Code);
    }

    private static RingCabinet CreateCabinet(GroundingStructureKind? structure)
    {
        RingCabinetIntervalDefinition[] definitions = structure is null
            ? [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)
              ]
            : [
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    1,
                    structure.Value,
                    SwitchState.Open,
                    structure == GroundingStructureKind.UpperIsolationGrounding
                        ? SwitchState.Closed : SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)
              ];
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "GS cabinet", definitions));
    }

    private static EnergizationResult CreateResult(
        DrawingDocument drawing,
        EnergizationState state)
    {
        var terminals = drawing.Terminals.ToDictionary(
            terminal => terminal.Id,
            terminal => new EnergizationPointResult(
                terminal.ElectricalNodeId is Guid terminalNodeId &&
                drawing.ElectricalNodes.Single(node => node.Id == terminalNodeId).Type == ElectricalNodeType.Earth
                    ? EnergizationState.Deenergized : state,
                []));
        var nodes = drawing.ElectricalNodes.ToDictionary(
            node => node.Id,
            node => new EnergizationPointResult(
                node.Type == ElectricalNodeType.Earth ? EnergizationState.Deenergized : state,
                []));
        ElectricalConnectivityEdge[] edges = new ElectricalConnectivityGraphBuilder()
            .Build(drawing).Edges.ToArray();
        return new EnergizationResult(
            EnergizationValidity.Complete, terminals, nodes, [], edges, []);
    }

    private static Fixture CreateOverheadFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "GS overhead");
        var startPole = new Pole(Guid.NewGuid(), "P-10");
        var endPole = new Pole(Guid.NewGuid(), "P-11");
        drawing.AddDevice(startPole);
        drawing.AddDevice(endPole);
        Terminal startTerminal = startPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal endTerminal = endPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(startTerminal);
        drawing.AddTerminal(endTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            startTerminal.Id, endTerminal.Id, "GS OHL", "10kV");
        drawing.AddConnection(connection);
        drawing.AddOverheadLine(new OverheadLine(
            connection.Id, "JKLYJ", [startPole.Id, endPole.Id]));
        return new Fixture(drawing, startPole, endPole, startTerminal, endTerminal, connection);
    }

    private sealed record Fixture(
        DrawingDocument Drawing,
        Pole StartPole,
        Pole EndPole,
        Terminal StartTerminal,
        Terminal EndTerminal,
        Connection Connection);
}
