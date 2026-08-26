using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Devices;

public sealed class CreatePoleCommand
{
    private readonly DrawingDocument _document;

    public CreatePoleCommand(
        DrawingDocument document,
        PoleCreationResult result)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PoleCreationResult Result { get; }

    public Pole Pole => Result.Pole;

    public void Execute()
    {
        if (_document.Devices.Any(device => device.Id == Pole.Id))
        {
            throw new InvalidOperationException(
                $"Pole '{Pole.Id}' already exists in the document.");
        }

        _document.AddDevice(Pole);
        try
        {
            foreach (ElectricalNode node in Result.ElectricalNodes.Where(node =>
                         node.OwnerType == TopologyOwnerType.Device &&
                         node.OwnerId == Pole.Id))
            {
                _document.AddElectricalNode(node);
            }

            foreach (Terminal terminal in Result.Terminals.Where(terminal =>
                         terminal.OwnerType == TopologyOwnerType.Device &&
                         terminal.OwnerId == Pole.Id))
            {
                _document.AddTerminal(terminal);
            }

            foreach (Device device in Result.Devices)
            {
                AddAttachedDevice(device);
            }
        }
        catch
        {
            RemoveAttachedDevices();
            _document.RemoveDevice(Pole.Id);
            throw;
        }
    }

    public void Undo()
    {
        RemoveAttachedDevices();
        _document.RemoveDevice(Pole.Id);
    }

    public void Redo()
    {
        Execute();
    }

    private void AddAttachedDevice(Device device)
    {
        PoleAttachment attachment = Result.Attachments.Single(candidate =>
            candidate.AttachedDeviceId == device.Id);

        switch (device)
        {
            case SwitchDevice switchDevice:
                Terminal[] switchTerminals = Result.Terminals
                    .Where(terminal => switchDevice.OwnsTerminal(terminal.Id))
                    .ToArray();
                if (switchTerminals.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"Switch '{switchDevice.Id}' must have two creation terminals.");
                }

                _document.AddPoleSwitchAttachment(
                    switchDevice,
                    switchTerminals[0],
                    switchTerminals[1],
                    attachment);
                break;

            case CableTermination cableTermination:
                ElectricalNode internalNode = Result.ElectricalNodes.Single(node =>
                    node.Id == cableTermination.InternalNodeId);
                Terminal cableSideTerminal = Result.Terminals.Single(terminal =>
                    terminal.Id == cableTermination.CableSideTerminalId);
                Terminal overheadSideTerminal = Result.Terminals.Single(terminal =>
                    terminal.Id == cableTermination.OverheadSideTerminalId);
                _document.AddCableTerminationAttachment(
                    cableTermination,
                    internalNode,
                    cableSideTerminal,
                    overheadSideTerminal,
                    attachment);
                break;

            default:
                throw new InvalidOperationException(
                    $"Device type '{device.Type}' is not supported as a pole attachment.");
        }
    }

    private void RemoveAttachedDevices()
    {
        foreach (Device device in Result.Devices.Reverse())
        {
            PoleAttachment? attachment = Result.Attachments.SingleOrDefault(candidate =>
                candidate.AttachedDeviceId == device.Id);
            if (attachment is null)
            {
                continue;
            }

            if (!_document.PoleAttachments.Any(existing =>
                    existing.AttachedDeviceId == device.Id))
            {
                continue;
            }

            switch (device)
            {
                case SwitchDevice:
                    _document.RemovePoleSwitchAttachment(attachment.AttachmentId);
                    break;
                case CableTermination:
                    _document.RemoveCableTerminationAttachment(attachment.AttachmentId);
                    break;
            }
        }
    }
}
