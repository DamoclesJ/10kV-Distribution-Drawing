using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class WorkScopeTests
{
    [Fact]
    public void SelectedDeviceCanPersistWithoutGuessingAnUnavailableWorkSide()
    {
        Guid deviceId = Guid.NewGuid(), terminalId = Guid.NewGuid();
        var boundary = new WorkScopeBoundary(deviceId, BoundarySide.Unknown, terminalId);
        Assert.Equal(deviceId, boundary.DeviceId);
        Assert.Equal(terminalId, boundary.TerminalId);
        Assert.Equal(BoundarySide.Unknown, boundary.Side);
        Assert.Null(boundary.ConnectionId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RegionAcceptsTerminalNodeOrBothAndCopiesInputs(bool hasTerminal, bool hasNode)
    {
        Guid terminal = Guid.NewGuid(), node = Guid.NewGuid();
        Guid[] terminals = hasTerminal ? [terminal] : [];
        Guid[] nodes = hasNode ? [node] : [];
        var region = new WorkScopeRegion(terminals, nodes);
        if (hasTerminal) terminals[0] = Guid.NewGuid();
        if (hasNode) nodes[0] = Guid.NewGuid();
        Assert.Equal(hasTerminal ? [terminal] : Array.Empty<Guid>(), region.TerminalIds);
        Assert.Equal(hasNode ? [node] : Array.Empty<Guid>(), region.ElectricalNodeIds);
        Assert.False(region.TerminalIds is Guid[]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void MultipleDisconnectedRegionsAndAnyBoundaryCountAcceptOptionalDescription(int count)
    {
        WorkScopeRegion[] regions = [new([Guid.NewGuid()], []), new([], [Guid.NewGuid()])];
        WorkScopeBoundary[] boundaries = Enumerable.Range(0, count)
            .Select(_ => new WorkScopeBoundary(Guid.NewGuid(), BoundarySide.Source)).ToArray();
        WorkScope scope = WorkScope.Create(Guid.NewGuid(), regions, boundaries);
        regions[0] = new([Guid.NewGuid()], []);
        if (count > 0) boundaries[0] = new(Guid.NewGuid(), BoundarySide.Load);
        Assert.Equal(2, scope.Regions.Count);
        Assert.Equal(count, scope.Boundaries.Count);
        Assert.Null(scope.Description);
        Assert.Equal("", WorkScope.Create(Guid.NewGuid(), scope.Regions, scope.Boundaries, "").Description);
        Assert.False(scope.Regions is WorkScopeRegion[]);
    }

    [Theory]
    [InlineData("empty-region")]
    [InlineData("empty-terminal")]
    [InlineData("empty-node")]
    [InlineData("repeat-terminal")]
    [InlineData("repeat-node")]
    [InlineData("no-regions")]
    [InlineData("overlap-terminal")]
    [InlineData("overlap-node")]
    [InlineData("empty-scope")]
    [InlineData("empty-device")]
    [InlineData("empty-boundary-terminal")]
    [InlineData("empty-boundary-connection")]
    [InlineData("undefined-side")]
    [InlineData("duplicate-boundary")]
    public void InvalidStructuralContractsAreRejected(string invalid)
    {
        Guid id = Guid.NewGuid();
        var region = new WorkScopeRegion([id], [id]);
        var boundary = new WorkScopeBoundary(id, BoundarySide.Line);
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            switch (invalid)
            {
                case "empty-region": _ = new WorkScopeRegion([], []); break;
                case "empty-terminal": _ = new WorkScopeRegion([Guid.Empty], []); break;
                case "empty-node": _ = new WorkScopeRegion([], [Guid.Empty]); break;
                case "repeat-terminal": _ = new WorkScopeRegion([id, id], []); break;
                case "repeat-node": _ = new WorkScopeRegion([], [id, id]); break;
                case "no-regions": _ = WorkScope.Create(id, [], []); break;
                case "overlap-terminal": _ = WorkScope.Create(id, [region, new([id], [])], []); break;
                case "overlap-node": _ = WorkScope.Create(id, [region, new([], [id])], []); break;
                case "empty-scope": _ = WorkScope.Create(Guid.Empty, [region], []); break;
                case "empty-device": _ = new WorkScopeBoundary(Guid.Empty, BoundarySide.Line); break;
                case "empty-boundary-terminal": _ = new WorkScopeBoundary(id, BoundarySide.Line, Guid.Empty); break;
                case "empty-boundary-connection": _ = new WorkScopeBoundary(id, BoundarySide.Line, connectionId: Guid.Empty); break;
                case "undefined-side": _ = new WorkScopeBoundary(id, (BoundarySide)999); break;
                case "duplicate-boundary": _ = WorkScope.Create(id, [region], [boundary, new(id, BoundarySide.Line)]); break;
            }
        });
    }

    [Theory]
    [InlineData("terminal")]
    [InlineData("node")]
    [InlineData("device")]
    [InlineData("boundary-terminal")]
    [InlineData("boundary-connection")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-connection")]
    public void MissingOrIllegalReferencesRejectAddAndUpdateWithoutChangingDocument(string invalid)
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        RingCabinet other = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet); document.AddDevice(other);
        Guid terminal = cabinet.Intervals[0].CableTerminalId!.Value;
        var connection = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            other.Intervals[0].CableTerminalId!.Value, other.Intervals[1].CableTerminalId!.Value, "c", "10kV");
        document.AddConnection(connection);
        WorkScopeRegion region = invalid == "terminal" ? new([Guid.NewGuid()], []) :
            invalid == "node" ? new([], [Guid.NewGuid()]) : new([terminal], []);
        WorkScopeBoundary[] boundaries = invalid switch
        {
            "device" => [new(Guid.NewGuid(), BoundarySide.Line)],
            "boundary-terminal" => [new(cabinet.Id, BoundarySide.Line, Guid.NewGuid())],
            "boundary-connection" => [new(cabinet.Id, BoundarySide.Line, connectionId: Guid.NewGuid())],
            "wrong-owner" => [new(other.Id, BoundarySide.Line, terminal)],
            "wrong-connection" => [new(cabinet.Id, BoundarySide.Line, terminal, connection.Id)],
            _ => []
        };
        Assert.Throws<InvalidOperationException>(() => document.CreateWorkScope(Guid.NewGuid(), [region], boundaries));
        Assert.Empty(document.WorkScopes);
        WorkScope original = document.CreateWorkScope(Guid.NewGuid(), [new([terminal], [])], [], null);
        Assert.Throws<InvalidOperationException>(() => document.UpdateWorkScope(original.WorkScopeId, [region], boundaries, "bad"));
        Assert.Same(original, Assert.Single(document.WorkScopes));
        Assert.Null(original.Description);
        Assert.Equal(terminal, Assert.Single(original.Regions[0].TerminalIds));
    }

    [Theory]
    [InlineData("terminal")]
    [InlineData("node")]
    [InlineData("device")]
    [InlineData("boundary-terminal")]
    public void RemoveDeviceProtectsAllReferencedIdentitiesAndAllowsUnrelatedRemoval(string reference)
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        RingCabinet unrelated = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet); document.AddDevice(unrelated);
        RingCabinet membership = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(membership);
        Guid terminal = cabinet.Intervals[0].CableTerminalId!.Value;
        Guid node = cabinet.MainBusNodeId;
        Guid identity = reference switch { "node" => node, "device" or "boundary-terminal" => cabinet.Id, _ => terminal };
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(),
            [reference == "node" ? new([], [node]) : reference is "device" or "boundary-terminal"
                ? new([], [membership.MainBusNodeId]) : new([terminal], [])],
            reference == "device" ? [new(cabinet.Id, BoundarySide.Bus)] :
            reference == "boundary-terminal" ? [new(cabinet.Id, BoundarySide.Line, terminal)] : []);
        string error = Assert.Throws<InvalidOperationException>(() => document.RemoveDevice(cabinet.Id)).Message;
        Assert.Contains(scope.WorkScopeId.ToString(), error);
        Assert.Contains(identity.ToString(), error);
        document.RemoveDevice(unrelated.Id);
        Assert.Contains(cabinet, document.Devices);
        Assert.DoesNotContain(unrelated, document.Devices);
    }

    [Theory]
    [InlineData("terminal")]
    [InlineData("node")]
    [InlineData("device")]
    public void RingTypeChangePreflightsRetiredIdentityBeforeAggregateMutation(string reference)
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet);
        var interval = cabinet.Intervals[0];
        RingCabinetRestoreDefinition before = cabinet.CaptureRestoreDefinition();
        RingCabinet replacement = RingCabinet.Restore(before);
        replacement.ChangeIntervalType(interval.IntervalId, IntervalKind.PTInterval);
        Guid terminal = cabinet.Terminals.Select(item => item.Id).Except(replacement.Terminals.Select(item => item.Id)).First();
        Guid node = cabinet.ElectricalNodes.Select(item => item.Id).Except(replacement.ElectricalNodes.Select(item => item.Id)).First();
        Guid device = cabinet.InternalSwitchDevices.Select(item => item.Id).Except(replacement.InternalSwitchDevices.Select(item => item.Id)).First();
        document.CreateWorkScope(Guid.NewGuid(),
            [reference == "terminal" ? new([terminal], []) : reference == "node" ? new([], [node]) : new([], [cabinet.MainBusNodeId])],
            reference == "device" ? [new(device, BoundarySide.Bus)] : []);
        Assert.Throws<InvalidOperationException>(() => cabinet.ChangeIntervalType(interval.IntervalId, IntervalKind.PTInterval));
        Assert.Same(interval, cabinet.Intervals[0]);
        Assert.Contains(document.Terminals, item => item.Id == terminal);
        Assert.Contains(document.ElectricalNodes, item => item.Id == node);
        Assert.Contains(document.Devices, item => item.Id == device);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalCableTerminalRemovalIsGuardedButSwitchStateOnlyChangeIsAllowed(bool boundaryOnly)
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet);
        var interval = cabinet.Intervals[0];
        Guid terminal = interval.CableTerminalId!.Value;
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(),
            [boundaryOnly ? new([], [cabinet.MainBusNodeId]) : new([terminal], [])],
            boundaryOnly ? [new(cabinet.Id, BoundarySide.Line, terminal)] : []);
        Assert.Throws<InvalidOperationException>(() => cabinet.SetIntervalCableTerminal(interval.IntervalId, null));
        Assert.Equal(terminal, cabinet.Intervals[0].CableTerminalId);
        document.ChangeSwitchState(interval.SwitchDevices[0].Id, SwitchState.Closed);
        Assert.Same(scope, Assert.Single(document.WorkScopes));
        if (boundaryOnly) Assert.Equal(terminal, scope.Boundaries[0].TerminalId);
        else Assert.Equal(terminal, scope.Regions[0].TerminalIds[0]);
    }

    [Fact]
    public void IntermediateTerminalCannotBeRemovedWhileInRegion()
    {
        var document = TestFixtures.CreateDocument();
        var intermediate = new IntermediateTerminal(Guid.NewGuid(), "t", Guid.NewGuid());
        var terminal = new Terminal(intermediate.TerminalId, TopologyOwnerType.IntermediateTerminal,
            intermediate.Id, "c", "10kV", true, true, allowedConnectionTypes: [ConnectionType.Cable]);
        document.AddIntermediateTerminal(intermediate, terminal);
        document.CreateWorkScope(Guid.NewGuid(), [new([terminal.Id], [])], []);
        Assert.Throws<InvalidOperationException>(() => document.RemoveIntermediateTerminal(intermediate.Id));
        Assert.Same(intermediate, Assert.Single(document.IntermediateTerminals));
        Assert.Same(terminal, Assert.Single(document.Terminals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryConnectionProtectsDirectAndCableDeletion(bool hasSegment)
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet);
        Guid first = cabinet.Intervals[0].CableTerminalId!.Value, second = cabinet.Intervals[1].CableTerminalId!.Value;
        var connection = new Connection(Guid.NewGuid(), ConnectionType.Cable, first, second, "c", "10kV");
        var cable = new CableSegment(Guid.NewGuid(), "c", "YJV", 1, "10kV", connection.Id, first, second);
        if (hasSegment) document.AddCableSegment(cable, connection); else document.AddConnection(connection);
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(), [new([], [cabinet.MainBusNodeId])],
            [new(cabinet.Id, BoundarySide.Line, first, connection.Id)]);
        string error = Assert.Throws<InvalidOperationException>(() =>
        {
            if (hasSegment) document.RemoveCableSegment(cable.Id); else document.RemoveConnection(connection.Id);
        }).Message;
        Assert.Contains(scope.WorkScopeId.ToString(), error);
        Assert.Contains(connection.Id.ToString(), error);
        Assert.Same(connection, Assert.Single(document.Connections));
        if (hasSegment) Assert.Same(cable, Assert.Single(document.CableSegments));
    }

    [Theory]
    [InlineData("terminal", false)]
    [InlineData("node", false)]
    [InlineData("device", false)]
    [InlineData("boundary-terminal", false)]
    [InlineData("terminal", true)]
    [InlineData("node", true)]
    [InlineData("device", true)]
    [InlineData("boundary-terminal", true)]
    public void PoleSwitchRemovalAndBypassProtectReferencesBeforeChangingTopology(string reference, bool bypass)
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "p");
        document.AddDevice(pole);
        Terminal anchor = TestFixtures.CreatePoleAnchorTerminal(pole, true);
        document.AddTerminal(anchor);
        SwitchDevice device = TestFixtures.CreatePoleSwitch();
        var first = new Terminal(device.TerminalIds[0], TopologyOwnerType.Device, device.Id,
            "first", "10kV", true, true, allowedConnectionTypes: [ConnectionType.OverheadLine]);
        var second = new Terminal(device.TerminalIds[1], TopologyOwnerType.Device, device.Id,
            "second", "10kV", true, false, allowedConnectionTypes: [ConnectionType.OverheadLine]);
        var attachment = new PoleAttachment(Guid.NewGuid(), pole.Id, device.Id);
        document.AddPoleSwitchAttachment(device, first, second, attachment);
        Guid node = document.ElectricalNodes.Single(item => item.OwnerId == device.Id).Id;
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(),
            [reference == "terminal" ? new([second.Id], []) : reference == "node"
                ? new([], [node]) : new([anchor.Id], [])],
            reference == "device" ? [new(device.Id, BoundarySide.Load)] : reference == "boundary-terminal"
                ? [new(device.Id, BoundarySide.Load, second.Id)] : []);
        string error = Assert.Throws<InvalidOperationException>(() =>
        {
            if (bypass) document.RemovePoleSwitchAndBypass(attachment.AttachmentId);
            else document.RemovePoleSwitchAttachment(attachment.AttachmentId);
        }).Message;
        Assert.Contains(scope.WorkScopeId.ToString(), error);
        Assert.Contains(device, document.Devices);
        Assert.Contains(document.Terminals, item => item.Id == second.Id);
        Assert.Contains(document.ElectricalNodes, item => item.Id == node);
        Assert.Same(attachment, Assert.Single(document.PoleAttachments));
    }

    [Fact]
    public void OhlDeletionRejectsBoundaryConnectionBeforeRemovingDetail()
    {
        var document = TestFixtures.CreateDocument();
        var first = new Pole(Guid.NewGuid(), "p1");
        var second = new Pole(Guid.NewGuid(), "p2");
        document.AddDevice(first); document.AddDevice(second);
        Terminal start = TestFixtures.CreatePoleAnchorTerminal(first), end = TestFixtures.CreatePoleAnchorTerminal(second);
        document.AddTerminal(start); document.AddTerminal(end);
        var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine, start.Id, end.Id, "line", "10kV");
        document.AddConnection(connection);
        var line = new OverheadLine(connection.Id, "JKLYJ", [first.Id, second.Id]);
        document.AddOverheadLine(line);
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(), [new([start.Id], [])],
            [new(first.Id, BoundarySide.LargerNumber, start.Id, connection.Id)]);
        string error = Assert.Throws<InvalidOperationException>(() => document.RemoveOverheadLine(connection.Id)).Message;
        Assert.Contains(scope.WorkScopeId.ToString(), error);
        Assert.Contains(connection.Id.ToString(), error);
        Assert.Same(line, Assert.Single(document.OverheadLines));
        Assert.Same(connection, Assert.Single(document.Connections));
    }

    [Fact]
    public void GroundingIsIndependentFromScopeOwnership()
    {
        var document = TestFixtures.CreateDocument();
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        document.AddDevice(cabinet);
        Guid terminal = cabinet.Intervals[0].CableTerminalId!.Value;
        GroundingPoint point = document.CreateGroundingPoint(Guid.NewGuid(), terminal, "c", "S1");
        document.CreateWorkScope(Guid.NewGuid(), [new([terminal], [])], []);
        document.RemoveGroundingPoint(point.GroundingPointId);
        Assert.Empty(document.GroundingPoints);
        Assert.Single(document.WorkScopes);
        Assert.Null(typeof(WorkScope).GetProperty("GroundingPointIds"));
    }
}
