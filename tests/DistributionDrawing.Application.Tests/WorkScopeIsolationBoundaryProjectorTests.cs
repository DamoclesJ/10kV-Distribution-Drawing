using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkScopeIsolationBoundaryProjectorTests
{
    [Fact]
    public void Project_ZeroBoundariesIsCompleteWithoutEaAndDoesNotChangeExistingTicketFacts()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([cabinet.Intervals[0].CableTerminalId!.Value], []),
             new WorkScopeRegion([], [cabinet.Intervals[1].CircuitNodeId])], []);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [new IsolationBoundary(Guid.NewGuid(), BoundarySide.Line)]
        };
        string before = Snapshot(drawing, scope);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Complete, result.Status);
        Assert.True(result.IsComplete);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Empty(result.BoundaryResults);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ticket, ticket with { });
        Assert.Equal(before, Snapshot(drawing, scope));
    }

    [Fact]
    public void Project_RingLoadSwitchUsesDirectAWhenExactAndDeterministicBWhenResolverMustCompleteFacts()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice busSwitch = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        SwitchDevice lineSwitch = cabinet.Intervals[1].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        Connection connection = new(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[1].CableTerminalId!.Value,
            cabinet.Intervals[0].CableTerminalId!.Value, "C1", "10kV");
        drawing.AddConnection(connection);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, busSwitch.Id, BoundarySide.Bus,
            out IsolationBoundary? direct, out string issue), issue);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, lineSwitch.Id, BoundarySide.Line,
            out IsolationBoundary? adapted, out issue), issue);
        WorkScope scope = AddScope(drawing, [new WorkScopeRegion(
            [busSwitch.FirstTerminalId, lineSwitch.SecondTerminalId], [])],
            [new WorkScopeBoundary(busSwitch.Id, BoundarySide.Bus, direct!.TerminalId, direct.ConnectionId),
             new WorkScopeBoundary(lineSwitch.Id, BoundarySide.Line, lineSwitch.SecondTerminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.IsolationBoundaries.Count);
        WorkScopeBoundaryProjection bus = Assert.Single(result.BoundaryResults,
            item => item.SourceBoundary.DeviceId == busSwitch.Id);
        Assert.Equal(WorkScopeBoundaryClassification.A, bus.Classification);
        Assert.Equal(direct, bus.IsolationBoundary);
        AssertWtaAccepted(drawing, bus.IsolationBoundary!);
        WorkScopeBoundaryProjection line = Assert.Single(result.BoundaryResults,
            item => item.SourceBoundary.DeviceId == lineSwitch.Id);
        Assert.Equal(WorkScopeBoundaryClassification.B, line.Classification);
        Assert.Equal(adapted, line.IsolationBoundary);
        AssertWtaAccepted(drawing, line.IsolationBoundary!);
    }

    [Fact]
    public void Project_WtaSetValidationRejectionDoesNotReturnComplete()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice device = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        drawing.AddConnection(new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value,
            cabinet.Intervals[1].CableTerminalId!.Value, "C1", "10kV"));

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Bus,
            out IsolationBoundary? busBoundary, out string issue), issue);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Line,
            out IsolationBoundary? lineBoundary, out issue), issue);
        IsolationBoundary[] wtaBoundaries = [busBoundary!, lineBoundary!];
        Assert.Throws<InvalidOperationException>(() =>
            WorkTicketRangeSetup.ValidateBoundaries(drawing, wtaBoundaries));

        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([device.FirstTerminalId, device.SecondTerminalId], [])],
            [new WorkScopeBoundary(device.Id, BoundarySide.Bus, busBoundary!.TerminalId),
             new WorkScopeBoundary(device.Id, BoundarySide.Line, lineBoundary!.TerminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.NotEqual(WorkScopeIsolationBoundaryProjectionStatus.Complete, result.Status);
        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable, result.Status);
        Assert.False(result.IsComplete);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Equal(2, result.BoundaryResults.Count);
        Assert.Equal(WorkScopeBoundaryClassification.A,
            Assert.Single(result.BoundaryResults, item => item.SourceBoundary.Side == BoundarySide.Bus)
                .Classification);
        Assert.Equal(WorkScopeBoundaryClassification.B,
            Assert.Single(result.BoundaryResults, item => item.SourceBoundary.Side == BoundarySide.Line)
                .Classification);
        Assert.Equal(2, result.ProjectedBoundaries.Count);
        Assert.Contains(result.ProjectedBoundaries, item =>
            item.SourceBoundaries.Contains(scope.Boundaries.Single(boundary => boundary.Side == BoundarySide.Bus)));
        Assert.Contains(result.ProjectedBoundaries, item =>
            item.SourceBoundaries.Contains(scope.Boundaries.Single(boundary => boundary.Side == BoundarySide.Line)));
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorkScopeBoundaryProjectionDiagnosticCode.WtaBoundarySetRejected);
    }

    [Fact]
    public void Project_MultipleDistinctDeviceBoundariesPassWtaSetValidation()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice[] devices = cabinet.Intervals.Select(interval => interval.SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch)).ToArray();
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion(devices.Select(item => item.FirstTerminalId), [])],
            devices.Select(item => new WorkScopeBoundary(item.Id, BoundarySide.Bus, item.FirstTerminalId)));

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Complete, result.Status);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.IsolationBoundaries.Count);
        WorkTicketRangeSetup.ValidateBoundaries(drawing, result.IsolationBoundaries);
    }

    [Theory]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.IsolationSwitch)]
    public void Project_IntegratedBreakerAndIsolatorUseBAdapterAcceptedByWtaResolver(SwitchKind kind)
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(1,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice device = cabinet.Intervals[0].SwitchDevices.Single(item => item.SwitchKind == kind);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Bus,
            out IsolationBoundary? resolved, out string issue), issue);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([resolved!.TerminalId!.Value], [])],
            [new WorkScopeBoundary(device.Id, BoundarySide.Bus, resolved.TerminalId, resolved.ConnectionId)]);

        WorkScopeBoundaryProjection result = Assert.Single(new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId).BoundaryResults);

        Assert.Equal(WorkScopeBoundaryClassification.B, result.Classification);
        Assert.Equal(resolved, result.IsolationBoundary);
        AssertWtaAccepted(drawing, result.IsolationBoundary!);
    }

    [Fact]
    public void Project_RepresentablePoleUsesPersistedDirectionAndCanonicalTerminalAndConnection()
    {
        (DrawingDocument drawing, SwitchDevice device, Guid smallerConnection, _) = PoleDrawing();
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([device.FirstTerminalId], [])],
            [new WorkScopeBoundary(device.Id, BoundarySide.SmallerNumber,
                device.FirstTerminalId, smallerConnection)]);

        WorkScopeBoundaryProjection result = Assert.Single(new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId).BoundaryResults);

        Assert.Equal(WorkScopeBoundaryClassification.B, result.Classification);
        Assert.Equal(new IsolationBoundary(device.Id, BoundarySide.SmallerNumber,
            device.FirstTerminalId, smallerConnection), result.IsolationBoundary);
        AssertWtaAccepted(drawing, result.IsolationBoundary!);
    }

    [Theory]
    [InlineData("P01", "P01", "P01")]
    [InlineData("?", "?", "?")]
    public void Project_PoleEqualOrUnresolvedDirectionIsCAndNeverFabricatesBoundary(
        string smallerNumber, string currentNumber, string largerNumber)
    {
        (DrawingDocument drawing, SwitchDevice device, Guid smallerConnection, _) =
            PoleDrawing(smallerNumber, currentNumber, largerNumber);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([device.FirstTerminalId], [])],
            [new WorkScopeBoundary(device.Id, BoundarySide.SmallerNumber,
                device.FirstTerminalId, smallerConnection)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable, result.Status);
        Assert.False(result.IsComplete);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Null(Assert.Single(result.BoundaryResults).IsolationBoundary);
        Assert.Equal(WorkScopeBoundaryClassification.C, result.BoundaryResults[0].Classification);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorkScopeBoundaryProjectionDiagnosticCode.UnresolvedPoleDirection);
    }

    [Fact]
    public void Project_CustomerStationIncomingIsolatorIsCWithNoWtaValue()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Customer station projection");
        CustomerStation station = new CustomerStationCreationFactory()
            .Create(StationKind.BoxStation, ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([feeder.StationTerminalId], [feeder.ElectricalNodeId])],
            [new WorkScopeBoundary(feeder.IsolationSwitch.Id, BoundarySide.Load,
                feeder.StationTerminalId)]);

        WorkScopeBoundaryProjection result = Assert.Single(new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId).BoundaryResults);

        Assert.Equal(WorkScopeBoundaryClassification.C, result.Classification);
        Assert.Null(result.IsolationBoundary);
        Assert.Contains(WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedCustomerStationBoundary,
            result.Diagnostics.Select(item => item.Code));
    }

    [Fact]
    public void Project_MixedAandBandCIsUnrepresentableAndExposesNoHandoffBoundaries()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice busSwitch = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        SwitchDevice lineSwitch = cabinet.Intervals[1].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        Connection connection = new(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[1].CableTerminalId!.Value, cabinet.Intervals[0].CableTerminalId!.Value,
            "C1", "10kV");
        drawing.AddConnection(connection);
        CustomerStation station = new CustomerStationCreationFactory()
            .Create(StationKind.BoxStation, ["Feeder A"]);
        drawing.AddCustomerStation(station);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([busSwitch.FirstTerminalId, lineSwitch.SecondTerminalId,
                feeder.StationTerminalId], [feeder.ElectricalNodeId])],
            [new WorkScopeBoundary(feeder.IsolationSwitch.Id, BoundarySide.Load, feeder.StationTerminalId),
             new WorkScopeBoundary(lineSwitch.Id, BoundarySide.Line, lineSwitch.SecondTerminalId),
             new WorkScopeBoundary(busSwitch.Id, BoundarySide.Bus, busSwitch.FirstTerminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable, result.Status);
        Assert.False(result.IsComplete);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Equal(2, result.ProjectedBoundaries.Count);
        Assert.Contains(result.BoundaryResults, item => item.Classification == WorkScopeBoundaryClassification.A);
        Assert.Contains(result.BoundaryResults, item => item.Classification == WorkScopeBoundaryClassification.B);
        Assert.Contains(result.BoundaryResults, item => item.Classification == WorkScopeBoundaryClassification.C);
        foreach (ProjectedIsolationBoundary projection in result.ProjectedBoundaries)
            AssertWtaAccepted(drawing, projection.IsolationBoundary);
        Assert.Single(result.BoundaryResults, item => item.Classification == WorkScopeBoundaryClassification.C &&
            item.IsolationBoundary is null);
    }

    [Fact]
    public void Project_NormalizesDuplicateWtaValuesAndPreservesSourceAttribution()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice device = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([device.FirstTerminalId], [])],
            [new WorkScopeBoundary(device.Id, BoundarySide.Bus),
             new WorkScopeBoundary(device.Id, BoundarySide.Bus, device.FirstTerminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.True(result.IsComplete);
        Assert.Single(result.IsolationBoundaries);
        ProjectedIsolationBoundary normalized = Assert.Single(result.ProjectedBoundaries);
        Assert.Equal(2, normalized.SourceBoundaries.Count);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorkScopeBoundaryProjectionDiagnosticCode.DuplicateNormalizedOutput);
    }

    [Fact]
    public void Project_IsDeterministicAcrossBoundaryInsertionOrderAndIndependentOfOldTicketBoundaries()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice[] switches = cabinet.Intervals.Select(interval => interval.SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.LoadSwitch)).ToArray();
        WorkScopeBoundary[] boundaries =
        [
            new(switches[0].Id, BoundarySide.Bus, switches[0].FirstTerminalId),
            new(switches[1].Id, BoundarySide.Bus, switches[1].FirstTerminalId)
        ];
        WorkScope first = AddScope(drawing, [new WorkScopeRegion(
            [switches[0].FirstTerminalId, switches[1].FirstTerminalId], [])], boundaries);
        IsolationBoundary unrelated = new(Guid.NewGuid(), BoundarySide.Line);
        WorkTicketSession oldTicket = WorkTicketSession.Create() with { IsolationBoundaries = [unrelated] };
        string before = Snapshot(drawing, first);
        string ticketBefore = TicketSignature(oldTicket);

        WorkScopeIsolationBoundaryProjector projector = new();
        WorkScopeIsolationBoundaryProjection projected = projector.Project(drawing, first.WorkScopeId);
        WorkScope reverse = WorkScope.Create(Guid.NewGuid(), first.Regions,
            boundaries.Reverse(), "reverse insertion");
        drawing.AddWorkScope(reverse);
        WorkScopeIsolationBoundaryProjection reversed = projector.Project(drawing, reverse.WorkScopeId);

        Assert.Equal(ProjectionSignature(projected), ProjectionSignature(reversed));
        Assert.Equal(before, Snapshot(drawing, first));
        Assert.Equal(ticketBefore, TicketSignature(oldTicket));
        Assert.Equal(unrelated, Assert.Single(oldTicket.IsolationBoundaries));
    }

    [Fact]
    public void Project_MissingWorkScopeIsInvalidWithStableDiagnostic()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Missing scope");

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, Guid.NewGuid());

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Invalid, result.Status);
        Assert.Equal(WorkScopeBoundaryProjectionDiagnosticCode.MissingWorkScope,
            Assert.Single(result.Diagnostics).Code);
        Assert.Empty(result.IsolationBoundaries);
    }

    [Fact]
    public void Project_GroundSwitchBoundaryIsInvalidAndNeverMappedAsIsolation()
    {
        (DrawingDocument drawing, RingCabinet cabinet) = Cabinet(
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Closed),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open));
        SwitchDevice groundSwitch = cabinet.Intervals[0].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.GroundSwitch);
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([groundSwitch.FirstTerminalId], [])],
            [new WorkScopeBoundary(groundSwitch.Id, BoundarySide.Line, groundSwitch.SecondTerminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Invalid, result.Status);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Equal(WorkScopeBoundaryProjectionDiagnosticCode.GroundingBoundary,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Project_TransformerBoundaryIsUnrepresentableAndDoesNotInventIsolationDevice()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Transformer boundary");
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(Guid.NewGuid(), TransformerKind.PublicIndoor,
            terminalId, "HV leaf");
        drawing.AddTransformer(transformer, new Terminal(terminalId, TopologyOwnerType.Device,
            transformer.Id, Transformer.HvTerminalRole, Transformer.TenKilovolts,
            true, false, null, [ConnectionType.Cable]));
        WorkScope scope = AddScope(drawing,
            [new WorkScopeRegion([terminalId], [])],
            [new WorkScopeBoundary(transformer.Id, BoundarySide.Line, terminalId)]);

        WorkScopeIsolationBoundaryProjection result = new WorkScopeIsolationBoundaryProjector()
            .Project(drawing, scope.WorkScopeId);

        Assert.Equal(WorkScopeIsolationBoundaryProjectionStatus.Unrepresentable, result.Status);
        Assert.Empty(result.IsolationBoundaries);
        Assert.Equal(WorkScopeBoundaryClassification.C, Assert.Single(result.BoundaryResults).Classification);
        Assert.Equal(WorkScopeBoundaryProjectionDiagnosticCode.UnsupportedInstallationType,
            Assert.Single(result.Diagnostics).Code);
    }

    private static (DrawingDocument Drawing, RingCabinet Cabinet) Cabinet(
        params RingCabinetIntervalDefinition[] intervals)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "WorkScope boundary adapter");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), "Adapter cabinet", intervals));
        drawing.AddDevice(cabinet);
        return (drawing, cabinet);
    }

    private static WorkScope AddScope(
        DrawingDocument drawing,
        IEnumerable<WorkScopeRegion> regions,
        IEnumerable<WorkScopeBoundary> boundaries)
    {
        WorkScope scope = WorkScope.Create(Guid.NewGuid(), regions, boundaries);
        drawing.AddWorkScope(scope);
        return scope;
    }

    private static void AssertWtaAccepted(DrawingDocument drawing, IsolationBoundary boundary)
    {
        WorkTicketRangeSetup.ValidateBoundaries(drawing, [boundary]);
        Assert.Null(new FirstKindRulePack().BoundaryIssue(drawing, boundary));
    }

    private static (DrawingDocument Drawing, SwitchDevice Device, Guid SmallerConnection,
        Guid LargerConnection) PoleDrawing(
        string smallerNumber = "P01", string currentNumber = "P02", string largerNumber = "P03")
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Pole boundary adapter");
        Pole smaller = new(Guid.NewGuid(), smallerNumber);
        Pole current = new(Guid.NewGuid(), currentNumber);
        Pole larger = new(Guid.NewGuid(), largerNumber);
        drawing.AddDevice(smaller);
        drawing.AddDevice(current);
        drawing.AddDevice(larger);
        Terminal smallerAnchor = smaller.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = larger.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.IsolationSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device,
            device.Id, "SwitchLeftTerminal", "10kV", true, true, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device,
            device.Id, "SwitchRightTerminal", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), current.Id, device.Id));
        Connection left = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            smallerAnchor.Id, device.FirstTerminalId, "P01-P02", "10kV");
        Connection right = new(Guid.NewGuid(), ConnectionType.OverheadLine,
            device.SecondTerminalId, largerAnchor.Id, "P02-P03", "10kV");
        drawing.AddConnection(left);
        drawing.AddConnection(right);
        drawing.AddOverheadLine(new OverheadLine(left.Id, "JKLYJ", [smaller.Id, current.Id]));
        drawing.AddOverheadLine(new OverheadLine(right.Id, "JKLYJ", [current.Id, larger.Id]));
        return (drawing, device, left.Id, right.Id);
    }

    private static string ProjectionSignature(WorkScopeIsolationBoundaryProjection result) =>
        $"{result.Status}|{result.IsComplete}|" +
        string.Join('|', result.BoundaryResults.Select(item =>
            $"{item.SourceBoundary.DeviceId:N}:{item.SourceBoundary.Side}:{item.Classification}:" +
            $"{item.IsolationBoundary?.DeviceId:N}:{item.IsolationBoundary?.Side}:" +
            $"{item.IsolationBoundary?.TerminalId:N}:{item.IsolationBoundary?.ConnectionId:N}:" +
            string.Join(',', item.Diagnostics.Select(diagnostic => diagnostic.Code)))) + "|" +
        string.Join('|', result.Diagnostics.Select(item => item.Code)) + "|" +
        string.Join('|', result.IsolationBoundaries.Select(item =>
            $"{item.DeviceId:N}:{item.Side}:{item.TerminalId:N}:{item.ConnectionId:N}"));

    private static string Snapshot(DrawingDocument drawing, WorkScope scope) =>
        $"{scope.WorkScopeId:N}:{scope.Description}|" +
        string.Join('|', scope.Regions.Select(region =>
            $"{string.Join(',', region.TerminalIds.Order())}/{string.Join(',', region.ElectricalNodeIds.Order())}")) + "|" +
        string.Join('|', scope.Boundaries.Select(item =>
            $"{item.DeviceId:N}:{item.Side}:{item.TerminalId:N}:{item.ConnectionId:N}")) + "|" +
        string.Join('|', drawing.Connections.Select(item => item.Id).Order()) + "|" +
        string.Join('|', drawing.Devices.OfType<SwitchDevice>().Select(item =>
            $"{item.Id:N}:{item.SwitchState}"));

    private static string TicketSignature(WorkTicketSession ticket) =>
        $"{ticket.Id:N}:{string.Join('|', ticket.IsolationBoundaries.Select(item =>
            $"{item.DeviceId:N}:{item.Side}:{item.TerminalId:N}:{item.ConnectionId:N}"))}:" +
        string.Join(',', ticket.WorkScopeIds);
}
