using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddPoleSwitchAttachmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;

    public AddPoleSwitchAttachmentCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        PoleSwitchAttachmentCreation creation)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
    }

    public PoleSwitchAttachmentCreation Creation { get; }

    public void Execute()
    {
        if (_runtimeLayout.DrawingLayout.Attachments.ContainsKey(Creation.Attachment.AttachmentId))
        {
            throw new InvalidOperationException("柱上开关布局已存在。");
        }

        _document.AddPoleSwitchAttachment(
            Creation.SwitchDevice,
            Creation.FirstTerminal,
            Creation.SecondTerminal,
            Creation.Attachment);
        try
        {
            _runtimeLayout.DrawingLayout.Add(Creation.Layout);
        }
        catch
        {
            _document.RemovePoleSwitchAttachment(Creation.Attachment.AttachmentId);
            throw;
        }
    }

    public void Undo()
    {
        _document.RemovePoleSwitchAttachment(Creation.Attachment.AttachmentId);
        try
        {
            _runtimeLayout.DrawingLayout.RemoveAttachment(Creation.Attachment.AttachmentId);
        }
        catch
        {
            _document.AddPoleSwitchAttachment(
                Creation.SwitchDevice,
                Creation.FirstTerminal,
                Creation.SecondTerminal,
                Creation.Attachment);
            throw;
        }
    }

    public void Redo() => Execute();
}
