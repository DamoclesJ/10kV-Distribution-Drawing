using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.CableTerminationCreation;

public sealed class CableTerminationAttachmentController
{
    private static readonly DocumentPoint InitialAttachmentOffset = new(9, 12);
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory;

    public CableTerminationAttachmentController(
        Func<ProjectRuntimeSession?> getSession,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public event EventHandler? SceneChanged;

    public void AddToSelectedPole(string? displayName)
    {
        ProjectRuntimeSession session = RequireSession();
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("No pole is selected.");
        if (selected.Kind != SelectionTargetKind.Device)
        {
            throw new InvalidOperationException("The selected object is not a pole.");
        }

        Pole pole = session.PersistenceSession.Domain.Devices
            .OfType<Pole>()
            .SingleOrDefault(candidate => candidate.Id == selected.ObjectId)
            ?? throw new InvalidOperationException("The selected object is not a pole.");

        AddCableTerminationAttachmentCommand command =
            _commandFactory.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Id,
                displayName,
                InitialAttachmentOffset);

        session.CommandStack.ExecuteCommand(command);
        session.RebuildScene();
        session.SelectionManager.Select(
            new SelectionReference(
                SelectionTargetKind.PoleAttachment,
                command.Creation.Attachment.AttachmentId,
                pole.Id));
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private ProjectRuntimeSession RequireSession()
    {
        return _getSession()
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
