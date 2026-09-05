using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class OptionalCableTerminalCommandTests
{
    [Theory]
    [InlineData(IntervalKind.LoadSwitchInterval)]
    [InlineData(IntervalKind.IntegratedFeederInterval)]
    public void PresentToAbsent_RemovesOnlyTheCableTerminal(IntervalKind kind)
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(kind);
        Guid terminalId = interval.CableTerminalId!.Value;
        Guid circuitNodeId = interval.CircuitNodeId;

        new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, false).Execute();

        RingCabinetInterval changed = GetInterval(cabinet, interval.IntervalId);
        Assert.Null(changed.CableTerminalId);
        Assert.DoesNotContain(document.Terminals, terminal => terminal.Id == terminalId);
        Assert.Contains(document.ElectricalNodes, node => node.Id == circuitNodeId);
    }

    [Fact]
    public void PresentToAbsent_UndoRedoRestoresTheSameTerminalId()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.LoadSwitchInterval);
        Guid terminalId = interval.CableTerminalId!.Value;
        var stack = new CommandStack();

        stack.ExecuteCommand(new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, false));
        Assert.Null(GetInterval(cabinet, interval.IntervalId).CableTerminalId);
        Assert.True(stack.Undo());
        Assert.Equal(terminalId, GetInterval(cabinet, interval.IntervalId).CableTerminalId);
        Assert.True(stack.Redo());
        Assert.Null(GetInterval(cabinet, interval.IntervalId).CableTerminalId);
    }

    [Fact]
    public void AbsentToPresent_UndoRedoReusesId_ButFutureEnableCreatesANewId()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.LoadSwitchInterval);
        cabinet.SetIntervalCableTerminal(interval.IntervalId, null);
        document.SynchronizeRingCabinetAggregate(cabinet);
        var firstStack = new CommandStack();

        firstStack.ExecuteCommand(new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, true));
        Guid firstEnabledId = GetInterval(cabinet, interval.IntervalId).CableTerminalId!.Value;
        Assert.True(firstStack.Undo());
        Assert.Null(GetInterval(cabinet, interval.IntervalId).CableTerminalId);
        Assert.True(firstStack.Redo());
        Assert.Equal(firstEnabledId, GetInterval(cabinet, interval.IntervalId).CableTerminalId);

        Assert.True(firstStack.Undo());
        var futureStack = new CommandStack();
        futureStack.ExecuteCommand(new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, true));
        Assert.NotEqual(
            firstEnabledId,
            GetInterval(cabinet, interval.IntervalId).CableTerminalId);
    }

    [Fact]
    public void CableReference_BlocksRemovalAndLeavesDocumentAndHistoryUnchanged()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.LoadSwitchInterval);
        RingCabinet other = CreateCabinet(IntervalKind.LoadSwitchInterval, 11);
        document.AddDevice(other);
        Guid terminalId = interval.CableTerminalId!.Value;
        Guid otherTerminalId = other.Intervals[0].CableTerminalId!.Value;
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.Cable, terminalId, otherTerminalId, "C1", "10kV");
        document.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(), "C1", "YJV", 10, "10kV", connection.Id,
                terminalId, otherTerminalId),
            connection);
        var stack = new CommandStack();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            stack.ExecuteCommand(new SetRingCabinetIntervalCableTerminalPresenceCommand(
                document, cabinet, interval.IntervalId, false)));

        Assert.Contains("still referenced", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(terminalId, GetInterval(cabinet, interval.IntervalId).CableTerminalId);
        Assert.Contains(document.Terminals, terminal => terminal.Id == terminalId);
        Assert.Empty(stack.History);
    }

    [Fact]
    public void GroundingPointReference_BlocksRemoval()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.IntegratedFeederInterval);
        Guid terminalId = interval.CableTerminalId!.Value;
        document.CreateGroundingPoint(Guid.NewGuid(), terminalId, "电缆侧", "L01");

        Assert.Throws<InvalidOperationException>(() =>
            new SetRingCabinetIntervalCableTerminalPresenceCommand(
                document, cabinet, interval.IntervalId, false).Execute());

        Assert.Equal(terminalId, GetInterval(cabinet, interval.IntervalId).CableTerminalId);
    }

    [Fact]
    public void ConnectionCannotUseAbsentTerminal_AndCanUseExplicitlyReenabledTerminal()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.LoadSwitchInterval);
        RingCabinet other = CreateCabinet(IntervalKind.LoadSwitchInterval, 31);
        document.AddDevice(other);
        Guid removedTerminalId = interval.CableTerminalId!.Value;
        Guid otherTerminalId = other.Intervals[0].CableTerminalId!.Value;
        var disable = new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, false);
        disable.Execute();

        Assert.Throws<InvalidOperationException>(() => document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            removedTerminalId,
            otherTerminalId,
            "Invalid cable",
            "10kV")));

        var enable = new SetRingCabinetIntervalCableTerminalPresenceCommand(
            document, cabinet, interval.IntervalId, true);
        enable.Execute();
        Guid reenabledTerminalId = GetInterval(cabinet, interval.IntervalId)
            .CableTerminalId!.Value;
        document.AddConnection(new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            reenabledTerminalId,
            otherTerminalId,
            "Valid cable",
            "10kV"));

        Assert.NotEqual(removedTerminalId, reenabledTerminalId);
        Assert.Contains(document.Connections, connection =>
            connection.UsesTerminal(reenabledTerminalId));
    }

    [Fact]
    public void WorkScopeBoundaryReference_BlocksRemoval()
    {
        (DrawingDocument document, RingCabinet cabinet, RingCabinetInterval interval) =
            CreateDocument(IntervalKind.LoadSwitchInterval);
        RingCabinet other = CreateCabinet(IntervalKind.LoadSwitchInterval, 21);
        document.AddDevice(other);
        Guid terminalId = interval.CableTerminalId!.Value;
        document.CreateWorkScope(
            Guid.NewGuid(),
            new BoundaryPoint(cabinet.Id, terminalId, "起始"),
            new BoundaryPoint(other.Id, other.Intervals[0].CableTerminalId!.Value, "终止"),
            "检修范围");

        Assert.Throws<InvalidOperationException>(() =>
            new SetRingCabinetIntervalCableTerminalPresenceCommand(
                document, cabinet, interval.IntervalId, false).Execute());

        Assert.Equal(terminalId, GetInterval(cabinet, interval.IntervalId).CableTerminalId);
    }

    [Fact]
    public void PTInterval_DoesNotAcceptOptionalCableTerminalOperation()
    {
        RingCabinet cabinet = CreateCabinet(IntervalKind.PTInterval, 1);
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);

        Assert.Throws<InvalidOperationException>(() =>
            cabinet.SetIntervalCableTerminal(interval.IntervalId, null));
        Assert.True(interval.HasCableTerminal);
    }

    [Fact]
    public void SupportedTypeChange_PreservesPresentIdAndAbsentState()
    {
        RingCabinet presentCabinet = CreateCabinet(IntervalKind.LoadSwitchInterval, 1);
        RingCabinetInterval present = presentCabinet.Intervals[0];
        Guid terminalId = present.CableTerminalId!.Value;

        presentCabinet.ChangeIntervalType(
            present.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding);

        Assert.Equal(
            terminalId,
            GetInterval(presentCabinet, present.IntervalId).CableTerminalId);

        RingCabinet absentCabinet = CreateCabinet(IntervalKind.LoadSwitchInterval, 10);
        RingCabinetInterval absent = absentCabinet.Intervals[0];
        absentCabinet.SetIntervalCableTerminal(absent.IntervalId, null);
        absentCabinet.ChangeIntervalType(
            absent.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding);

        Assert.Null(GetInterval(absentCabinet, absent.IntervalId).CableTerminalId);
    }

    [Fact]
    public void NewSupportedIntervalsAndPTToSupported_DefaultToPresent()
    {
        RingCabinet load = CreateCabinet(IntervalKind.LoadSwitchInterval, 1);
        RingCabinet integrated = CreateCabinet(IntervalKind.IntegratedFeederInterval, 10);
        RingCabinet pt = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "PT mixed",
            [
                RingCabinetIntervalDefinition.CreatePT(
                    20, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    21, SwitchState.Open, SwitchState.Open)
            ]));
        RingCabinetInterval ptInterval = pt.Intervals.Single(interval =>
            interval.IntervalKind == IntervalKind.PTInterval);

        pt.ChangeIntervalType(
            ptInterval.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperIsolationGrounding);

        Assert.True(load.Intervals[0].HasCableTerminal);
        Assert.True(integrated.Intervals[0].HasCableTerminal);
        Assert.True(GetInterval(pt, ptInterval.IntervalId).HasCableTerminal);
    }

    private static (DrawingDocument, RingCabinet, RingCabinetInterval) CreateDocument(
        IntervalKind kind)
    {
        RingCabinet cabinet = CreateCabinet(kind, 1);
        var document = new DrawingDocument(Guid.NewGuid(), "Optional terminal");
        document.AddDevice(cabinet);
        return (document, cabinet, cabinet.Intervals[0]);
    }

    private static RingCabinet CreateCabinet(IntervalKind kind, int firstBay)
    {
        RingCabinetIntervalDefinition first = kind switch
        {
            IntervalKind.LoadSwitchInterval => RingCabinetIntervalDefinition.CreateLoadSwitch(
                firstBay, SwitchState.Open, SwitchState.Open),
            IntervalKind.IntegratedFeederInterval =>
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    firstBay,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open),
            IntervalKind.PTInterval => RingCabinetIntervalDefinition.CreatePT(
                firstBay, SwitchState.Open, SwitchState.Open),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        IReadOnlyList<RingCabinetIntervalDefinition> definitions = kind == IntervalKind.PTInterval
            ? [first]
            : [
                first,
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    firstBay + 1, SwitchState.Open, SwitchState.Open)
            ];
        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(), $"Cabinet {firstBay}", definitions));
    }

    private static RingCabinetInterval GetInterval(RingCabinet cabinet, Guid intervalId) =>
        cabinet.Intervals.Single(interval => interval.IntervalId == intervalId);
}
