using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkTicketRangeSetupTests
{
    [Fact]
    public void FactTargetsAreExplicitAndIndependentOfBoundaryChanges()
    {
        (DrawingDocument drawing, RingCabinet cabinet, SwitchDevice[] switches) = Cabinet();
        Assert.Throws<InvalidOperationException>(() =>
            UserTicketFactReferences.Create("RedCloth61", null));
        TicketReference targetB = Assert.Single(UserTicketFactReferences.Create("RedCloth61", switches[1].Id));
        Assert.Equal(new TicketReference(TicketReferenceKind.Device, switches[1].Id), targetB);
        UserTicketFact noTargetFact = new("RetainedLive", "现场带电设备",
            UserTicketFactReferences.Create("RetainedLive", null), true);
        Assert.Empty(noTargetFact.References);

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, switches[0].Id, BoundarySide.Line,
            out IsolationBoundary? boundaryA, out _));
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, switches[1].Id, BoundarySide.Line,
            out IsolationBoundary? boundaryB, out _));
        UserTicketFact fact = new("RedCloth61", "一号间隔", [targetB], true);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [boundaryA!, boundaryB!],
            UserFacts = [fact],
            WorkScopeItems = [new WorkScopeItem(WorkScopeItemKind.Equipment, cabinet.Id)]
        };

        WorkTicketSession reordered = WorkTicketRangeSetup.Confirm(drawing, ticket,
            [boundaryB, boundaryA]);
        WorkTicketSession removed = WorkTicketRangeSetup.Confirm(drawing, reordered,
            [boundaryA]);
        WorkTicketSession replaced = WorkTicketRangeSetup.Confirm(drawing, removed,
            [boundaryB]);

        Assert.Equal(switches[1].Id, Assert.Single(reordered.UserFacts[0].References).Id);
        Assert.Equal(switches[1].Id, Assert.Single(removed.UserFacts[0].References).Id);
        Assert.Equal(switches[1].Id, Assert.Single(replaced.UserFacts[0].References).Id);
        Assert.Equal(fact, replaced.UserFacts[0]);
    }

    [Fact]
    public void AnalyzeGateValidatesEveryBoundaryAndDoesNotRequireRetainedLiveOrGroundPoints()
    {
        (DrawingDocument drawing, RingCabinet cabinet, SwitchDevice[] switches) = Cabinet();
        IsolationBoundary Resolve(SwitchDevice device)
        {
            Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Line,
                out IsolationBoundary? boundary, out string issue), issue);
            return boundary!;
        }
        WorkTicketSession Ticket(params IsolationBoundary[] boundaries) => WorkTicketSession.Create() with
        {
            Task = new WorkTask("更换开关", "一号柜"),
            IsolationBoundaries = boundaries
        };
        var analyzer = new WorkTicketAnalyzer();
        IsolationBoundary validA = Resolve(switches[0]);
        IsolationBoundary validB = Resolve(switches[1]);

        InvalidOperationException noBoundaries = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(drawing, Ticket()));
        Assert.Contains("至少设置一个隔离边界", noBoundaries.Message);
        WorkTicketSession oneValid = analyzer.Analyze(drawing, Ticket(validA));
        WorkTicketSession twoValid = analyzer.Analyze(drawing, Ticket(validA, validB));
        Assert.NotNull(oneValid.Draft);
        Assert.NotNull(twoValid.Draft);

        IsolationBoundary wrongTerminal = validA with { TerminalId = switches[0].FirstTerminalId };
        InvalidOperationException oneInvalid = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(drawing, Ticket(wrongTerminal)));
        Assert.Contains("侧别与端子", oneInvalid.Message);
        IsolationBoundary wrongTerminalB = validB with { TerminalId = switches[1].FirstTerminalId };
        InvalidOperationException mixed = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(drawing, Ticket(validA, wrongTerminalB)));
        Assert.Contains("侧别与端子", mixed.Message);

        IsolationBoundary wrongConnection = validA with { ConnectionId = Guid.NewGuid() };
        InvalidOperationException topologyChanged = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(drawing, Ticket(wrongConnection)));
        Assert.Contains("连接或拓扑已变化", topologyChanged.Message);

        IsolationBoundary missingDevice = validA with { DeviceId = Guid.NewGuid() };
        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() =>
            analyzer.Analyze(drawing, Ticket(missingDevice)));
        Assert.Contains("设备", missing.Message);
        Assert.Contains("不存在", missing.Message);

        WorkTicketSession noManualLiveOrGrounding = analyzer.Analyze(drawing, Ticket(validA));
        Assert.Empty(noManualLiveOrGrounding.UserFacts);
        Assert.Empty(noManualLiveOrGrounding.GroundingPointIds);
        Assert.NotNull(noManualLiveOrGrounding.Draft);
        Assert.Equal(SectionCompletion.NeedsConfirmation,
            noManualLiveOrGrounding.Draft!.Section("6.4").Completion);

        WorkTicketSession emptyTask = analyzer.Analyze(drawing, WorkTicketSession.Create() with
        {
            IsolationBoundaries = [validA]
        });
        Assert.NotNull(emptyTask.Draft);
        Assert.Equal("", emptyTask.Task.Content);
        Assert.Equal("", emptyTask.Task.WorkObject);
    }

    [Fact]
    public void OrderedSlotsCanBeReplacedRemovedAndExtendBeyondFour()
    {
        (DrawingDocument drawing, RingCabinet cabinet, SwitchDevice[] switches) = Cabinet();
        IsolationBoundary[] boundaries = switches.Select(device =>
        {
            Assert.True(WorkTicketRangeSetup.TryResolve(drawing, device.Id, BoundarySide.Line,
                out IsolationBoundary? resolved, out _));
            Assert.Equal(device.SecondTerminalId, resolved!.TerminalId);
            return resolved;
        }).ToArray();
        Assert.Equal([BoundarySide.Bus, BoundarySide.Line],
            WorkTicketRangeSetup.AvailableSides(switches[0]));
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Draft = new WorkTicketDraft([new SectionDraft("6.1", [], SectionCompletion.Completed)])
        };
        WorkScopeItem equipment = new(WorkScopeItemKind.Equipment, cabinet.Id);
        WorkTicketSession first = WorkTicketRangeSetup.Confirm(drawing, ticket,
            boundaries.Cast<IsolationBoundary?>().ToArray());
        Assert.Equal(switches.Select(device => device.Id),
            first.IsolationBoundaries.Select(item => item.DeviceId));
        Assert.Equal(SectionCompletion.Stale, first.Draft!.Section("6.1").Completion);

        WorkTicketSession orderedTicket = first with
        {
            IsolationBoundaries = [boundaries[0], boundaries[1]],
            Draft = new WorkTicketDraft([new SectionDraft("6.1", [], SectionCompletion.Completed)])
        };
        WorkTicketSession reorderedTicket = WorkTicketRangeSetup.Confirm(drawing, orderedTicket,
            [boundaries[1], boundaries[0]]);
        Assert.Equal([boundaries[1], boundaries[0]], reorderedTicket.IsolationBoundaries);
        Assert.Equal(SectionCompletion.Stale, reorderedTicket.Draft!.Section("6.1").Completion);

        IsolationBoundary[] replacement = [boundaries[4], boundaries[0], boundaries[2], boundaries[3]];
        WorkTicketSession second = WorkTicketRangeSetup.Confirm(drawing, first,
            replacement.Cast<IsolationBoundary?>().ToArray());
        Assert.Equal(replacement, second.IsolationBoundaries);
        Assert.Equal("AA", WorkTicketRangeSetup.SlotName(26));
        Assert.Same(second, WorkTicketRangeSetup.Confirm(drawing, second,
            replacement.Cast<IsolationBoundary?>().ToArray()));
        Assert.Equal(ticket.WorkScopeItems, first.WorkScopeItems);
    }

    [Fact]
    public void SideChangeInvalidatesCompletedDraftAndUnresolvedSideCannotConfirm()
    {
        (DrawingDocument drawing, RingCabinet cabinet, SwitchDevice[] switches) = Cabinet();
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, switches[0].Id, BoundarySide.Line,
            out IsolationBoundary? line, out _));
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, switches[0].Id, BoundarySide.Bus,
            out IsolationBoundary? bus, out _));
        Assert.Equal(switches[0].FirstTerminalId, bus!.TerminalId);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [line!],
            Draft = new WorkTicketDraft([new SectionDraft("6.1", [], SectionCompletion.Completed)])
        };
        WorkTicketSession changedSide = WorkTicketRangeSetup.Confirm(drawing, ticket,
            [bus]);
        Assert.Equal(SectionCompletion.Stale, changedSide.Draft!.Section("6.1").Completion);
        Assert.Throws<InvalidOperationException>(() => WorkTicketRangeSetup.Confirm(drawing,
            ticket, [null]));
        Assert.Throws<InvalidOperationException>(() => WorkTicketRangeSetup.Confirm(drawing,
            ticket, [new IsolationBoundary(switches[0].Id, BoundarySide.Unknown)]));
    }

    [Fact]
    public void PoleSideResolvesOnlyDirectionsProvenByBothLineTopologies()
    {
        (DrawingDocument drawing, SwitchDevice isolator, Guid smallerConnection, Guid largerConnection) =
            PoleSetup(connectSmaller: true, connectLarger: true);
        Assert.Equal([BoundarySide.SmallerNumber, BoundarySide.LargerNumber],
            WorkTicketRangeSetup.AvailableSides(isolator));

        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.SmallerNumber, out IsolationBoundary? smaller, out string issue), issue);
        Assert.Equal(isolator.FirstTerminalId, smaller!.TerminalId);
        Assert.Equal(smallerConnection, smaller.ConnectionId);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.LargerNumber, out IsolationBoundary? larger, out issue), issue);
        Assert.Equal(isolator.SecondTerminalId, larger!.TerminalId);
        Assert.Equal(largerConnection, larger.ConnectionId);
        Assert.Null(new FirstKindRulePack().BoundaryIssue(drawing, smaller));
        WorkTicketSession analyzed = new WorkTicketAnalyzer().Analyze(drawing,
            WorkTicketSession.Create() with
            {
                Task = new WorkTask("更换隔离刀闸", "P02杆"),
                IsolationBoundaries = [smaller]
            });
        Assert.NotNull(analyzed.Draft);
    }

    [Fact]
    public void PoleWithOnlySmallerTopologyDoesNotInferLargerSide()
    {
        (DrawingDocument drawing, SwitchDevice isolator, _, _) = PoleSetup(true, false);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.SmallerNumber, out _, out _));
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.LargerNumber, out _, out _));
    }

    [Fact]
    public void PoleWithOnlyLargerTopologyDoesNotInferSmallerSide()
    {
        (DrawingDocument drawing, SwitchDevice isolator, _, _) = PoleSetup(false, true);
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.LargerNumber, out _, out _));
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.SmallerNumber, out _, out _));
    }

    [Fact]
    public void PoleWithoutDirectionalTopologyLeavesBothSidesUnresolved()
    {
        (DrawingDocument drawing, SwitchDevice isolator, _, _) = PoleSetup(false, false);
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.SmallerNumber, out _, out _));
        Assert.False(WorkTicketRangeSetup.TryResolve(drawing, isolator.Id,
            BoundarySide.LargerNumber, out _, out _));
    }

    [Fact]
    public void ConfirmDoesNotCreateAnElectricalRangeOrRequireOne()
    {
        (DrawingDocument drawing, _, SwitchDevice[] switches) = Cabinet();
        Assert.True(WorkTicketRangeSetup.TryResolve(drawing, switches[0].Id,
            BoundarySide.Line, out IsolationBoundary? boundary, out _));
        WorkTicketSession result = WorkTicketRangeSetup.Confirm(drawing, WorkTicketSession.Create(),
            [boundary]);
        Assert.Empty(drawing.WorkScopes);
        Assert.Empty(result.WorkScopeIds);
        Assert.Empty(result.WorkScopeItems);
    }

    private static (DrawingDocument, RingCabinet, SwitchDevice[]) Cabinet()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "范围测试");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            Enumerable.Range(1, 5).Select(index => RingCabinetIntervalDefinition.CreateLoadSwitch(
                index, SwitchState.Closed, SwitchState.Open)).ToArray()));
        drawing.AddDevice(cabinet);
        return (drawing, cabinet, cabinet.Intervals.Select(interval =>
            interval.SwitchDevices.Single(device => device.SwitchKind == SwitchKind.LoadSwitch)).ToArray());
    }

    private static (DrawingDocument Drawing, SwitchDevice Switch, Guid SmallerConnection,
        Guid LargerConnection) PoleSetup(bool connectSmaller, bool connectLarger)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "柱上侧别");
        var smallerPole = new Pole(Guid.NewGuid(), "P01");
        var currentPole = new Pole(Guid.NewGuid(), "P02");
        var largerPole = new Pole(Guid.NewGuid(), "P03");
        drawing.AddDevice(smallerPole);
        drawing.AddDevice(currentPole);
        drawing.AddDevice(largerPole);
        Terminal smallerAnchor = smallerPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal largerAnchor = largerPole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(smallerAnchor);
        drawing.AddTerminal(largerAnchor);
        SwitchDevice isolator = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.IsolationSwitch,
            Guid.NewGuid(), Guid.NewGuid(), displayName: "P02隔离刀闸");
        drawing.AddDevice(isolator);
        drawing.AddTerminal(new Terminal(isolator.FirstTerminalId, TopologyOwnerType.Device,
            isolator.Id, "SwitchLeftTerminal", "10kV", true, true, null,
            [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(isolator.SecondTerminalId, TopologyOwnerType.Device,
            isolator.Id, "SwitchRightTerminal", "10kV", true, false, null,
            [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), currentPole.Id, isolator.Id));

        Guid smallerConnection = Guid.Empty;
        if (connectSmaller)
        {
            var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
                smallerAnchor.Id, isolator.FirstTerminalId, "P01-P02", "10kV");
            drawing.AddConnection(connection);
            drawing.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ",
                [smallerPole.Id, currentPole.Id]));
            smallerConnection = connection.Id;
        }
        Guid largerConnection = Guid.Empty;
        if (connectLarger)
        {
            var connection = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
                isolator.SecondTerminalId, largerAnchor.Id, "P02-P03", "10kV");
            drawing.AddConnection(connection);
            drawing.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ",
                [currentPole.Id, largerPole.Id]));
            largerConnection = connection.Id;
        }
        return (drawing, isolator, smallerConnection, largerConnection);
    }
}
