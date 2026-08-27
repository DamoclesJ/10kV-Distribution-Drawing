using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Desktop.PoleSwitchCreation;

public sealed class PoleSwitchAttachmentController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory;
    private Guid? _pendingPoleId;
    private SwitchKind? _pendingSwitchKind;

    public PoleSwitchAttachmentController(
        Func<ProjectRuntimeSession?> getSession,
        DeviceCommandFactory? commandFactory = null)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _commandFactory = commandFactory ?? new DeviceCommandFactory();
    }

    public event EventHandler? SceneChanged;

    public bool IsSelectingControlledConnection =>
        _pendingPoleId is not null && _pendingSwitchKind is not null;

    public string StatusText => IsSelectingControlledConnection
        ? "请选择柱上开关要控制的架空线路，Esc 取消"
        : string.Empty;

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

        Guid[] poleTerminalIds = pole.OverheadAnchorTerminalIds.ToArray();
        int connectedLineCount = session.PersistenceSession.Domain.Connections.Count(connection =>
            connection.Type == ConnectionType.OverheadLine &&
            (poleTerminalIds.Contains(connection.StartTerminalId) ||
             poleTerminalIds.Contains(connection.EndTerminalId)));
        if (connectedLineCount > 2)
        {
            _pendingPoleId = pole.Id;
            _pendingSwitchKind = switchKind;
            SceneChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        AddPoleSwitchAttachmentCommand command = CreateCommand(
            session,
            pole.Id,
            switchKind,
            controlledConnectionId: null);
        Complete(session, pole, command);
    }

    public void PickControlledConnection(Guid connectionId)
    {
        if (!IsSelectingControlledConnection)
        {
            return;
        }

        ProjectRuntimeSession session = _getSession()
            ?? throw new InvalidOperationException("当前没有打开工程。");
        Pole pole = session.PersistenceSession.Domain.Devices.OfType<Pole>()
            .Single(item => item.Id == _pendingPoleId);
        AddPoleSwitchAttachmentCommand command = CreateCommand(
            session,
            pole.Id,
            _pendingSwitchKind!.Value,
            connectionId);
        _pendingPoleId = null;
        _pendingSwitchKind = null;
        Complete(session, pole, command);
    }

    public void Cancel()
    {
        _pendingPoleId = null;
        _pendingSwitchKind = null;
    }

    private AddPoleSwitchAttachmentCommand CreateCommand(
        ProjectRuntimeSession session,
        Guid poleId,
        SwitchKind switchKind,
        Guid? controlledConnectionId)
    {
        return _commandFactory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            poleId,
            switchKind,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(switchKind),
            controlledConnectionId);
    }

    private void Complete(
        ProjectRuntimeSession session,
        Pole pole,
        AddPoleSwitchAttachmentCommand command)
    {
        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
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
