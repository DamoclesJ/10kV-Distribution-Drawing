using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class DeviceCommandFactory
{
    public AddPoleCommand CreateAddPole(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Guid poleId = Guid.NewGuid();
        var pole = new Pole(poleId, NextPoleNumber(document));
        Terminal terminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid(), allowsMultipleConnections: true);
        return new AddPoleCommand(
            document,
            runtimeLayout,
            pole,
            terminal,
            new PoleLayout(poleId, position));
    }

    public AddRingCabinetCommand CreateAddRingCabinet(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Guid cabinetId = Guid.NewGuid();
        RingCabinet cabinet = RingCabinet.CreateNormalLoadSwitchCabinet(
            cabinetId,
            $"环网柜-{document.Devices.OfType<RingCabinet>().Count() + 1}",
            intervalCount: 3,
            initialLoadSwitchState: SwitchState.Open,
            initialGroundSwitchState: SwitchState.Open);
        return new AddRingCabinetCommand(
            document,
            runtimeLayout,
            cabinet,
            CreateRingCabinetLayout(cabinet, position));
    }

    public ICommand CreateRemove(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid deviceId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        Device device = document.Devices.SingleOrDefault(candidate => candidate.Id == deviceId)
            ?? throw new InvalidOperationException($"Device '{deviceId}' does not exist.");
        return device switch
        {
            Pole pole => new RemovePoleCommand(
                document,
                runtimeLayout,
                pole,
                runtimeLayout.DrawingLayout.Poles[pole.Id]),
            RingCabinet cabinet => new RemoveRingCabinetCommand(
                document,
                runtimeLayout,
                cabinet,
                runtimeLayout.RingCabinetLayouts[cabinet.Id]),
            _ => throw new InvalidOperationException(
                "Only Pole and RingCabinet deletion is supported in this phase.")
        };
    }

    private static string NextPoleNumber(DrawingDocument document)
    {
        int sequence = document.Devices.OfType<Pole>().Count() + 1;
        string candidate;
        do
        {
            candidate = $"P-{sequence:00}";
            sequence++;
        }
        while (document.Devices.OfType<Pole>().Any(pole => pole.PoleNumber == candidate));

        return candidate;
    }

    private static RingCabinetLayout CreateRingCabinetLayout(
        RingCabinet cabinet,
        DocumentPoint position)
    {
        const double intervalWidth = 42;
        const double intervalHeight = 90;
        var intervals = new List<RingCabinetIntervalLayout>();
        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            var switches = new[]
            {
                CreateSwitchLayout(interval, SwitchKind.LoadSwitch, new DocumentPoint(14, 30)),
                CreateSwitchLayout(interval, SwitchKind.GroundSwitch, new DocumentPoint(14, 58))
            };
            intervals.Add(new RingCabinetIntervalLayout(
                interval.IntervalId,
                new DocumentPoint((interval.Sequence - 1) * intervalWidth, 10),
                intervalWidth,
                intervalHeight,
                switchLayouts: switches));
        }

        return new RingCabinetLayout(
            cabinet.Id,
            position,
            intervalWidth * cabinet.Intervals.Count,
            110,
            20,
            intervals);
    }

    private static RingCabinetSwitchLayout CreateSwitchLayout(
        RingCabinetInterval interval,
        SwitchKind kind,
        DocumentPoint position)
    {
        SwitchDevice switchDevice = interval.SwitchDevices.Single(item => item.SwitchKind == kind);
        return new RingCabinetSwitchLayout(switchDevice.Id, position);
    }
}
