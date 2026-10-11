using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.TestSupport;

internal sealed record WorkScopeCorrectionFixture(
    DrawingDocument Drawing, EnergizationAnalysisState State, EnergizationScenario Scenario)
{
    public static WorkScopeCorrectionFixture Ring(int selectedCount = 2, bool selectedClosed = false)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "EA inverse scope");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "24 feeders",
            Enumerable.Range(1, 24).Select(index => RingCabinetIntervalDefinition.CreateLoadSwitch(index,
                selectedClosed && index <= selectedCount ? SwitchState.Closed : SwitchState.Open,
                SwitchState.Open))));
        drawing.AddDevice(cabinet);
        var scenario = new EnergizationScenario(Guid.NewGuid(), cabinet.Intervals.Take(selectedCount)
            .Select(interval => new EnergizedSeed(Guid.NewGuid(), interval.SwitchDevices[0].Id, EnergizationSide.Bus)));
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        return new(drawing, state, scenario);
    }

    public static WorkScopeCorrectionFixture PoleWithoutDownstreamConnection()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Selected pole without work-side connection");
        var smaller = new Pole(Guid.NewGuid(), "P01");
        var attached = new Pole(Guid.NewGuid(), "P02");
        drawing.AddDevice(smaller);
        drawing.AddDevice(attached);
        Terminal anchor = smaller.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(anchor);
        SwitchDevice device = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
            Guid.NewGuid(), Guid.NewGuid());
        drawing.AddDevice(device);
        drawing.AddTerminal(new Terminal(device.FirstTerminalId, TopologyOwnerType.Device, device.Id,
            "First", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddTerminal(new Terminal(device.SecondTerminalId, TopologyOwnerType.Device, device.Id,
            "Second", "10kV", true, false, null, [ConnectionType.OverheadLine]));
        drawing.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), attached.Id, device.Id));
        var line = new Connection(Guid.NewGuid(), ConnectionType.OverheadLine,
            anchor.Id, device.FirstTerminalId, "left", "10kV");
        drawing.AddConnection(line);
        drawing.AddOverheadLine(new OverheadLine(line.Id, "JKLYJ", [smaller.Id, attached.Id]));
        var scenario = new EnergizationScenario(Guid.NewGuid(),
            [new EnergizedSeed(Guid.NewGuid(), device.Id, EnergizationSide.SmallerNumber)]);
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        return new(drawing, state, scenario);
    }

    public static WorkScopeCorrectionFixture ConnectedRing(bool unresolvedSecond = false,
        bool integratedFeeder = false)
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "Selected switches with real external cables");
        RingCabinet source = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Source",
            Enumerable.Range(1, 24).Select(index => index == 2 && integratedFeeder
                ? RingCabinetIntervalDefinition.CreateIntegratedFeeder(index,
                    GroundingStructureKind.UpperIsolationGrounding, SwitchState.Open,
                    SwitchState.Closed, SwitchState.Open)
                : RingCabinetIntervalDefinition.CreateLoadSwitch(index, SwitchState.Open, SwitchState.Open))));
        RingCabinet downstream = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Downstream",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        drawing.AddDevice(source);
        drawing.AddDevice(downstream);
        SwitchDevice[] selected = source.Intervals.Take(2).Select(interval => interval.SwitchDevices
            .Single(device => device.SwitchKind == (interval.IntervalKind == IntervalKind.IntegratedFeederInterval
                ? SwitchKind.IsolationSwitch : SwitchKind.LoadSwitch))).ToArray();
        for (int index = 0; index < selected.Length; index++)
        {
            Guid first = source.Intervals[index].CableTerminalId!.Value;
            Guid second = downstream.Intervals[index].CableTerminalId!.Value;
            var connection = new Connection(Guid.NewGuid(), ConnectionType.Cable, first, second, $"Cable {index}", "10kV");
            drawing.AddCableSegment(new CableSegment(Guid.NewGuid(), $"Cable {index}", "YJV", 1,
                "10kV", connection.Id, first, second), connection);
        }
        if (unresolvedSecond) drawing.ChangeSwitchState(selected[1].Id, SwitchState.Closed);
        var scenario = new EnergizationScenario(Guid.NewGuid(), selected.Select(device =>
            new EnergizedSeed(Guid.NewGuid(), device.Id, EnergizationSide.Bus)));
        var state = new EnergizationAnalysisState();
        state.Execute(drawing, scenario);
        return new(drawing, state, scenario);
    }
}

internal sealed class CountingCorrectionAnalyzer : IWorkTicketHandoffAnalyzer
{
    private readonly WorkTicketAnalyzer _inner = new();
    public int Calls { get; private set; }
    public WorkTicketSession AnalyzeConfirmedWorkScopeHandoff(DrawingDocument drawing, WorkTicketSession ticket)
    {
        Calls++;
        return _inner.AnalyzeConfirmedWorkScopeHandoff(drawing, ticket);
    }
}
