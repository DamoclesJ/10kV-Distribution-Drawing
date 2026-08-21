using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Desktop.PoleSwitchCreation;

public sealed class PoleSwitchAttachmentController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory;

    public PoleSwitchAttachmentController(
        Func<ProjectRuntimeSession?> getSession,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public event EventHandler? SceneChanged;

    public void AddToSelectedPole(SwitchKind switchKind)
    {
        ProjectRuntimeSession session = _getSession()
            ?? throw new InvalidOperationException("当前没有打开工程。");
        SelectionReference selected = session.SelectionManager.Selected
            ?? throw new InvalidOperationException("请先选择一个杆塔。");

        Pole? pole = ResolveSelectedPole(session, selected);
        if (pole is null)
        {
            throw new InvalidOperationException("请先选择一个杆塔。");
        }

        AddPoleSwitchAttachmentCommand command = _commandFactory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Id,
            switchKind,
            new DocumentPoint(0, 0));
        session.CommandStack.ExecuteCommand(command);
        session.RebuildScene();
        session.SelectionManager.Select(new SelectionReference(
            SelectionTargetKind.PoleAttachment,
            command.Creation.Attachment.AttachmentId,
            pole.Id));
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Pole? ResolveSelectedPole(
        ProjectRuntimeSession session,
        SelectionReference selected)
    {
        if (selected.Kind == SelectionTargetKind.Device &&
            session.PersistenceSession.Domain.Devices.SingleOrDefault(
                device => device.Id == selected.ObjectId) is Pole pole)
        {
            return pole;
        }

        Guid? poleId = selected.Kind switch
        {
            SelectionTargetKind.PoleAttachment => selected.ParentId,
            SelectionTargetKind.Device => session.PersistenceSession.Domain.PoleAttachments
                .SingleOrDefault(attachment => attachment.AttachedDeviceId == selected.ObjectId)
                ?.PoleId,
            _ => null
        };

        return poleId is Guid id
            ? session.PersistenceSession.Domain.Devices
                .OfType<Pole>()
                .SingleOrDefault(candidate => candidate.Id == id)
            : null;
    }
}
