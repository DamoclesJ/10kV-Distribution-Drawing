using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class DeviceCommandFactory
{
    private readonly RingCabinetCreationFactory _ringCabinetCreationFactory;
    private readonly RingCabinetLayoutFactory _ringCabinetLayoutFactory;

    public DeviceCommandFactory(
        RingCabinetCreationFactory? ringCabinetCreationFactory = null,
        RingCabinetLayoutFactory? ringCabinetLayoutFactory = null)
    {
        _ringCabinetCreationFactory = ringCabinetCreationFactory ?? new RingCabinetCreationFactory();
        _ringCabinetLayoutFactory = ringCabinetLayoutFactory ?? new RingCabinetLayoutFactory();
    }

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
        RingCabinetCreationConfiguration configuration,
        DocumentPoint position)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        ArgumentNullException.ThrowIfNull(configuration);

        RingCabinet cabinet = _ringCabinetCreationFactory.Create(configuration);
        return new AddRingCabinetCommand(
            document,
            runtimeLayout,
            cabinet,
            _ringCabinetLayoutFactory.Create(cabinet, position));
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
}
