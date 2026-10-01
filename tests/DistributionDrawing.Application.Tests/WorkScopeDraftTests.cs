using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkScopeDraftTests
{
    [Theory]
    [InlineData(EnergizationSide.Bus, EnergizationSide.Line)]
    [InlineData(EnergizationSide.Line, EnergizationSide.Bus)]
    public void CabinetResolverUsesTypedOppositeSide(EnergizationSide sourceSide,
        EnergizationSide workSide)
    {
        (DrawingDocument drawing, SwitchDevice device) = CabinetBoundary();
        var seed = new EnergizedSeed(Guid.NewGuid(), device.Id, sourceSide);

        Assert.True(new WorkScopeBoundaryResolver().TryResolve(drawing, seed, out var anchor));
        Assert.Equal(workSide, anchor!.WorkSide);
        Assert.Equal(sourceSide == EnergizationSide.Bus
            ? device.FirstTerminalId : device.SecondTerminalId, anchor.SourceTerminalId);
        Assert.Equal(sourceSide == EnergizationSide.Bus
            ? device.SecondTerminalId : device.FirstTerminalId, anchor.WorkTerminalId);
        Assert.NotEqual(anchor.SourceNodeId, anchor.WorkNodeId);
    }

    [Theory]
    [InlineData(EnergizationSide.SmallerNumber, EnergizationSide.LargerNumber)]
    [InlineData(EnergizationSide.LargerNumber, EnergizationSide.SmallerNumber)]
    public void PoleResolverProvesBothDirections(EnergizationSide sourceSide,
        EnergizationSide workSide)
    {
        PoleChain chain = CreatePoleChain();
        var seed = new EnergizedSeed(Guid.NewGuid(), chain.A.Id, sourceSide);
        Assert.True(new WorkScopeBoundaryResolver().TryResolve(chain.Drawing, seed, out var anchor));
        Assert.Equal(workSide, anchor!.WorkSide);
        Assert.Equal(sourceSide == EnergizationSide.SmallerNumber
            ? chain.A.FirstTerminalId : chain.A.SecondTerminalId, anchor.SourceTerminalId);
        Assert.Equal(sourceSide == EnergizationSide.SmallerNumber
            ? chain.A.SecondTerminalId : chain.A.FirstTerminalId, anchor.WorkTerminalId);
    }

    [Fact]
    public void MissingPoleDirectionAndGroundSwitchCannotBeBoundaries()
    {
        PoleChain chain = CreatePoleChain(connectMiddle: false);
        OverheadLine sourceLine = chain.Drawing.OverheadLines.First();
        chain.Drawing.RemoveOverheadLine(sourceLine.ConnectionId);
        Assert.False(new WorkScopeBoundaryResolver().TryResolve(chain.Drawing,
            new EnergizedSeed(Guid.NewGuid(), chain.A.Id, EnergizationSide.SmallerNumber), out _));

        (DrawingDocument drawing, SwitchDevice load) = CabinetBoundary();
        SwitchDevice ground = drawing.Devices.OfType<RingCabinet>()
            .Single().Intervals[0].SwitchDevices.Single(item =>
                item.SwitchKind == SwitchKind.GroundSwitch);
        Assert.False(new WorkScopeBoundaryResolver().TryResolve(drawing,
            new EnergizedSeed(Guid.NewGuid(), ground.Id, EnergizationSide.Line), out _));
        Assert.NotEqual(load.Id, ground.Id);
    }

    [Fact]
    public void InternalOpenAndClosedSwitchYieldSameStructuralScope()
    {
        PoleChain chain = CreatePoleChain();
        WorkScopeDraftResult open = Generate(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, open.Status);
        Assert.Contains(chain.Inner.FirstTerminalId, open.RegionTerminalIds);
        Assert.Contains(chain.Inner.SecondTerminalId, open.RegionTerminalIds);
        Assert.DoesNotContain(chain.A.FirstTerminalId, open.RegionTerminalIds);
        Assert.DoesNotContain(chain.B.SecondTerminalId, open.RegionTerminalIds);
        Assert.Equal(2, open.BoundaryAnchors.Count);
        Assert.Contains(open.RegionDeviceSummary, item => item.Id == chain.Inner.Id);
        Assert.Contains(open.RegionDeviceSummary, item => item.Kind == "架空线");
        Assert.DoesNotContain(open.RegionDeviceSummary, item => item.Id == chain.A.Id);

        chain.Drawing.ChangeSwitchState(chain.Inner.Id, SwitchState.Closed);
        WorkScopeDraftResult closed = Generate(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, closed.Status);
        Assert.Equal(open.RegionTerminalIds, closed.RegionTerminalIds);
        Assert.Equal(open.RegionConnectionIds, closed.RegionConnectionIds);
    }

    [Theory]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void OtherOrdinarySwitchKindsDoNotCutStructuralScope(SwitchKind kind)
    {
        PoleChain chain = CreatePoleChain(innerKind: kind);
        WorkScopeDraftResult open = Generate(chain.Drawing, chain.Scenario);
        chain.Drawing.ChangeSwitchState(chain.Inner.Id, SwitchState.Closed);
        WorkScopeDraftResult closed = Generate(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, open.Status);
        Assert.Equal(open.RegionTerminalIds, closed.RegionTerminalIds);
    }

    [Fact]
    public void GroundSwitchAndEarthBranchAreExcludedFromStructuralRegion()
    {
        (DrawingDocument drawing, SwitchDevice boundary) = CabinetBoundary();
        RingCabinetInterval interval = drawing.Devices.OfType<RingCabinet>()
            .Single().Intervals[0];
        SwitchDevice ground = interval.SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.GroundSwitch);
        drawing.ChangeSwitchState(ground.Id, SwitchState.Closed);
        WorkScopeStructuralGraph graph = WorkScopeStructuralGraph.Build(drawing,
            new HashSet<Guid> { boundary.Id });
        IReadOnlySet<Guid> region = graph.FindRegion(boundary.SecondTerminalId);
        Assert.DoesNotContain(ground.SecondTerminalId, region);
        Assert.DoesNotContain(graph.Edges, edge =>
            edge.FirstTerminalId == ground.SecondTerminalId ||
            edge.SecondTerminalId == ground.SecondTerminalId);
    }

    [Fact]
    public void DisconnectedWorkSidesAreBlocked()
    {
        PoleChain chain = CreatePoleChain(connectMiddle: false);
        Assert.Equal(WorkScopeDraftStatus.DisconnectedAnchors,
            Generate(chain.Drawing, chain.Scenario).Status);
    }

    [Fact]
    public void RepeatedBoundaryCannotDefineUniqueRegion()
    {
        PoleChain chain = CreatePoleChain();
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [chain.Scenario.Seeds[0],
                new EnergizedSeed(Guid.NewGuid(), chain.A.Id,
                    EnergizationSide.SmallerNumber)], true);
        Assert.Equal(WorkScopeDraftStatus.AmbiguousRegion,
            Generate(chain.Drawing, scenario).Status);
    }

    [Fact]
    public void SourceSideReachedThroughAnotherStructuralBranchIsAmbiguous()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "bypassed boundary");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(),
            "柜 A", [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open,
                    SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice[] switches = cabinet.Intervals.Select(interval =>
            interval.SwitchDevices.Single(item => item.SwitchKind == SwitchKind.LoadSwitch)).ToArray();
        drawing.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value,
            cabinet.Intervals[2].CableTerminalId!.Value, "旁路电缆", "10kV"));
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), switches[0].Id, EnergizationSide.Line),
                new EnergizedSeed(Guid.NewGuid(), switches[1].Id, EnergizationSide.Line)], true);

        Assert.Equal(WorkScopeDraftStatus.AmbiguousRegion,
            Generate(drawing, scenario).Status);
    }

    [Fact]
    public void ThreeCabinetBoundariesUseOneUnifiedRegion()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "three sources");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(),
            "柜 A", [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open,
                    SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice[] switches = cabinet.Intervals.Select(interval =>
            interval.SwitchDevices.Single(item => item.SwitchKind == SwitchKind.LoadSwitch)).ToArray();
        var scenario = new EnergizationScenario(Guid.NewGuid(), switches.Select(device =>
            new EnergizedSeed(Guid.NewGuid(), device.Id, EnergizationSide.Line)), true);

        WorkScopeDraftResult draft = Generate(drawing, scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, draft.Status);
        Assert.Equal(3, draft.BoundaryAnchors.Count);
        Assert.All(switches, device => Assert.Contains(device.FirstTerminalId,
            draft.RegionTerminalIds));
        Assert.All(switches, device => Assert.DoesNotContain(device.SecondTerminalId,
            draft.RegionTerminalIds));
        Assert.Contains(cabinet.MainBusNodeId, draft.RegionNodeIds);
    }

    [Fact]
    public void DropoutFuseBoundaryHasPhysicalStateWarningOnly()
    {
        PoleChain chain = CreatePoleChain(boundaryKind: SwitchKind.DropoutFuse);
        WorkScopeDraftResult draft = Generate(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, draft.Status);
        Assert.Contains(draft.Warnings, item => item.Contains("熔管"));
    }

    [Fact]
    public void CableTerminationConnectsCableToOverheadWithinDraft()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "mixed path");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(),
            "柜 A", [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                    SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                    SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice a = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P03", PoleType.Cement, null, switchKinds: null, includeCableTerminal: true);
        new CreatePoleCommand(drawing, pole).Execute();
        Pole terminationPole = pole.Pole;
        CableTermination termination = pole.Devices.OfType<CableTermination>().Single();
        var p04 = new Pole(Guid.NewGuid(), "P04");
        var p05 = new Pole(Guid.NewGuid(), "P05");
        drawing.AddDevice(p04);
        drawing.AddDevice(p05);
        SwitchDevice b = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.IsolationSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(b);
        drawing.AddTerminal(new Terminal(b.FirstTerminalId, TopologyOwnerType.Device,
            b.Id, "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(b.SecondTerminalId, TopologyOwnerType.Device,
            b.Id, "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), p04.Id, b.Id));
        Terminal end = p05.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(end);
        var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value,
            termination.CableSideTerminalId, "电缆 A", "10kV");
        drawing.AddConnection(cable);
        var overhead = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            termination.OverheadSideTerminalId, b.FirstTerminalId, "架空线 A", "10kV");
        drawing.AddConnection(overhead);
        drawing.AddOverheadLine(new OverheadLine(overhead.Id, "JKLYJ",
            [terminationPole.Id, p04.Id]));
        var outer = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            b.SecondTerminalId, end.Id, "架空线 B", "10kV");
        drawing.AddConnection(outer);
        drawing.AddOverheadLine(new OverheadLine(outer.Id, "JKLYJ", [p04.Id, p05.Id]));
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), a.Id, EnergizationSide.Bus),
                new EnergizedSeed(Guid.NewGuid(), b.Id, EnergizationSide.LargerNumber)], true);

        WorkScopeDraftResult draft = Generate(drawing, scenario);
        Assert.Equal(WorkScopeDraftStatus.Success, draft.Status);
        Assert.Contains(termination.CableSideTerminalId, draft.RegionTerminalIds);
        Assert.Contains(termination.OverheadSideTerminalId, draft.RegionTerminalIds);
        Assert.Contains(termination.InternalNodeId, draft.RegionNodeIds);
        Assert.Contains(cable.Id, draft.RegionConnectionIds);
        Assert.Contains(overhead.Id, draft.RegionConnectionIds);
        Assert.Contains(draft.RegionDeviceSummary, item => item.Kind == "电缆");
        Assert.Contains(draft.RegionDeviceSummary, item => item.Kind == "架空线");
        Assert.Contains(draft.RegionDeviceSummary, item => item.Kind == "电缆终端");
    }

    [Fact]
    public void SeedCountsAndForwardOnlyCannotProduceDraft()
    {
        PoleChain chain = CreatePoleChain();
        var analysis = new EnergizationAnalysisState();
        var generator = new WorkScopeDraftGenerator();
        var empty = new EnergizationScenario(Guid.NewGuid());
        Assert.Equal(WorkScopeDraftStatus.NoSeeds,
            generator.Generate(chain.Drawing, empty, analysis).Status);
        var one = new EnergizationScenario(Guid.NewGuid(), [chain.Scenario.Seeds[0]], true);
        Assert.Equal(WorkScopeDraftStatus.TooFewBoundaries,
            generator.Generate(chain.Drawing, one, analysis).Status);
        chain.Scenario.SetSourceSetComplete(false);
        analysis.Execute(chain.Drawing, chain.Scenario);
        Assert.Equal(EnergizationValidity.ForwardOnly, analysis.CurrentResult!.Validity);
        Assert.Equal(WorkScopeDraftStatus.SourceSetIncomplete,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
    }

    [Fact]
    public void PreconditionsAndEnergizedInteriorBlockDraft()
    {
        PoleChain chain = CreatePoleChain();
        var analysis = new EnergizationAnalysisState();
        var generator = new WorkScopeDraftGenerator();
        Assert.Equal(WorkScopeDraftStatus.ResultNotCurrent,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
        analysis.Execute(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.Success,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
        chain.Drawing.ChangeSwitchState(chain.A.Id, SwitchState.Closed);
        analysis.Execute(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.EnergizedInsideScope,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);

        chain.Scenario.SetSourceSetComplete(false);
        Assert.Equal(WorkScopeDraftStatus.SourceSetIncomplete,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
        chain.Scenario.SetSourceSetComplete(true);
        analysis.Invalidate();
        Assert.Equal(WorkScopeDraftStatus.ResultNotCurrent,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
        analysis.Execute(chain.Drawing, chain.Scenario);
        chain.Scenario.AddSeed(new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(),
            EnergizationSide.Bus));
        chain.Scenario.SetSourceSetComplete(true);
        analysis.Execute(chain.Drawing, chain.Scenario);
        Assert.Equal(WorkScopeDraftStatus.BoundaryResolutionFailed,
            generator.Generate(chain.Drawing, chain.Scenario, analysis).Status);
    }

    [Fact]
    public void DraftFreshnessTracksScenarioTopologyAndAnalysisIdentity()
    {
        PoleChain chain = CreatePoleChain();
        var analysis = new EnergizationAnalysisState();
        analysis.Execute(chain.Drawing, chain.Scenario);
        var generator = new WorkScopeDraftGenerator();
        int deviceCount = chain.Drawing.Devices.Count;
        int scopeCount = chain.Drawing.WorkScopes.Count;
        WorkScopeDraftResult first = generator.Generate(chain.Drawing, chain.Scenario, analysis);
        Assert.True(first.IsCurrent(chain.Drawing, chain.Scenario, analysis));
        Assert.Equal(deviceCount, chain.Drawing.Devices.Count);
        Assert.Equal(scopeCount, chain.Drawing.WorkScopes.Count);

        chain.Drawing.ChangeSwitchState(chain.Inner.Id, SwitchState.Closed);
        Assert.False(first.IsCurrent(chain.Drawing, chain.Scenario, analysis));
        analysis.Execute(chain.Drawing, chain.Scenario);
        WorkScopeDraftResult second = generator.Generate(chain.Drawing, chain.Scenario, analysis);
        Assert.NotEqual(first.DraftId, second.DraftId);
        Assert.True(second.IsCurrent(chain.Drawing, chain.Scenario, analysis));
        analysis.Execute(chain.Drawing, chain.Scenario);
        Assert.False(second.IsCurrent(chain.Drawing, chain.Scenario, analysis));
        WorkScopeDraftResult third = generator.Generate(chain.Drawing, chain.Scenario, analysis);
        chain.Scenario.RemoveSeed(chain.Scenario.Seeds[0].Id);
        Assert.False(third.IsCurrent(chain.Drawing, chain.Scenario, analysis));
    }

    [Fact]
    public void RemovingAConnectionInvalidatesTheDraft()
    {
        PoleChain chain = CreatePoleChain();
        var analysis = new EnergizationAnalysisState();
        analysis.Execute(chain.Drawing, chain.Scenario);
        WorkScopeDraftResult draft = new WorkScopeDraftGenerator()
            .Generate(chain.Drawing, chain.Scenario, analysis);
        Assert.True(draft.IsCurrent(chain.Drawing, chain.Scenario, analysis));

        OverheadLine line = chain.Drawing.OverheadLines.First();
        chain.Drawing.RemoveOverheadLine(line.ConnectionId);
        chain.Drawing.RemoveConnection(line.ConnectionId);
        Assert.False(draft.IsCurrent(chain.Drawing, chain.Scenario, analysis));
    }

    [Fact]
    public void SafetyValidatorRejectsPreciseUnknownTerminalAndNode()
    {
        PoleChain chain = CreatePoleChain();
        EnergizationResult real = new EnergizationAnalyzer().Analyze(chain.Drawing, chain.Scenario);
        Guid terminalId = chain.Inner.FirstTerminalId;
        var terminals = real.Terminals.ToDictionary(item => item.Key, item => item.Value);
        terminals[terminalId] = new EnergizationPointResult(EnergizationState.Unknown, []);
        EnergizationResult unknown = new(EnergizationValidity.Complete, terminals,
            real.Nodes, [], [], []);
        Assert.Equal(WorkScopeDraftStatus.UnknownInsideScope,
            WorkScopeRegionSafetyValidator.Check(chain.Drawing,
                new HashSet<Guid> { terminalId }, [], unknown).Status);

        (DrawingDocument cabinetDrawing, SwitchDevice cabinetSwitch) = CabinetBoundary();
        Guid nodeId = cabinetDrawing.Terminals.Single(item =>
            item.Id == cabinetSwitch.SecondTerminalId).ElectricalNodeId!.Value;
        var nodeResults = new Dictionary<Guid, EnergizationPointResult>
        {
            [nodeId] = new(EnergizationState.Energized, [])
        };
        EnergizationResult energizedNode = new(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>(), nodeResults, [], [], []);
        Assert.Equal(WorkScopeDraftStatus.EnergizedInsideScope,
            WorkScopeRegionSafetyValidator.Check(cabinetDrawing, new HashSet<Guid>(), [nodeId],
                energizedNode).Status);

        nodeResults[nodeId] = new EnergizationPointResult(EnergizationState.Unknown, []);
        EnergizationResult unknownNode = new(EnergizationValidity.Complete,
            new Dictionary<Guid, EnergizationPointResult>(), nodeResults, [], [], []);
        Assert.Equal(WorkScopeDraftStatus.UnknownInsideScope,
            WorkScopeRegionSafetyValidator.Check(cabinetDrawing, new HashSet<Guid>(), [nodeId],
                unknownNode).Status);
    }

    [Fact]
    public void ExplicitEnergizedAndGroundedTerminalHasHardBlockDiagnostic()
    {
        PoleChain chain = CreatePoleChain();
        Guid terminalId = chain.Inner.FirstTerminalId;
        chain.Drawing.CreateGroundingPoint(Guid.NewGuid(), terminalId, "P03 处");
        EnergizationResult actual = new EnergizationAnalyzer().Analyze(chain.Drawing,
            chain.Scenario);
        var terminals = actual.Terminals.ToDictionary(item => item.Key, item => item.Value);
        terminals[terminalId] = new EnergizationPointResult(EnergizationState.Energized, []);
        EnergizationResult conflict = new(EnergizationValidity.Complete, terminals,
            actual.Nodes, [], [], []);
        var check = WorkScopeRegionSafetyValidator.Check(chain.Drawing,
            new HashSet<Guid> { terminalId }, [], conflict);
        Assert.Equal(WorkScopeDraftStatus.EnergizedInsideScope, check.Status);
        Assert.Contains("接地", check.Diagnostic);
    }

    private static WorkScopeDraftResult Generate(DrawingDocument drawing,
        EnergizationScenario scenario)
    {
        var analysis = new EnergizationAnalysisState();
        analysis.Execute(drawing, scenario);
        return new WorkScopeDraftGenerator().Generate(drawing, scenario, analysis);
    }

    private static (DrawingDocument, SwitchDevice) CabinetBoundary()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "cabinet");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(),
            "柜 A", [RingCabinetIntervalDefinition.CreateLoadSwitch(1,
                SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2,
                    SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        return (drawing, cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch));
    }

    private sealed record PoleChain(DrawingDocument Drawing, SwitchDevice A,
        SwitchDevice Inner, SwitchDevice B, EnergizationScenario Scenario);

    private static PoleChain CreatePoleChain(bool connectMiddle = true,
        SwitchKind innerKind = SwitchKind.IsolationSwitch,
        SwitchKind boundaryKind = SwitchKind.IsolationSwitch)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "pole chain");
        Pole[] poles = Enumerable.Range(1, 5).Select(number =>
            new Pole(Guid.NewGuid(), $"P{number:00}")).ToArray();
        foreach (Pole pole in poles) drawing.AddDevice(pole);
        Terminal left = poles[0].CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal right = poles[4].CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(left);
        drawing.AddTerminal(right);

        SwitchDevice CreateSwitch(int poleIndex, SwitchKind kind)
        {
            SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), kind,
                Guid.NewGuid(), Guid.NewGuid(), SwitchState.Open, $"P{poleIndex + 1:00} switch");
            drawing.AddDevice(device);
            drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
                device.Id, "First", "10kV", true, false, null,
                [ConnectionType.OverheadLine]));
            drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
                device.Id, "Second", "10kV", true, false, null,
                [ConnectionType.OverheadLine]));
            drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), poles[poleIndex].Id,
                device.Id));
            return device;
        }
        SwitchDevice a = CreateSwitch(1, boundaryKind);
        SwitchDevice inner = CreateSwitch(2, innerKind);
        SwitchDevice b = CreateSwitch(3, SwitchKind.IsolationSwitch);

        void Connect(Guid start, Guid end, int firstPole, int secondPole)
        {
            var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
                start, end, $"P{firstPole + 1:00}-P{secondPole + 1:00}", "10kV");
            drawing.AddConnection(connection);
            drawing.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ",
                [poles[firstPole].Id, poles[secondPole].Id]));
        }
        Connect(left.Id, a.FirstTerminalId, 0, 1);
        Connect(a.SecondTerminalId, inner.FirstTerminalId, 1, 2);
        if (connectMiddle) Connect(inner.SecondTerminalId, b.FirstTerminalId, 2, 3);
        else
        {
            Terminal separate = poles[2].CreateOverheadAnchorTerminal(Guid.NewGuid());
            drawing.AddTerminal(separate);
            Connect(separate.Id, b.FirstTerminalId, 2, 3);
        }
        Connect(b.SecondTerminalId, right.Id, 3, 4);
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), a.Id, EnergizationSide.SmallerNumber),
             new EnergizedSeed(Guid.NewGuid(), b.Id, EnergizationSide.LargerNumber)], true);
        return new PoleChain(drawing, a, inner, b, scenario);
    }
}
