using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class AddPoleSwitchAttachmentCommand : ICommand
{
    private readonly DrawingDocument _document;
    private readonly RuntimeLayoutDocument _runtimeLayout;
    private readonly IReadOnlyList<OverheadConnectionEndpointTransition> _transitions;

    public AddPoleSwitchAttachmentCommand(
        DrawingDocument document,
        RuntimeLayoutDocument runtimeLayout,
        PoleSwitchAttachmentCreation creation,
        IReadOnlyList<OverheadConnectionEndpointTransition>? transitions = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _runtimeLayout = runtimeLayout ?? throw new ArgumentNullException(nameof(runtimeLayout));
        Creation = creation ?? throw new ArgumentNullException(nameof(creation));
        _transitions = transitions ?? [];
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
        bool layoutAdded = false;
        int appliedTransitions = 0;
        try
        {
            _runtimeLayout.DrawingLayout.Add(Creation.Layout);
            layoutAdded = true;
            foreach (OverheadConnectionEndpointTransition transition in _transitions)
            {
                ReplaceOverheadConnection(
                    transition.Before,
                    transition.After,
                    transition.OverheadLine);
                appliedTransitions++;
            }
        }
        catch
        {
            foreach (OverheadConnectionEndpointTransition transition in
                     _transitions.Take(appliedTransitions).Reverse())
            {
                ReplaceOverheadConnection(
                    transition.After,
                    transition.Before,
                    transition.OverheadLine);
            }

            if (layoutAdded)
            {
                _runtimeLayout.DrawingLayout.RemoveAttachment(
                    Creation.Attachment.AttachmentId);
            }

            _document.RemovePoleSwitchAttachment(Creation.Attachment.AttachmentId);
            throw;
        }
    }

    public void Undo()
    {
        foreach (OverheadConnectionEndpointTransition transition in _transitions.Reverse())
        {
            ReplaceOverheadConnection(transition.After, transition.Before, transition.OverheadLine);
        }

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

    private void ReplaceOverheadConnection(
        Connection before,
        Connection after,
        OverheadLine overheadLine)
    {
        Connection current = _document.Connections.Single(connection => connection.Id == before.Id);
        if (current.StartTerminalId != before.StartTerminalId ||
            current.EndTerminalId != before.EndTerminalId)
        {
            throw new InvalidOperationException("架空线端点状态与预期不一致。");
        }

        _document.RemoveOverheadLine(before.Id);
        _document.RemoveConnection(before.Id);
        try
        {
            _document.AddConnection(after);
            try
            {
                _document.AddOverheadLine(overheadLine);
            }
            catch
            {
                _document.RemoveConnection(after.Id);
                throw;
            }
        }
        catch
        {
            _document.AddConnection(before);
            _document.AddOverheadLine(overheadLine);
            throw;
        }
    }
}

public sealed record OverheadConnectionEndpointTransition(
    Connection Before,
    Connection After,
    OverheadLine OverheadLine);
