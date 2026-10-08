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
