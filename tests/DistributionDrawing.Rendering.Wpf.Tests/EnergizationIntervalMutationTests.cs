using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class EnergizationIntervalMutationTests
{
    [Fact]
    public void ReplacementRevertAndReconnectRemainAnalyzableAfterReselectingSource()
    {
        var (drawing, cabinet, layout, scenario, cable) = CreateFixture();
        Guid intervalId = cabinet.Intervals[0].IntervalId;
        var analyzer = new EnergizationAnalyzer();
        Assert.True(analyzer.Analyze(drawing, scenario).IsSuccess);
        var stack = new CommandStack();
        stack.ExecuteCommand(Change(GroundingStructureKind.UpperLowerGrounding));
        SelectCurrentSource();
        EnergizationResult replaced = analyzer.Analyze(drawing, scenario);
        stack.ExecuteCommand(Change(GroundingStructureKind.UpperIsolationGrounding));
        SelectCurrentSource();
        EnergizationResult reverted = analyzer.Analyze(drawing, scenario);
        var connection = drawing.Connections.Single(item => item.Id == cable.ConnectionId);
        new ReconnectCableCommand(drawing, cable.Id, cabinet.Intervals[1].CableTerminalId!.Value,
            connection.EndTerminalId).Execute();
        EnergizationResult reconnected = analyzer.Analyze(drawing, scenario);
        foreach (EnergizationResult result in new[] { replaced, reverted, reconnected })
            Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        AssertTopology(drawing, cabinet);

        ChangeIntervalTypeCommand Change(GroundingStructureKind structure) => new(cabinet,
            layout, intervalId, IntervalKind.IntegratedFeederInterval, structure, document: drawing, scenario: scenario);
        void SelectCurrentSource()
        {
            foreach (var seed in scenario.Seeds.ToArray())
                EnergizationScenarioCommand.Remove(scenario, seed.Id).Execute();
            var source = cabinet.Intervals[0].SwitchDevices.Single(item => item.SwitchKind == SwitchKind.IsolationSwitch);
            EnergizationScenarioCommand.Add(scenario,
                new EnergizedSeed(Guid.NewGuid(), source.Id, EnergizationSide.Bus)).Execute();
        }
    }

    [Fact]
    public void ReplacementDoesNotRetainDeletedSourceBoundary()
    {
        var (drawing, cabinet, layout, scenario, _) = CreateFixture();
        var seed = Assert.Single(scenario.Seeds);
        new ChangeIntervalTypeCommand(cabinet, layout, cabinet.Intervals[0].IntervalId,
            IntervalKind.IntegratedFeederInterval, GroundingStructureKind.UpperLowerGrounding,
            document: drawing, scenario: scenario).Execute();
        Assert.DoesNotContain(scenario.Seeds, item => item.BoundaryDeviceId == seed.BoundaryDeviceId);
        Assert.Empty(scenario.Seeds);
        Assert.Equal(EnergizationValidity.NoSeeds, new EnergizationAnalyzer().Analyze(drawing, scenario).Validity);
        AssertTopology(drawing, cabinet);
    }

    [Fact]
    public void UnreconciledSeedFailsExplicitlyEvenWhenCableAndReplacementTopologyAreValid()
    {
        var (drawing, cabinet, layout, scenario, cable) = CreateFixture();
        var oldSeed = scenario.Seeds[0];
        // Exercise the low-level topology command without project-owned Scenario wiring.
        new ChangeIntervalTypeCommand(cabinet, layout, cabinet.Intervals[0].IntervalId,
            IntervalKind.IntegratedFeederInterval, GroundingStructureKind.UpperLowerGrounding,
            document: drawing).Execute();
        AssertTopology(drawing, cabinet);
        Assert.DoesNotContain(drawing.Devices, device => device.Id == oldSeed.BoundaryDeviceId);
        AssertFailure();
        var connection = drawing.Connections.Single(item => item.Id == cable.ConnectionId);
        new ReconnectCableCommand(drawing, cable.Id, cabinet.Intervals[1].CableTerminalId!.Value,
            connection.EndTerminalId).Execute();
        AssertTopology(drawing, cabinet);
        AssertFailure();
        EnergizationScenarioCommand.Remove(scenario, oldSeed.Id).Execute();
        var boundary = cabinet.Intervals[0].SwitchDevices.Single(device => device.SwitchKind == SwitchKind.IsolationSwitch);
        EnergizationScenarioCommand.Add(scenario,
            new EnergizedSeed(Guid.NewGuid(), boundary.Id, EnergizationSide.Bus)).Execute();
        Assert.True(new EnergizationAnalyzer().Analyze(drawing, scenario).IsSuccess);

        void AssertFailure()
        {
            var result = new EnergizationAnalyzer().Analyze(drawing, scenario);
            Assert.Equal(EnergizationValidity.Failed, result.Validity);
            Assert.Contains(result.Diagnostics, item => item.Code == EnergizationDiagnosticCode.MissingBoundaryDevice);
            Assert.Empty(result.Terminals);
            Assert.Empty(result.Nodes);
        }
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void CommandStackUndoRedoRestoresSourcesAndBothTopologyStates(GroundingStructureKind structure)
    {
        var (drawing, cabinet, layout, scenario, _) = CreateFixture();
        EnergizedSeed original = scenario.Seeds[0];
        var unaffectedBoundary = cabinet.Intervals[1].SwitchDevices.Single(item => item.SwitchKind == SwitchKind.LoadSwitch);
        var unaffected = new EnergizedSeed(Guid.NewGuid(), unaffectedBoundary.Id, EnergizationSide.Bus);
        EnergizationScenarioCommand.Add(scenario, unaffected).Execute();
        scenario.SetSourceSetComplete(true);
        Guid[] beforeSwitches = cabinet.Intervals[0].SwitchDevices.Select(item => item.Id).ToArray();
        Guid[] beforeTerminals = drawing.Terminals.Select(item => item.Id).ToArray();
        var command = new ChangeIntervalTypeCommand(cabinet, layout, cabinet.Intervals[0].IntervalId,
            IntervalKind.IntegratedFeederInterval, structure, document: drawing, scenario: scenario);
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.Equal(original, Assert.Single(command.RemovedSeeds));
        Guid[] afterSwitches = cabinet.Intervals[0].SwitchDevices.Select(item => item.Id).ToArray();
        Assert.Equal(unaffected, Assert.Single(scenario.Seeds));
        Check();
        Assert.True(stack.Undo());
        Assert.Equal([original, unaffected], scenario.Seeds);
        Assert.Equal(beforeSwitches, cabinet.Intervals[0].SwitchDevices.Select(item => item.Id));
        Assert.Equal(beforeTerminals.Order(), drawing.Terminals.Select(item => item.Id).Order());
        Check();
        Assert.True(stack.Redo());
        Assert.Equal(unaffected, Assert.Single(scenario.Seeds));
        Assert.Equal(afterSwitches, cabinet.Intervals[0].SwitchDevices.Select(item => item.Id));
        Assert.Single(stack.History);
        Check();

        void Check()
        {
            AssertTopology(drawing, cabinet);
            Assert.True(scenario.IsSourceSetComplete);
            var result = new EnergizationAnalyzer().Analyze(drawing, scenario);
            Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
            Assert.All(result.Terminals.Values, point => Assert.NotEqual(EnergizationState.Unknown, point.State));
        }
    }

    [Fact]
    public void FailedCommandValidationRollsBackTopologyLayoutAndScenarioTogether()
    {
        var (drawing, cabinet, layout, scenario, _) = CreateFixture();
        var before = cabinet.CaptureRestoreDefinition();
        var seed = scenario.Seeds[0];
        var beforeLayout = layout.RingCabinetLayouts[cabinet.Id];
        var stack = new CommandStack();
        var command = new ChangeIntervalTypeCommand(cabinet, layout, cabinet.Intervals[0].IntervalId,
            IntervalKind.IntegratedFeederInterval, GroundingStructureKind.UpperLowerGrounding,
            document: drawing, scenario: scenario);
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(command,
            () => throw new InvalidOperationException("scene validation rejected")));
        Assert.Empty(stack.History);
        Assert.Equal(seed, Assert.Single(scenario.Seeds));
        Assert.Same(beforeLayout, layout.RingCabinetLayouts[cabinet.Id]);
        Assert.Equal(before.Intervals[0].Switches.Select(item => item.Id), cabinet.Intervals[0].SwitchDevices.Select(item => item.Id));
        AssertTopology(drawing, cabinet);
        Assert.True(new EnergizationAnalyzer().Analyze(drawing, scenario).IsSuccess);
        stack.ExecuteCommand(command);
        Assert.Empty(scenario.Seeds);
        AssertTopology(drawing, cabinet);
        Assert.True(stack.Undo());
        Assert.Equal(seed, Assert.Single(scenario.Seeds));
        AssertTopology(drawing, cabinet);
    }

    [Fact]
    public void RejectedRetiredCableEndpointLeavesOriginalSourceAndTopologyIntact()
    {
        var (drawing, cabinet, layout, scenario, _) = CreateFixture();
        var seed = scenario.Seeds[0];
        var stack = new CommandStack();
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(new ChangeIntervalTypeCommand(
            cabinet, layout, cabinet.Intervals[0].IntervalId, IntervalKind.PTInterval, null,
            document: drawing, scenario: scenario)));
        Assert.Empty(stack.History);
        Assert.Equal(seed, Assert.Single(scenario.Seeds));
        AssertTopology(drawing, cabinet);
        Assert.True(new EnergizationAnalyzer().Analyze(drawing, scenario).IsSuccess);
    }

    private static (DrawingDocument, RingCabinet, RuntimeLayoutDocument, EnergizationScenario, CableSegment) CreateFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "interval EA regression");
        var cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "柜 A",
            [RingCabinetIntervalDefinition.CreateIntegratedFeeder(1, GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        var endpoint = new IntermediateTerminalCreationFactory().Create("电缆末端");
        new CreateIntermediateTerminalCommand(drawing, endpoint).Execute();
        var cable = new CableSegmentCreationFactory().Create(drawing, cabinet.Intervals[0].CableTerminalId!.Value,
            endpoint.Terminal.Id, "电缆", "10kV-Cable", 25);
        new CreateCableSegmentCommand(drawing, cable).Execute();
        var layout = new RuntimeLayoutDocument(new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>
            { [cabinet.Id] = new RingCabinetLayoutFactory().Create(cabinet, new(0, 0)) });
        var boundary = cabinet.Intervals[0].SwitchDevices.Single(item => item.SwitchKind == SwitchKind.IsolationSwitch);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), boundary.Id, EnergizationSide.Bus)]);
        return (drawing, cabinet, layout, scenario, cable.CableSegment);
    }

    private static void AssertTopology(DrawingDocument drawing, RingCabinet cabinet)
    {
        RingCabinet.Restore(cabinet.CaptureRestoreDefinition());
        Assert.All(drawing.Terminals, terminal =>
        {
            if (terminal.OwnerType == TopologyOwnerType.Device)
                Assert.Contains(drawing.Devices, item => item.Id == terminal.OwnerId);
            if (terminal.ElectricalNodeId is Guid id)
                Assert.Contains(drawing.ElectricalNodes, node => node.Id == id && node.TerminalIds.Contains(terminal.Id));
        });
        Assert.All(drawing.ElectricalNodes, node =>
        {
            if (node.OwnerType == TopologyOwnerType.Device)
                Assert.Contains(drawing.Devices, item => item.Id == node.OwnerId);
            Assert.All(node.TerminalIds, id => Assert.Contains(drawing.Terminals,
                terminal => terminal.Id == id && terminal.ElectricalNodeId == node.Id));
        });
        Assert.All(drawing.Connections, connection =>
        {
            Assert.Contains(drawing.Terminals, item => item.Id == connection.StartTerminalId);
            Assert.Contains(drawing.Terminals, item => item.Id == connection.EndTerminalId);
        });
        Assert.Equal(cabinet.ElectricalNodes.Single(item => item.Id == cabinet.MainBusNodeId).TerminalIds.Order(),
            drawing.ElectricalNodes.Single(item => item.Id == cabinet.MainBusNodeId).TerminalIds.Order());
        new ElectricalConnectivityGraphBuilder().Build(drawing);
    }
}
