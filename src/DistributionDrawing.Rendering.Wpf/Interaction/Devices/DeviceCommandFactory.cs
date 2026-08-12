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
    private readonly CableTerminationAttachmentCreationFactory
        _cableTerminationAttachmentCreationFactory;

    public DeviceCommandFactory(
        RingCabinetCreationFactory? ringCabinetCreationFactory = null,
        RingCabinetLayoutFactory? ringCabinetLayoutFactory = null,
        CableTerminationAttachmentCreationFactory?
            cableTerminationAttachmentCreationFactory = null)
    {
        _ringCabinetCreationFactory = ringCabinetCreationFactory ?? new RingCabinetCreationFactory();
        _ringCabinetLayoutFactory = ringCabinetLayoutFactory ?? new RingCabinetLayoutFactory();
        _cableTerminationAttachmentCreationFactory =
            cableTerminationAttachmentCreationFactory ??
            new CableTerminationAttachmentCreationFactory();
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

    public AddCableTerminationAttachmentCommand CreateAddCableTerminationAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid poleId,
        string? displayName,
        DocumentPoint attachmentOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        CableTerminationAttachmentCreation creation =
            _cableTerminationAttachmentCreationFactory.Create(
                poleId,
                displayName,
                attachmentOffset);
        return new AddCableTerminationAttachmentCommand(
            document,
            runtimeLayout,
            creation);
    }

    public RemoveCableTerminationAttachmentCommand CreateRemoveCableTerminationAttachment(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        PoleAttachment attachment = document.PoleAttachments.SingleOrDefault(candidate =>
                candidate.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException(
                $"Pole attachment '{attachmentId}' does not exist.");
        CableTermination cableTermination = document.Devices.SingleOrDefault(candidate =>
                candidate.Id == attachment.AttachedDeviceId) as CableTermination
            ?? throw new InvalidOperationException(
                $"Attachment '{attachmentId}' does not reference a cable termination.");
        ElectricalNode internalNode = document.ElectricalNodes.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.InternalNodeId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' internal node is missing.");
        Terminal cableSideTerminal = document.Terminals.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.CableSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' cable-side terminal is missing.");
        Terminal overheadSideTerminal = document.Terminals.SingleOrDefault(candidate =>
                candidate.Id == cableTermination.OverheadSideTerminalId)
            ?? throw new InvalidOperationException(
                $"Cable termination '{cableTermination.Id}' overhead-side terminal is missing.");
        AttachmentLayout layout = runtimeLayout.DrawingLayout.Attachments[attachmentId];

        return new RemoveCableTerminationAttachmentCommand(
            document,
            runtimeLayout,
            new CableTerminationAttachmentCreation(
                cableTermination,
                internalNode,
                cableSideTerminal,
                overheadSideTerminal,
                attachment,
                layout));
    }

    public MoveAttachmentCommand CreateMoveAttachment(
        RuntimeLayoutDocument runtimeLayout,
        Guid attachmentId,
        DocumentPoint offset)
    {
        ArgumentNullException.ThrowIfNull(runtimeLayout);

        AttachmentLayout current = runtimeLayout.DrawingLayout.Attachments
            .GetValueOrDefault(attachmentId)
            ?? throw new InvalidOperationException(
                $"No layout exists for attachment '{attachmentId}'.");
        return new MoveAttachmentCommand(
            runtimeLayout.DrawingLayout,
            attachmentId,
            current.Offset,
            offset);
    }

    public RenameCableTerminationCommand CreateRenameCableTermination(
        CableTermination cableTermination,
        string? displayName)
    {
        ArgumentNullException.ThrowIfNull(cableTermination);

        return new RenameCableTerminationCommand(
            cableTermination,
            cableTermination.DisplayName,
            displayName);
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
