using DistributionDrawing.TestSupport;
using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkScopeCandidateProjectorTests
{
    [Fact]
    public void Project_UsesCurrentResultBuildsDeenergizedRegionsAndExcludesEarth()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice seedSwitch = LoadSwitch(cabinet.Intervals[0]);
        EnergizationAnalysisState state = Analyze(drawing, seedSwitch, EnergizationSide.Bus);
        EnergizationResult result = Assert.IsType<EnergizationResult>(state.CurrentResult);

        WorkScopeCandidateProjection projection = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        WorkScopeCandidate candidate = Assert.IsType<WorkScopeCandidate>(projection.Candidate);
        Assert.NotEmpty(candidate.Regions);
        Assert.All(candidate.Regions, region =>
        {
            Assert.Equal(region.TerminalIds.Order(), region.TerminalIds);
            Assert.Equal(region.ElectricalNodeIds.Order(), region.ElectricalNodeIds);
            Assert.All(region.TerminalIds, id =>
                Assert.Equal(EnergizationState.Deenergized, result.Terminals[id].State));
            Assert.All(region.ElectricalNodeIds, id =>
                Assert.Equal(EnergizationState.Deenergized, result.Nodes[id].State));
        });

        HashSet<Guid> earthNodeIds = drawing.ElectricalNodes
            .Where(node => node.Type == ElectricalNodeType.Earth)
            .Select(node => node.Id).ToHashSet();
        HashSet<Guid> earthTerminalIds = drawing.Terminals
            .Where(terminal => terminal.ElectricalNodeId is Guid nodeId && earthNodeIds.Contains(nodeId))
            .Select(terminal => terminal.Id).ToHashSet();
        Assert.DoesNotContain(candidate.Regions.SelectMany(region => region.TerminalIds), earthTerminalIds.Contains);
        Assert.DoesNotContain(candidate.Regions.SelectMany(region => region.ElectricalNodeIds), earthNodeIds.Contains);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(projection.Diagnostics);
    }

    [Fact]
    public void Project_RecordsOpenSwitchEnergizationTransitionWithoutWtaBoundary()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open));
        SwitchDevice boundarySwitch = LoadSwitch(cabinet.Intervals[0]);
        EnergizationAnalysisState state = Analyze(drawing, boundarySwitch, EnergizationSide.Bus);
        EnergizationResult ea = state.CurrentResult!;

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        WorkScopeCandidateBoundary boundary = Assert.Single(candidate.Boundaries,
            item => item.SwitchDeviceId == boundarySwitch.Id);
        Assert.Equal(boundarySwitch.SecondTerminalId, boundary.DeenergizedTerminalId);
        Assert.Equal(boundarySwitch.FirstTerminalId, boundary.EnergizedTerminalId);
        Assert.Equal(boundarySwitch.ParentId, boundary.TopologyParentId);
        Assert.DoesNotContain(ea.ConductingEdges, edge => edge.SourceId == boundarySwitch.Id);
    }

    [Fact]
    public void Project_ProducesOneDeenergizedRegionWithMultipleBoundaries()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice[] switches = cabinet.Intervals.Select(LoadSwitch).ToArray();
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            switches.Select(device => Seed(device, EnergizationSide.Line)), true));
        EnergizationResult result = Assert.IsType<EnergizationResult>(state.CurrentResult);
        var projector = new WorkScopeCandidateProjector();

        WorkScopeCandidateProjection projection = projector.Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        Assert.True(projection.IsValid);
        WorkScopeCandidate candidate = Assert.IsType<WorkScopeCandidate>(projection.Candidate);
        WorkScopeCandidateRegion region = Assert.Single(candidate.Regions);
        ElectricalNode bus = drawing.ElectricalNodes.Single(node => node.Id == cabinet.MainBusNodeId);
        Assert.Equal(bus.TerminalIds.Order(), region.TerminalIds);
        Assert.Equal([bus.Id], region.ElectricalNodeIds);
        Assert.Equal(EnergizationState.Deenergized, result.Nodes[bus.Id].State);
        Assert.Equal(2, candidate.Boundaries.Count);
        Assert.Equal(switches.Select(device => device.Id).Order(),
            candidate.Boundaries.Select(boundary => boundary.SwitchDeviceId));
        Assert.Equal(2, candidate.Boundaries.Select(boundary =>
            (boundary.SwitchDeviceId, boundary.DeenergizedTerminalId, boundary.EnergizedTerminalId))
            .Distinct().Count());
        foreach (SwitchDevice device in switches)
        {
            WorkScopeCandidateBoundary boundary = Assert.Single(candidate.Boundaries,
                item => item.SwitchDeviceId == device.Id);
            Assert.Equal(device.FirstTerminalId, boundary.DeenergizedTerminalId);
            Assert.Equal(device.SecondTerminalId, boundary.EnergizedTerminalId);
            Assert.Equal(device.ParentId, boundary.TopologyParentId);
            Assert.Contains(boundary.DeenergizedTerminalId, region.TerminalIds);
            Assert.DoesNotContain(boundary.EnergizedTerminalId, region.TerminalIds);
            Assert.Equal(EnergizationState.Deenergized, result.Terminals[device.FirstTerminalId].State);
            Assert.Equal(EnergizationState.Energized, result.Terminals[device.SecondTerminalId].State);
        }
        Assert.Empty(candidate.Diagnostics);
        Assert.Equal(Signature(candidate), Signature(projector.Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!));
    }

    [Theory]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.CircuitBreaker)]
    public void Project_RecordsIntegratedFeederSwitchTransition(SwitchKind boundaryKind)
    {
        SwitchState isolationState = boundaryKind == SwitchKind.IsolationSwitch
            ? SwitchState.Open : SwitchState.Closed;
        SwitchState breakerState = boundaryKind == SwitchKind.CircuitBreaker
            ? SwitchState.Open : SwitchState.Closed;
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(1,
                GroundingStructureKind.UpperIsolationGrounding,
                isolationState, breakerState, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice seedSwitch = interval.SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.IsolationSwitch);
        SwitchDevice boundarySwitch = interval.SwitchDevices.Single(item => item.SwitchKind == boundaryKind);
        EnergizationAnalysisState state = Analyze(drawing, seedSwitch, EnergizationSide.Bus);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.Equal(seedSwitch.Id, Assert.Single(candidate.SelectedSeeds).BoundaryDeviceId);
        if (boundaryKind == SwitchKind.IsolationSwitch)
            Assert.Contains(candidate.Boundaries, item => item.SwitchDeviceId == seedSwitch.Id &&
                item.DeenergizedTerminalId == seedSwitch.SecondTerminalId);
        else
            Assert.DoesNotContain(candidate.Boundaries, item => item.SwitchDeviceId == boundarySwitch.Id);
    }

    [Fact]
    public void Project_RecordsPoleSwitchTransitionWithBothTerminalIdentities()
    {
        (DrawingDocument drawing, SwitchDevice boundarySwitch) = PoleSwitch();
        EnergizationAnalysisState state = Analyze(drawing, boundarySwitch, EnergizationSide.SmallerNumber);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.Contains(candidate.Boundaries, item => item.SwitchDeviceId == boundarySwitch.Id &&
            item.DeenergizedTerminalId == boundarySwitch.SecondTerminalId &&
            item.EnergizedTerminalId == boundarySwitch.FirstTerminalId &&
            item.AttachedPoleId == drawing.PoleAttachments.Single(item =>
                item.AttachedDeviceId == boundarySwitch.Id).PoleId &&
            item.RelatedConnectionIds.Count == 2);
        Connection downstreamConnection = drawing.Connections.Single(connection =>
            connection.UsesTerminal(boundarySwitch.SecondTerminalId));
        Guid downstreamTerminal = downstreamConnection.StartTerminalId == boundarySwitch.SecondTerminalId
            ? downstreamConnection.EndTerminalId : downstreamConnection.StartTerminalId;
        Assert.Contains(candidate.Regions, region => region.TerminalIds.Contains(downstreamTerminal));
    }

    [Fact]
    public void Project_TraversesCableConnectionAndCableTerminationInternalPath()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P-100", PoleType.Cement, null, switchKinds: null, includeCableTerminal: true);
        new CreatePoleCommand(drawing, pole).Execute();
        CableTermination termination = Assert.Single(pole.Devices.OfType<CableTermination>());
        var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[1].CableTerminalId!.Value,
            termination.CableSideTerminalId, "cable", "10kV");
        drawing.AddConnection(cable);
        EnergizationAnalysisState state = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        WorkScopeCandidateRegion region = Assert.Single(candidate.Regions, item =>
            item.TerminalIds.Contains(termination.CableSideTerminalId));
        Assert.Contains(termination.OverheadSideTerminalId, region.TerminalIds);
        Assert.Contains(termination.InternalNodeId, region.ElectricalNodeIds);
        Assert.Contains(cable.Id, state.CurrentResult!.ConductingEdges.Select(edge => edge.SourceId));
    }

    [Fact]
    public void Project_IncludesTransformerHvLeafAsOneTerminalWithoutInventingNodes()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open));
        Guid transformerId = Guid.NewGuid();
        Guid hvTerminalId = Guid.NewGuid();
        var transformer = new Transformer(transformerId, TransformerKind.PublicIndoor,
            hvTerminalId, "HV leaf");
        var hvTerminal = new Terminal(hvTerminalId, TopologyOwnerType.Device, transformerId,
            Transformer.HvTerminalRole, Transformer.TenKilovolts, true, false, null,
            [ConnectionType.Cable]);
        drawing.AddTransformer(transformer, hvTerminal);
        EnergizationAnalysisState state = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.Contains(candidate.Regions, region => region.TerminalIds.SequenceEqual([hvTerminalId]) &&
            region.ElectricalNodeIds.Count == 0);
    }

    [Fact]
    public void Project_KeepsCustomerStationFeedersIndependent()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open));
        var station = new CustomerStationCreationFactory().Create(StationKind.IndoorStation,
            ["Feeder A", "Feeder B"]);
        drawing.AddCustomerStation(station);
        EnergizationAnalysisState state = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        foreach (IncomingFeeder feeder in station.IncomingFeeders)
        {
            Assert.Contains(candidate.Regions, region => region.TerminalIds.Contains(feeder.CableTerminalId));
            Assert.Contains(candidate.Regions, region => region.TerminalIds.Contains(feeder.StationTerminalId));
            Assert.DoesNotContain(candidate.Boundaries, boundary =>
                boundary.SwitchDeviceId == feeder.IsolationSwitch.Id);
        }
        Assert.DoesNotContain(candidate.Regions, region =>
            station.IncomingFeeders.All(feeder => region.TerminalIds.Contains(feeder.StationTerminalId)));
    }

    [Fact]
    public void Project_ProjectsCustomerStationSingleFeederUsingItsTerminalAndNodeIdentities()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open));
        CustomerStation station = new CustomerStationCreationFactory().Create(StationKind.BoxStation,
            ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value, feeder.CableTerminalId, "Feeder A", "10kV");
        drawing.AddConnection(cable);
        EnergizationAnalysisState state = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus);
        EnergizationResult result = Assert.IsType<EnergizationResult>(state.CurrentResult);

        WorkScopeCandidateProjection projection = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        Assert.True(projection.IsValid);
        WorkScopeCandidate candidate = Assert.IsType<WorkScopeCandidate>(projection.Candidate);
        Terminal cableTerminal = drawing.Terminals.Single(terminal => terminal.Id == feeder.CableTerminalId);
        Terminal stationTerminal = drawing.Terminals.Single(terminal => terminal.Id == feeder.StationTerminalId);
        ElectricalNode node = drawing.ElectricalNodes.Single(item => item.Id == feeder.ElectricalNodeId);
        Assert.Equal(feeder.IsolationSwitch.Id, cableTerminal.OwnerId);
        Assert.Equal(feeder.IsolationSwitch.Id, stationTerminal.OwnerId);
        Assert.Equal("CableTerminal", cableTerminal.Role);
        Assert.Equal("StationTerminal", stationTerminal.Role);
        Assert.Null(cableTerminal.ElectricalNodeId);
        Assert.Equal(node.Id, stationTerminal.ElectricalNodeId);
        Assert.Equal(feeder.IncomingFeederId, node.OwnerId);
        Assert.Equal([stationTerminal.Id], node.TerminalIds);
        Assert.Equal(EnergizationState.Energized, result.Terminals[cableTerminal.Id].State);
        Assert.Equal(EnergizationState.Deenergized, result.Terminals[stationTerminal.Id].State);
        Assert.Equal(EnergizationState.Deenergized, result.Nodes[node.Id].State);
        Assert.Contains(result.ConductingEdges, edge => edge.SourceId == cable.Id &&
            edge.Connects(cable.StartTerminalId, cable.EndTerminalId));
        Assert.Equal(SwitchState.Open, feeder.IsolationSwitch.SwitchState);
        Assert.Equal(cableTerminal.Id, feeder.IsolationSwitch.FirstTerminalId);
        Assert.Equal(stationTerminal.Id, feeder.IsolationSwitch.SecondTerminalId);
        Assert.DoesNotContain(result.ConductingEdges, edge => edge.SourceId == feeder.IsolationSwitch.Id);
        WorkScopeCandidateRegion region = Assert.Single(candidate.Regions);
        Assert.Equal([stationTerminal.Id], region.TerminalIds);
        Assert.Equal([node.Id], region.ElectricalNodeIds);
        Assert.Empty(candidate.Boundaries);
        Assert.DoesNotContain(candidate.SelectedSeeds, seed => seed.BoundaryDeviceId == feeder.IsolationSwitch.Id);
        Assert.Empty(candidate.Diagnostics);
    }

    [Fact]
    public void Project_FindsDisconnectedDeenergizedComponentsAndIsolatedTerminals()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open, SwitchState.Open));
        SwitchDevice isolatedSwitch = AddIsolatedOpenSwitch(drawing);
        SwitchDevice seedSwitch = LoadSwitch(cabinet.Intervals[0]);
        EnergizationAnalysisState state = Analyze(drawing, seedSwitch, EnergizationSide.Bus);
        EnergizationResult result = state.CurrentResult!;

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.True(candidate.Regions.Count >= 3);
        Assert.Contains(candidate.Regions, region => region.TerminalIds.Count == 1 &&
            result.Terminals[region.TerminalIds[0]].State == EnergizationState.Deenergized);
        Assert.Contains(candidate.Regions, region => region.TerminalIds.SequenceEqual([isolatedSwitch.FirstTerminalId]));
        Assert.Contains(candidate.Regions, region => region.TerminalIds.SequenceEqual([isolatedSwitch.SecondTerminalId]));
        for (int left = 0; left < candidate.Regions.Count; left++)
        for (int right = left + 1; right < candidate.Regions.Count; right++)
            Assert.Empty(candidate.Regions[left].TerminalIds.Intersect(candidate.Regions[right].TerminalIds));
    }

    [Fact]
    public void Project_ReturnsValidEmptyCandidateWhenAllBusinessPointsAreEnergized()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open));
        SwitchDevice seedSwitch = LoadSwitch(cabinet.Intervals[0]);
        EnergizationResult analyzed = Analyze(drawing, seedSwitch, EnergizationSide.Bus).CurrentResult!;
        var allEnergized = new EnergizationResult(EnergizationValidity.Complete,
            analyzed.Terminals.ToDictionary(pair => pair.Key, pair =>
                new EnergizationPointResult(IsEarthTerminal(drawing, pair.Key)
                    ? EnergizationState.Deenergized : EnergizationState.Energized, [])),
            analyzed.Nodes.ToDictionary(pair => pair.Key, pair =>
                new EnergizationPointResult(drawing.ElectricalNodes.Single(node => node.Id == pair.Key).Type ==
                    ElectricalNodeType.Earth ? EnergizationState.Deenergized : EnergizationState.Energized, [])),
            [], analyzed.ConductingEdges, analyzed.GroundingSwitchConnections);
        var state = new EnergizationAnalysisState();
        state.Publish(allEnergized);

        WorkScopeCandidateProjection projection = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        Assert.True(projection.IsValid);
        Assert.True(projection.Candidate!.IsEmpty);
        Assert.Contains(projection.Diagnostics, item => item.Code ==
            WorkScopeCandidateDiagnosticCode.EmptyCandidate);
    }

    [Fact]
    public void Project_ProducesWholeDeenergizedGraphWithoutBoundaryTransition()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open));
        EnergizationResult analyzed = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]),
            EnergizationSide.Bus).CurrentResult!;
        var allDeenergized = new EnergizationResult(EnergizationValidity.Complete,
            analyzed.Terminals.ToDictionary(pair => pair.Key, pair =>
                new EnergizationPointResult(EnergizationState.Deenergized, [])),
            analyzed.Nodes.ToDictionary(pair => pair.Key, pair =>
                new EnergizationPointResult(EnergizationState.Deenergized, [])),
            [], analyzed.ConductingEdges, analyzed.GroundingSwitchConnections);
        var state = new EnergizationAnalysisState();
        state.Publish(allDeenergized);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.NotEmpty(candidate.Regions);
        Assert.Empty(candidate.Boundaries);
        Assert.All(candidate.Regions.SelectMany(region => region.TerminalIds), id =>
            Assert.Equal(EnergizationState.Deenergized, allDeenergized.Terminals[id].State));
    }

    [Fact]
    public void Project_RejectsAbsentFailedNoSeedAndStaleResults()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open));
        var projector = new WorkScopeCandidateProjector();

        Assert.Equal(WorkScopeCandidateDiagnosticCode.CurrentResultUnavailable,
            Assert.Single(projector.Project(drawing, new EnergizationAnalysisState(), WorkScopeAnalysisFixture.ScenarioFor(new EnergizationAnalysisState())).Diagnostics).Code);

        var noSeedState = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(noSeedState, drawing, new EnergizationScenario(Guid.NewGuid(), []));
        Assert.Equal(WorkScopeCandidateDiagnosticCode.NoSeeds,
            Assert.Single(projector.Project(drawing, noSeedState, WorkScopeAnalysisFixture.ScenarioFor(noSeedState)).Diagnostics).Code);

        var failedState = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(failedState, drawing, new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), Guid.NewGuid(), EnergizationSide.Bus)]));
        Assert.Equal(WorkScopeCandidateDiagnosticCode.FailedAnalysis,
            Assert.Single(projector.Project(drawing, failedState, WorkScopeAnalysisFixture.ScenarioFor(failedState)).Diagnostics).Code);

        var staleState = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(staleState, drawing, new EnergizationScenario(Guid.NewGuid(),
            [Seed(LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Bus)]));
        staleState.Invalidate();
        Assert.Equal(WorkScopeCandidateDiagnosticCode.StaleAnalysis,
            Assert.Single(projector.Project(drawing, staleState, WorkScopeAnalysisFixture.ScenarioFor(staleState)).Diagnostics).Code);
    }

    [Fact]
    public void Project_RejectsResultWhosePointIdentitiesDoNotMatchDrawing()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open));
        EnergizationResult valid = Analyze(drawing, LoadSwitch(cabinet.Intervals[0]),
            EnergizationSide.Bus).CurrentResult!;
        var mismatch = new EnergizationResult(EnergizationValidity.Complete,
            valid.Terminals.Where(pair => pair.Key != valid.Terminals.Keys.First())
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            valid.Nodes, valid.Diagnostics, valid.ConductingEdges, valid.GroundingSwitchConnections);
        var state = new EnergizationAnalysisState();
        state.Publish(mismatch);

        WorkScopeCandidateProjection projection = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        Assert.False(projection.IsValid);
        Assert.Contains(projection.Diagnostics, item => item.Code == WorkScopeCandidateDiagnosticCode.IdentityMismatch);
    }

    [Fact]
    public void Project_ReportsEnergizedDeenergizedConductingEdgeAsConsistencyDiagnostic()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice loadSwitch = LoadSwitch(interval);
        EnergizationResult analyzed = Analyze(drawing, loadSwitch, EnergizationSide.Bus).CurrentResult!;
        var terminalResults = analyzed.Terminals.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (Terminal terminal in drawing.Terminals.Where(item => item.ElectricalNodeId == interval.CircuitNodeId))
            terminalResults[terminal.Id] = new EnergizationPointResult(EnergizationState.Deenergized, []);
        var nodeResults = analyzed.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodeResults[interval.CircuitNodeId] = new EnergizationPointResult(EnergizationState.Deenergized, []);
        var inconsistent = new EnergizationResult(EnergizationValidity.Complete,
            terminalResults, nodeResults, [], analyzed.ConductingEdges, analyzed.GroundingSwitchConnections);
        var state = new EnergizationAnalysisState();
        state.Publish(inconsistent);

        WorkScopeCandidateProjection projection = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state));

        Assert.False(projection.IsValid);
        Assert.Contains(projection.Diagnostics, item => item.Code ==
            WorkScopeCandidateDiagnosticCode.ConductingEdgeStateMismatch && item.Identity == loadSwitch.Id);
    }

    [Fact]
    public void Project_IgnoresGroundSwitchToEarthTransition()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Closed));
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice groundSwitch = interval.SwitchDevices.Single(item => item.SwitchKind == SwitchKind.GroundSwitch);
        EnergizationAnalysisState state = Analyze(drawing, LoadSwitch(interval), EnergizationSide.Bus);

        WorkScopeCandidate candidate = new WorkScopeCandidateProjector().Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.DoesNotContain(candidate.Boundaries, item => item.SwitchDeviceId == groundSwitch.Id);
        Assert.DoesNotContain(candidate.Regions.SelectMany(region => region.ElectricalNodeIds),
            id => drawing.ElectricalNodes.Single(node => node.Id == id).Type == ElectricalNodeType.Earth);
    }

    [Fact]
    public void Project_IsDeterministicAcrossSeedOrderingAndDoesNotMutateDocumentOrEaState()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open));
        AddIsolatedOpenSwitch(drawing);
        EnergizedSeed first = Seed(LoadSwitch(cabinet.Intervals[0]), EnergizationSide.Line);
        EnergizedSeed second = Seed(LoadSwitch(cabinet.Intervals[1]), EnergizationSide.Line);
        var scenario = new EnergizationScenario(Guid.NewGuid(), [first, second], true);
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, scenario);
        EnergizationResult beforeResult = state.CurrentResult!;
        int workScopeCount = drawing.WorkScopes.Count;
        SwitchState?[] switchStates = drawing.Devices.OfType<SwitchDevice>().Select(item => item.SwitchState).ToArray();
        var projector = new WorkScopeCandidateProjector();

        WorkScopeCandidate one = projector.Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;
        WorkScopeCandidate two = projector.Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;

        Assert.Equal(Signature(one), Signature(two));
        WorkScope persisted = WorkScope.Create(Guid.NewGuid(),
            [new WorkScopeRegion(one.Regions[0].TerminalIds, one.Regions[0].ElectricalNodeIds)], [], "historical");
        drawing.AddWorkScope(persisted);
        WorkScopeCandidate afterHistoricalSnapshot = projector.Project(drawing, state, WorkScopeAnalysisFixture.ScenarioFor(state)).Candidate!;
        Assert.Equal(Signature(one), Signature(afterHistoricalSnapshot));
        Assert.Same(beforeResult, state.CurrentResult);
        Assert.Equal(workScopeCount + 1, drawing.WorkScopes.Count);
        Assert.Equal(switchStates, drawing.Devices.OfType<SwitchDevice>().Select(item => item.SwitchState));

        var reversedState = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(reversedState, drawing, new EnergizationScenario(Guid.NewGuid(), [second, first], true));
        Assert.Equal(Signature(one), Signature(projector.Project(drawing, reversedState, WorkScopeAnalysisFixture.ScenarioFor(reversedState)).Candidate!));
    }

    [Fact]
    public void Project_IsStructurallyDeterministicAcrossDeviceAndConnectionCreationOrder()
    {
        var first = CreationOrderDrawing(reverse: false);
        var second = CreationOrderDrawing(reverse: true);
        Assert.Equal(["source", "downstream"], first.Drawing.Devices.OfType<RingCabinet>()
            .Select(device => first.Names[device.Id]));
        Assert.Equal(["downstream", "source"], second.Drawing.Devices.OfType<RingCabinet>()
            .Select(device => second.Names[device.Id]));
        Assert.Equal(["cable.1", "cable.2"], first.Drawing.Connections.Select(item => first.Names[item.Id]));
        Assert.Equal(["cable.2", "cable.1"], second.Drawing.Connections.Select(item => second.Names[item.Id]));
        Assert.Equal(TopologyAndEaSignature(first.Drawing, first.State.CurrentResult!, first.Names),
            TopologyAndEaSignature(second.Drawing, second.State.CurrentResult!, second.Names));
        var projector = new WorkScopeCandidateProjector();

        WorkScopeCandidateProjection firstProjection = projector.Project(first.Drawing, first.State, WorkScopeAnalysisFixture.ScenarioFor(first.State));
        WorkScopeCandidateProjection secondProjection = projector.Project(second.Drawing, second.State, WorkScopeAnalysisFixture.ScenarioFor(second.State));

        Assert.True(firstProjection.IsValid);
        Assert.True(secondProjection.IsValid);
        WorkScopeCandidate firstCandidate = Assert.IsType<WorkScopeCandidate>(firstProjection.Candidate);
        WorkScopeCandidate secondCandidate = Assert.IsType<WorkScopeCandidate>(secondProjection.Candidate);
        Assert.Single(firstCandidate.Regions);
        Assert.NotEmpty(firstCandidate.Regions[0].TerminalIds);
        Assert.NotEmpty(firstCandidate.Regions[0].ElectricalNodeIds);
        Assert.Equal(2, firstCandidate.Boundaries.Count);
        Assert.Empty(firstCandidate.Diagnostics);
        Assert.Empty(secondCandidate.Diagnostics);
        Assert.Equal(NormalizedSignature(firstCandidate, first.Names),
            NormalizedSignature(secondCandidate, second.Names));
    }

    private static (DrawingDocument Drawing, EnergizationAnalysisState State, Dictionary<Guid, string> Names)
        CreationOrderDrawing(bool reverse)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Creation order");
        var cabinets = new Dictionary<string, RingCabinet>();
        var names = new Dictionary<Guid, string>();
        foreach (string role in reverse ? new[] { "downstream", "source" } : ["source", "downstream"])
        {
            SwitchState switchState = role == "source" ? SwitchState.Closed : SwitchState.Open;
            RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), role,
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1, switchState, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2, switchState, SwitchState.Open)]));
            drawing.AddDevice(cabinet);
            cabinets.Add(role, cabinet);
            names.Add(cabinet.Id, role);
            names.Add(cabinet.MainBusNodeId, $"{role}.bus");
            foreach (RingCabinetInterval interval in cabinet.Intervals)
            {
                string intervalRole = $"{role}.interval.{interval.Sequence}";
                names.Add(interval.IntervalId, intervalRole);
                names.Add(interval.CircuitNodeId, $"{intervalRole}.circuit");
                names.Add(interval.EarthNodeId, $"{intervalRole}.earth");
                foreach (SwitchDevice device in interval.SwitchDevices)
                    names.Add(device.Id, $"{intervalRole}.{device.SwitchKind}");
            }
            foreach (Terminal terminal in cabinet.Terminals)
                names.Add(terminal.Id, $"{names[terminal.OwnerId]}.{terminal.Role}");
        }
        foreach (int sequence in reverse ? new[] { 2, 1 } : [1, 2])
        {
            var cable = new Connection(Guid.NewGuid(), ConnectionType.Cable,
                cabinets["source"].Intervals[sequence - 1].CableTerminalId!.Value,
                cabinets["downstream"].Intervals[sequence - 1].CableTerminalId!.Value,
                $"cable.{sequence}", "10kV");
            drawing.AddConnection(cable);
            names.Add(cable.Id, $"cable.{sequence}");
        }
        Assert.Equal(names.Count, names.Values.Distinct().Count());
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(),
            cabinets["downstream"].Intervals.Select(interval => Seed(LoadSwitch(interval), EnergizationSide.Line))));
        return (drawing, state, names);
    }

    private static string TopologyAndEaSignature(
        DrawingDocument drawing, EnergizationResult result, IReadOnlyDictionary<Guid, string> names) =>
        string.Join("|", drawing.Terminals.Select(terminal =>
                $"terminal:{names[terminal.Id]}:{(terminal.ElectricalNodeId is Guid id ? names[id] : "-")}:" +
                $"{result.Terminals[terminal.Id].State}")
            .Concat(drawing.ElectricalNodes.Select(node =>
                $"node:{names[node.Id]}:{node.Type}:{result.Nodes[node.Id].State}:" +
                string.Join(',', node.TerminalIds.Select(id => names[id]).Order(StringComparer.Ordinal))))
            .Concat(new ElectricalConnectivityGraphBuilder().Build(drawing).Edges.Select(edge =>
                $"edge:{edge.Type}:{names[edge.SourceId]}:" + string.Join(',',
                    new[] { names[edge.FirstTerminalId], names[edge.SecondTerminalId] }.Order(StringComparer.Ordinal))))
            .Order(StringComparer.Ordinal));

    private static string NormalizedSignature(WorkScopeCandidate candidate, IReadOnlyDictionary<Guid, string> names)
    {
        string Identity(Guid? id) => id is Guid value ? names[value] : "-";
        return string.Join("|", candidate.Regions.Select(region =>
                "region:" + string.Join(',', region.TerminalIds.Select(id => names[id]).Order(StringComparer.Ordinal)) +
                "/" + string.Join(',', region.ElectricalNodeIds.Select(id => names[id]).Order(StringComparer.Ordinal)))
            .Concat(candidate.Boundaries.Select(boundary =>
                $"boundary:{names[boundary.SwitchDeviceId]}:{boundary.SwitchKind}:{boundary.InstallationType}:" +
                $"{names[boundary.DeenergizedTerminalId]}:{names[boundary.EnergizedTerminalId]}:" +
                $"{Identity(boundary.TopologyParentId)}:{Identity(boundary.AttachedPoleId)}:" +
                string.Join(',', boundary.RelatedConnectionIds.Select(id => names[id]).Order(StringComparer.Ordinal))))
            .Concat(candidate.Diagnostics.Select(diagnostic =>
                $"diagnostic:{diagnostic.Code}:{Identity(diagnostic.Identity)}:{diagnostic.Detail}"))
            .Order(StringComparer.Ordinal));
    }

    private static string Signature(WorkScopeCandidate candidate) => string.Join("|",
        candidate.Regions.Select(region => string.Join(',', region.TerminalIds) + "/" +
            string.Join(',', region.ElectricalNodeIds))
            .Concat(candidate.Boundaries.Select(boundary =>
                $"{boundary.SwitchDeviceId}:{boundary.SwitchKind}:{boundary.InstallationType}:" +
                $"{boundary.DeenergizedTerminalId}:{boundary.EnergizedTerminalId}:{boundary.TopologyParentId}:" +
                $"{boundary.AttachedPoleId}:{string.Join(',', boundary.RelatedConnectionIds)}"))
            .Concat(candidate.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}:{diagnostic.Identity}:{diagnostic.Detail}")));

    private static bool IsEarthTerminal(DrawingDocument drawing, Guid terminalId)
    {
        Terminal terminal = drawing.Terminals.Single(item => item.Id == terminalId);
        return terminal.ElectricalNodeId is Guid nodeId &&
            drawing.ElectricalNodes.Single(item => item.Id == nodeId).Type == ElectricalNodeType.Earth;
    }

    private static EnergizationAnalysisState Analyze(
        DrawingDocument drawing, SwitchDevice seedSwitch, EnergizationSide side)
    {
        var state = new EnergizationAnalysisState();
        WorkScopeAnalysisFixture.Execute(state, drawing, new EnergizationScenario(Guid.NewGuid(), [Seed(seedSwitch, side)], true));
        return state;
    }

    private static EnergizedSeed Seed(SwitchDevice device, EnergizationSide side) =>
        new(Guid.NewGuid(), device.Id, side);

    private static SwitchDevice LoadSwitch(RingCabinetInterval interval) =>
        interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch);

    private static (DrawingDocument, RingCabinet) Cabinet(params RingCabinetIntervalDefinition[] intervals)
    {
        if (intervals.Length == 1)
            intervals = [.. intervals, RingCabinetIntervalDefinition.CreateLoadSwitch(
                2, SwitchState.Open, SwitchState.Open)];
        var drawing = new DrawingDocument(Guid.NewGuid(), "WorkScope projector");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "WorkScope projector", intervals));
        drawing.AddDevice(cabinet);
        return (drawing, cabinet);
    }

    private static (DrawingDocument, SwitchDevice) PoleSwitch()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "WorkScope pole switch");
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
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
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

    private static SwitchDevice AddIsolatedOpenSwitch(DrawingDocument drawing)
    {
        SwitchDevice device = SwitchDevice.CreateForPole(
            Guid.NewGuid(), SwitchKind.LoadSwitch, Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
            device.Id, "A", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
            device.Id, "B", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        return device;
    }
}
