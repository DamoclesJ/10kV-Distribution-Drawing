using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RemovePoleSwitchAndBypassCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly IReadOnlyList<OverheadConnectionEndpointTransition> _transitions;

    public RemovePoleSwitchAndBypassCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        PoleSwitchAttachmentCreation creation,
        IReadOnlyList<OverheadConnectionEndpointTransition> transitions)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
        _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
    }

    public PoleSwitchAttachmentCreation Creation { get; }

    public void Execute()
    {
        _document.RemovePoleSwitchAndBypass(Creation.Attachment.AttachmentId);
        try
        {
            _runtimeLayout.DrawingLayout.RemoveAttachment(Creation.Attachment.AttachmentId);
        }
        catch
        {
            RestoreDomain();
            throw;
        }
    }

    public void Undo()
    {
        RestoreDomain();
        try
        {
            _runtimeLayout.DrawingLayout.Add(Creation.Layout);
        }
        catch
        {
            foreach (OverheadConnectionEndpointTransition transition in _transitions.Reverse())
            {
                Replace(transition.After, transition.Before, transition.OverheadLine);
            }

            _document.RemovePoleSwitchAttachment(Creation.Attachment.AttachmentId);
            throw;
        }
    }

    public void Redo() => Execute();

    private void RestoreDomain()
    {
        _document.AddPoleSwitchAttachment(
            Creation.SwitchDevice,
            Creation.FirstTerminal,
            Creation.SecondTerminal,
            Creation.Attachment);
        int applied = 0;
        try
        {
            foreach (OverheadConnectionEndpointTransition transition in _transitions.Reverse())
            {
                Replace(transition.After, transition.Before, transition.OverheadLine);
                applied++;
            }
        }
        catch
        {
            foreach (OverheadConnectionEndpointTransition transition in
                     _transitions.Reverse().Take(applied).Reverse())
            {
                Replace(transition.Before, transition.After, transition.OverheadLine);
            }

            _document.RemovePoleSwitchAttachment(Creation.Attachment.AttachmentId);
            throw;
        }
    }

    private void Replace(Connection before, Connection after, OverheadLine line)
    {
        _document.RemoveOverheadLine(before.Id);
        _document.RemoveConnection(before.Id);
        try
        {
            _document.AddConnection(after);
            _document.AddOverheadLine(line);
        }
        catch
        {
            if (_document.Connections.Any(item => item.Id == after.Id))
            {
                _document.RemoveConnection(after.Id);
            }

            _document.AddConnection(before);
            _document.AddOverheadLine(line);
            throw;
        }
    }
}
