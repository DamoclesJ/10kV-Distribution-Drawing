using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class AuthoritativeSeedRegressionTests
{
    [Fact]
    public void OneSeedClosedPath_AllReachableAreEnergizedWithoutConfirmation()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "binary");
        RingCabinet cabinet = CreateCabinet(0);
        drawing.AddDevice(cabinet);
        SwitchDevice source = Switch(cabinet.Intervals[0], SwitchKind.LoadSwitch);
        EnergizedSeed seed = new(Guid.NewGuid(), source.Id, EnergizationSide.Bus);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed]);
        EnergizationResult result = new EnergizationAnalyzer().Analyze(drawing, scenario);
        Assert.True(result.IsSuccess);
        Assert.False(scenario.IsSourceSetComplete);
        HashSet<Guid> reachedNodes = [cabinet.MainBusNodeId,
            cabinet.Intervals[0].CircuitNodeId, cabinet.Intervals[1].CircuitNodeId];
        foreach (Guid id in drawing.Terminals
            .Where(terminal => terminal.ElectricalNodeId is Guid nodeId && reachedNodes.Contains(nodeId))
            .Select(terminal => terminal.Id))
            Assert.Equal(EnergizationState.Energized, result.Terminals[id].State);
        Assert.All(result.Terminals.Values, point => Assert.NotEqual(EnergizationState.Unknown, point.State));
    }

    [Fact]
    public void FourCabinetsUpperLowerGroundCableTerminationOhlAndPoleHaveNoTopologyBreak()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "mixed pressure topology");
        RingCabinet[] cabinets = Enumerable.Range(0, 4).Select(CreateCabinet).ToArray();
        foreach (RingCabinet cabinet in cabinets) drawing.AddDevice(cabinet);
        for (int index = 0; index < cabinets.Length - 1; index++)
            drawing.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.Cable,
                cabinets[index].Intervals[1].CableTerminalId!.Value,
                cabinets[index + 1].Intervals[0].CableTerminalId!.Value, $"电缆 {index + 1}", "10kV"));
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P01", PoleType.Cement, null, switchKinds: null, includeCableTerminal: true);
        new CreatePoleCommand(drawing, pole).Execute();
        CableTermination termination = pole.Devices.OfType<CableTermination>().Single();
        var center = new Pole(Guid.NewGuid(), "P02");
        var endPole = new Pole(Guid.NewGuid(), "P03");
        drawing.AddDevice(center);
        drawing.AddDevice(endPole);
        SwitchDevice poleSwitch = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.IsolationSwitch,
            Guid.NewGuid(), Guid.NewGuid(), SwitchState.Open);
        drawing.AddDevice(poleSwitch);
        foreach (Guid id in poleSwitch.TerminalIds)
            drawing.AddTerminal(new Terminal(id, TopologyOwnerType.Device, poleSwitch.Id,
                id == poleSwitch.FirstTerminalId ? "First" : "Second", "10kV", true, false,
                null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), center.Id, poleSwitch.Id));
        Terminal end = endPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(end);
        drawing.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinets[3].Intervals[1].CableTerminalId!.Value, termination.CableSideTerminalId,
            "终端电缆", "10kV"));
        var firstOhl = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            termination.OverheadSideTerminalId, poleSwitch.FirstTerminalId, "架空线 A", "10kV");
        var secondOhl = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            poleSwitch.SecondTerminalId, end.Id, "架空线 B", "10kV");
        drawing.AddConnection(firstOhl);
        drawing.AddConnection(secondOhl);
        drawing.AddOverheadLine(new OverheadLine(firstOhl.Id, "JKLYJ", [pole.Pole.Id, center.Id]));
        drawing.AddOverheadLine(new OverheadLine(secondOhl.Id, "JKLYJ", [center.Id, endPole.Id]));
        SwitchDevice source = Switch(cabinets[0].Intervals[0], SwitchKind.LoadSwitch);
        EnergizedSeed seed = new(Guid.NewGuid(), source.Id, EnergizationSide.Bus);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [seed]);
        var analyzer = new EnergizationAnalyzer();
        SwitchDevice middleBreaker = Switch(cabinets[2].Intervals[0], SwitchKind.CircuitBreaker);
        drawing.ChangeSwitchState(middleBreaker.Id, SwitchState.Open);

        EnergizationResult isolated = analyzer.Analyze(drawing, scenario);
        AssertBinary(isolated);
        Assert.Equal(EnergizationState.Energized, isolated.Nodes[cabinets[1].MainBusNodeId].State);
        Assert.Equal(EnergizationState.Energized,
            isolated.Terminals[cabinets[2].Intervals[0].CableTerminalId!.Value].State);
        Assert.Equal(EnergizationState.Deenergized, isolated.Nodes[cabinets[2].MainBusNodeId].State);
        Assert.Equal(EnergizationState.Deenergized, isolated.Nodes[cabinets[3].MainBusNodeId].State);
        Assert.Equal(EnergizationState.Deenergized, isolated.Terminals[termination.CableSideTerminalId].State);

        drawing.ChangeSwitchState(middleBreaker.Id, SwitchState.Closed);
        EnergizationResult closed = analyzer.Analyze(drawing, scenario);
        AssertBinary(closed);
        Assert.All(cabinets, cabinet => Assert.Equal(EnergizationState.Energized,
            closed.Nodes[cabinet.MainBusNodeId].State));
        Assert.Equal(EnergizationState.Energized, closed.Terminals[termination.CableSideTerminalId].State);
        Assert.Equal(EnergizationState.Energized, closed.Nodes[termination.InternalNodeId].State);
        Assert.Equal(EnergizationState.Energized, closed.Terminals[termination.OverheadSideTerminalId].State);
        Assert.Equal(EnergizationState.Energized, closed.Terminals[poleSwitch.FirstTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, closed.Terminals[poleSwitch.SecondTerminalId].State);
        Assert.Equal(EnergizationState.Deenergized, closed.Terminals[end.Id].State);
        Assert.True(new EnergizationBoundaryPolicy().TryResolve(drawing,
            new EnergizedSeed(Guid.NewGuid(), poleSwitch.Id, EnergizationSide.SmallerNumber), out Guid smaller, out _));
        Assert.Equal(poleSwitch.FirstTerminalId, smaller);
        drawing.ChangeSwitchState(poleSwitch.Id, SwitchState.Closed);
        EnergizationResult allClosed = analyzer.Analyze(drawing, scenario);
        AssertBinary(allClosed);
        Assert.Equal([seed.Id], allClosed.Terminals[end.Id].EnergizedBy);
        foreach (RingCabinet cabinet in cabinets)
            foreach (RingCabinetInterval interval in cabinet.Intervals)
                Assert.Equal(EnergizationState.Deenergized, allClosed.Nodes[interval.EarthNodeId].State);
    }

    [Fact]
    public void InvalidSideAndInconsistentNodeFailWithoutPublishingAnyPointState()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "invalid topology");
        RingCabinet cabinet = CreateCabinet(0);
        drawing.AddDevice(cabinet);
        SwitchDevice source = Switch(cabinet.Intervals[0], SwitchKind.LoadSwitch);
        var analyzer = new EnergizationAnalyzer();
        EnergizationResult wrongSide = analyzer.Analyze(drawing,
            new EnergizationScenario(Guid.NewGuid(),
                [new EnergizedSeed(Guid.NewGuid(), source.Id, EnergizationSide.LargerNumber)]));
        Assert.Equal(EnergizationValidity.Failed, wrongSide.Validity);
        Assert.Contains(wrongSide.Diagnostics, issue => issue.Code == EnergizationDiagnosticCode.InvalidSide);
        Assert.Empty(wrongSide.Terminals);
        // Inject an inconsistent membership to exercise the software/model failure boundary.
        ElectricalNode node = drawing.ElectricalNodes.Single(item => item.Id == cabinet.MainBusNodeId);
        ((HashSet<Guid>)node.TerminalIds).Remove(source.FirstTerminalId);
        EnergizationResult broken = analyzer.Analyze(drawing,
            new EnergizationScenario(Guid.NewGuid(),
                [new EnergizedSeed(Guid.NewGuid(), source.Id, EnergizationSide.Bus)]));
        Assert.Equal(EnergizationValidity.Failed, broken.Validity);
        Assert.Contains(broken.Diagnostics, issue => issue.Code == EnergizationDiagnosticCode.InvalidTopology);
        Assert.Empty(broken.Terminals);
        Assert.Empty(broken.Nodes);
    }

    private static void AssertBinary(EnergizationResult result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.All(result.Terminals.Values.Concat(result.Nodes.Values), point =>
            Assert.True(point.State is EnergizationState.Energized or EnergizationState.Deenergized));
    }

    private static SwitchDevice Switch(RingCabinetInterval interval, SwitchKind kind) =>
        interval.SwitchDevices.Single(item => item.SwitchKind == kind);

    private static RingCabinet CreateCabinet(int index) => RingCabinet.Create(
        RingCabinetDefinition.Create(Guid.NewGuid(), $"柜 {index}",
            [index == 0
                ? RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open)
                : RingCabinetIntervalDefinition.CreateIntegratedFeeder(1, index switch
                {
                    1 => GroundingStructureKind.UpperIsolationGrounding,
                    2 => GroundingStructureKind.UpperLowerGrounding,
                    _ => GroundingStructureKind.LowerLowerGrounding
                }, SwitchState.Closed, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open, SwitchState.Closed)]));
}
