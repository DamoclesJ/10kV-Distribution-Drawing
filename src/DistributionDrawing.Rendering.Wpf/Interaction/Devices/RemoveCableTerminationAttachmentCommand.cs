using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemoveCableTerminationAttachmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public RemoveCableTerminationAttachmentCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        CableTerminationAttachmentCreation creation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
    }

    public CableTerminationAttachmentCreation Creation { get; }

    public void Execute()
    {
        _ = _runtimeLayout.DrawingLayout.Attachments[
            Creation.Attachment.AttachmentId];
        _document.RemoveCableTerminationAttachment(
            Creation.Attachment.AttachmentId);
        try
        {
            _runtimeLayout.DrawingLayout.RemoveAttachment(
                Creation.Attachment.AttachmentId);
        }
        catch
        {
            AddDomainAggregate();
            throw;
        }
    }

    public void Undo()
    {
        if (_runtimeLayout.DrawingLayout.Attachments.ContainsKey(
                Creation.Attachment.AttachmentId))
        {
            throw new InvalidOperationException(
                $"Attachment layout '{Creation.Attachment.AttachmentId}' already exists.");
        }

        AddDomainAggregate();
        try
        {
            _runtimeLayout.DrawingLayout.Add(Creation.Layout);
        }
        catch
        {
            _document.RemoveCableTerminationAttachment(
                Creation.Attachment.AttachmentId);
            throw;
        }
    }

    public void Redo() => Execute();

    private void AddDomainAggregate()
    {
        _document.AddCableTerminationAttachment(
            Creation.CableTermination,
            Creation.InternalNode,
            Creation.CableSideTerminal,
            Creation.OverheadSideTerminal,
            Creation.Attachment);
    }
}
