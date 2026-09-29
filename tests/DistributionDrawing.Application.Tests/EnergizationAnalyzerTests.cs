using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class EnergizationAnalyzerTests
{
    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding, SwitchKind.IsolationSwitch)]
    [InlineData(GroundingStructureKind.UpperLowerGrounding, SwitchKind.IsolationSwitch)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding, SwitchKind.CircuitBreaker)]
    public void IntegratedFeederBoundaryUsesStructureAndBothTerminalSides(
        GroundingStructureKind structure, SwitchKind expectedKind)
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(1, structure,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                SwitchState.Open, SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice chosen = interval.SwitchDevices.Single(device => device.SwitchKind == expectedKind);
        SwitchDevice other = interval.SwitchDevices.Single(device => device.SwitchKind ==
            (expectedKind == SwitchKind.CircuitBreaker
                ? SwitchKind.IsolationSwitch : SwitchKind.CircuitBreaker));
        var policy = new EnergizationBoundaryPolicy();

        Assert.True(policy.TryResolve(drawing, Seed(chosen, EnergizationSide.Bus),
            out Guid bus, out _));
        Assert.True(policy.TryResolve(drawing, Seed(chosen, EnergizationSide.Line),
            out Guid line, out _));
        Assert.Equal(chosen.FirstTerminalId, bus);
        Assert.Equal(chosen.SecondTerminalId, line);
        Assert.False(policy.TryResolve(drawing, Seed(other, EnergizationSide.Bus),
            out _, out EnergizationDiagnosticCode wrongSwitch));
        Assert.Equal(EnergizationDiagnosticCode.UnsupportedBoundary, wrongSwitch);
        Assert.False(policy.TryResolve(drawing,
            Seed(interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.GroundSwitch),
                EnergizationSide.Bus), out _, out _));
        Assert.False(policy.TryResolve(drawing, Seed(chosen, EnergizationSide.SmallerNumber),
            out _, out EnergizationDiagnosticCode wrongSide));
        Assert.Equal(EnergizationDiagnosticCode.InvalidSide, wrongSide);
    }

    [Fact]
    public void OrdinaryCabinetBoundaryIsLoadSwitchAndInternalNodesRemainSeparateWhenOpen()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice first = LoadSwitch(cabinet.Intervals[0]);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(first, EnergizationSide.Bus)], true);

        EnergizationResult result = new EnergizationAnalyzer().Analyze(drawing, scenario);

        Assert.Equal(EnergizationValidity.Complete, result.Validity);
        Assert.Equal(EnergizationState.Energized, result.Terminals[first.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, result.Terminals[first.SecondTerminalId].State);
        Assert.Equal(EnergizationState.Energized, result.Nodes[cabinet.MainBusNodeId].State);
        Assert.Equal(EnergizationState.Deenergized,
            result.Nodes[cabinet.Intervals[0].CircuitNodeId].State);
        drawing.ChangeSwitchState(first.Id, SwitchState.Closed);
        Assert.Equal(EnergizationState.Energized,
            new EnergizationAnalyzer().Analyze(drawing, scenario)
                .Terminals[first.SecondTerminalId].State);
    }

    [Fact]
    public void EveryInternalSwitchParticipatesAfterSeedResolution()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(1,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                SwitchState.Open, SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice isolation = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.IsolationSwitch);
        SwitchDevice breaker = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(isolation, EnergizationSide.Bus)], true);

        EnergizationResult open = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(EnergizationState.Energized, open.Terminals[breaker.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, open.Terminals[breaker.SecondTerminalId].State);

        drawing.ChangeSwitchState(breaker.Id, SwitchState.Closed);
        EnergizationResult closed = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(EnergizationState.Energized, closed.Terminals[breaker.SecondTerminalId].State);
        Assert.Equal(EnergizationState.Energized, closed.Nodes[interval.CircuitNodeId].State);
    }

    [Fact]
    public void MultipleSeedsAreAllTrackedAndOpenedSwitchSeparatesTheirSources()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open));
        SwitchDevice first = LoadSwitch(cabinet.Intervals[0]);
        SwitchDevice second = LoadSwitch(cabinet.Intervals[1]);
        EnergizedSeed a = Seed(first, EnergizationSide.Line);
        EnergizedSeed b = Seed(second, EnergizationSide.Line);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [a, b], true);

        EnergizationResult closed = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(new HashSet<Guid> { a.Id, b.Id },
            closed.Nodes[cabinet.MainBusNodeId].EnergizedBy.ToHashSet());

        drawing.ChangeSwitchState(first.Id, SwitchState.Open);
        EnergizationResult opened = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal([a.Id], opened.Terminals[first.SecondTerminalId].EnergizedBy);
        Assert.Equal([b.Id], opened.Terminals[first.FirstTerminalId].EnergizedBy);
        Assert.True(opened.CanConcludeDeenergized);
    }

    [Fact]
    public void UnconfirmedOrFailedSourceSetProvesOnlyReachedObjects()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice first = LoadSwitch(cabinet.Intervals[0]);
        EnergizedSeed valid = Seed(first, EnergizationSide.Bus);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [valid]);
        var analyzer = new EnergizationAnalyzer();

        EnergizationResult unconfirmed = analyzer.Analyze(drawing, scenario);
        Assert.Equal(EnergizationValidity.ForwardOnly, unconfirmed.Validity);
        Assert.Equal(EnergizationState.Energized, unconfirmed.Terminals[first.FirstTerminalId].State);
        Assert.Equal([valid.Id], unconfirmed.Terminals[first.FirstTerminalId].EnergizedBy);
        Assert.Equal(EnergizationState.Unknown, unconfirmed.Terminals[first.SecondTerminalId].State);

        scenario.SetSourceSetComplete(true);
        scenario.AddSeed(new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus));
        scenario.SetSourceSetComplete(true);
        EnergizationResult partial = analyzer.Analyze(drawing, scenario);
        Assert.Equal(EnergizationValidity.Incomplete, partial.Validity);
        Assert.Equal(EnergizationState.Energized, partial.Terminals[first.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Unknown, partial.Terminals[first.SecondTerminalId].State);
        Assert.Contains(partial.Diagnostics, issue =>
            issue.Code == EnergizationDiagnosticCode.MissingBoundaryDevice);

        EnergizationResult empty = analyzer.Analyze(drawing,
            new EnergizationScenario(Guid.NewGuid()));
        Assert.Equal(EnergizationValidity.NoSeeds, empty.Validity);
        Assert.All(empty.Terminals.Values, item =>
            Assert.Equal(EnergizationState.Unknown, item.State));
    }

    [Fact]
    public void ClosedGroundSwitchIsQueryableButNeverPropagatesIntoEarth()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Closed),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice load = LoadSwitch(interval);
        SwitchDevice ground = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(load, EnergizationSide.Line)], true);

        EnergizationResult result = new EnergizationAnalyzer().Analyze(drawing, scenario);

        Assert.Equal(EnergizationState.Energized, result.Terminals[ground.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Unknown, result.Terminals[ground.SecondTerminalId].State);
        Assert.Equal(EnergizationState.Unknown, result.Nodes[interval.EarthNodeId].State);
        Assert.DoesNotContain(result.ConductingEdges, edge => edge.SourceId == ground.Id);
        Assert.Equal(ground.Id, Assert.Single(result.GroundingSwitchConnections).SwitchDeviceId);
    }

    [Fact]
    public void CableConnectionPropagatesBetweenCabinetsWithoutBypassingOpenSwitch()
    {
        (DrawingDocument drawing, RingCabinet firstCabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        RingCabinet secondCabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "second",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(secondCabinet);
        var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            firstCabinet.Intervals[0].CableTerminalId!.Value,
            secondCabinet.Intervals[0].CableTerminalId!.Value, "cable", "10kV");
        drawing.AddConnection(cable);
        SwitchDevice source = LoadSwitch(firstCabinet.Intervals[0]);
        SwitchDevice destination = LoadSwitch(secondCabinet.Intervals[0]);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(source, EnergizationSide.Bus)], true);

        EnergizationResult result = new EnergizationAnalyzer().Analyze(drawing, scenario);

        Assert.Equal(EnergizationState.Energized,
            result.Terminals[secondCabinet.Intervals[0].CableTerminalId!.Value].State);
        Assert.Equal(EnergizationState.Energized,
            result.Terminals[destination.SecondTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized,
            result.Terminals[destination.FirstTerminalId].State);
        Assert.Contains(result.ConductingEdges, edge => edge.SourceId == cable.Id);
    }

    [Fact]
    public void CableTerminationPropagatesFromCableSideThroughItsExistingInternalPath()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P-100", PoleType.Cement, null, switchKinds: null, includeCableTerminal: true);
        new CreatePoleCommand(drawing, pole).Execute();
        CableTermination termination = Assert.Single(pole.Devices.OfType<CableTermination>());
        var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value,
            termination.CableSideTerminalId, "cable", "10kV");
        drawing.AddConnection(cable);
        Guid[] nodeIdsBeforeAnalysis = drawing.ElectricalNodes.Select(node => node.Id).ToArray();
        EnergizedSeed seed = Seed(LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus);

        EnergizationResult result = new EnergizationAnalyzer().Analyze(drawing,
            new EnergizationScenario(Guid.NewGuid(), [seed], true));

        Assert.Equal(EnergizationState.Energized,
            result.Terminals[termination.CableSideTerminalId].State);
        Assert.Equal(EnergizationState.Energized,
            result.Terminals[termination.OverheadSideTerminalId].State);
        Assert.Equal(EnergizationState.Energized, result.Nodes[termination.InternalNodeId].State);
        Assert.Equal([seed.Id], result.Terminals[termination.OverheadSideTerminalId].EnergizedBy);
        Assert.Equal([seed.Id], result.Nodes[termination.InternalNodeId].EnergizedBy);
        Assert.Contains(result.ConductingEdges, edge =>
            edge.Type == ElectricalConnectivityEdgeType.PassiveDeviceInternal &&
            edge.SourceId == termination.Id);
        Assert.Equal(nodeIdsBeforeAnalysis, drawing.ElectricalNodes.Select(node => node.Id));
        Assert.Equal(nodeIdsBeforeAnalysis.ToHashSet(), result.Nodes.Keys.ToHashSet());
    }

    [Fact]
    public void UnprovablePoleSideAndMissingTopologyNeverCreateDeenergizedConclusion()
    {
        (DrawingDocument drawing, SwitchDevice device) = PoleSwitch(SwitchKind.LoadSwitch);
        Guid lineId = drawing.OverheadLines.Single(line =>
            line.SupportPoleIds.Any(poleId =>
                drawing.Devices.OfType<Pole>().Any(pole =>
                    pole.Id == poleId && pole.PoleNumber == "P03"))).ConnectionId;
        drawing.RemoveOverheadLine(lineId);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(device, EnergizationSide.LargerNumber)], true);

        EnergizationResult unresolved = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(EnergizationValidity.Incomplete, unresolved.Validity);
        Assert.Contains(unresolved.Diagnostics, issue =>
            issue.Code == EnergizationDiagnosticCode.UnresolvedSide);
        Assert.All(unresolved.Terminals.Values, item =>
            Assert.Equal(EnergizationState.Unknown, item.State));

        var broken = new DrawingDocument(Guid.NewGuid(), "broken topology");
        broken.AddDevice(SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
            Guid.NewGuid(), Guid.NewGuid()));
        EnergizationResult invalid = new EnergizationAnalyzer().Analyze(broken,
            new EnergizationScenario(Guid.NewGuid(),
                [new EnergizedSeed(Guid.NewGuid(), broken.Devices[0].Id,
                    EnergizationSide.SmallerNumber)], true));
        Assert.Equal(EnergizationValidity.Incomplete, invalid.Validity);
        Assert.Contains(invalid.Diagnostics, issue =>
            issue.Code == EnergizationDiagnosticCode.InvalidTopology);
    }

    [Theory]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void PoleSideIsProvenFromLineTopologyAndOpenBlocksPropagation(SwitchKind kind)
    {
        (DrawingDocument drawing, SwitchDevice device) = PoleSwitch(kind);
        var policy = new EnergizationBoundaryPolicy();
        Assert.True(policy.TryResolve(drawing, Seed(device, EnergizationSide.SmallerNumber),
            out Guid smaller, out _));
        Assert.True(policy.TryResolve(drawing, Seed(device, EnergizationSide.LargerNumber),
            out Guid larger, out _));
        Assert.Equal(device.FirstTerminalId, smaller);
        Assert.Equal(device.SecondTerminalId, larger);

        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [Seed(device, EnergizationSide.SmallerNumber)], true);
        EnergizationResult open = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(EnergizationState.Deenergized, open.Terminals[larger].State);
        drawing.ChangeSwitchState(device.Id, SwitchState.Closed);
        EnergizationResult closed = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.Equal(EnergizationState.Energized, closed.Terminals[larger].State);
    }

    private static (DrawingDocument, RingCabinet) Cabinet(
        params RingCabinetIntervalDefinition[] intervals)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA cabinet");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "EA cabinet", intervals));
        drawing.AddDevice(cabinet);
        return (drawing, cabinet);
    }

    private static SwitchDevice LoadSwitch(RingCabinetInterval interval) =>
        interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);

    private static EnergizedSeed Seed(SwitchDevice device, EnergizationSide side) =>
        new(Guid.NewGuid(), device.Id, side);

    private static (DrawingDocument, SwitchDevice) PoleSwitch(SwitchKind kind)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA pole");
        var smaller = new Pole(Guid.NewGuid(), "P01");
        var center = new Pole(Guid.NewGuid(), "P02");
        var larger = new Pole(Guid.NewGuid(), "P03");
        drawing.AddDevice(smaller);
        drawing.AddDevice(center);
        drawing.AddDevice(larger);
        Terminal smallerAnchor = smaller.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = larger.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), kind,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
            device.Id, "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
            device.Id, "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), center.Id, device.Id));
        var left = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            smallerAnchor.Id, device.FirstTerminalId, "left", "10kV");
        var right = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            device.SecondTerminalId, largerAnchor.Id, "right", "10kV");
        drawing.AddConnection(left);
        drawing.AddConnection(right);
        drawing.AddOverheadLine(new OverheadLine(left.Id, "JKLYJ", [smaller.Id, center.Id]));
        drawing.AddOverheadLine(new OverheadLine(right.Id, "JKLYJ", [center.Id, larger.Id]));
        return (drawing, device);
    }
}
