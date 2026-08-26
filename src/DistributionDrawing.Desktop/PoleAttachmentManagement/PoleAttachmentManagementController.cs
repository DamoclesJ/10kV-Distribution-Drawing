using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.PoleAttachmentManagement;

public sealed class PoleAttachmentManagementController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;
    private readonly DeviceCommandFactory _commandFactory = new();

    public PoleAttachmentManagementController(Func<ProjectRuntimeSession?> getSession)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
    }

    public event EventHandler? SceneChanged;

    public void Remove(Guid attachmentId)
    {
        ProjectRuntimeSession session = _getSession()
            ?? throw new InvalidOperationException("当前没有打开工程。");
        RemovePoleSwitchAttachmentCommand command = _commandFactory
            .CreateRemovePoleSwitchAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                attachmentId);
        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        session.SelectionManager.Clear();
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Rotate(Guid attachmentId, int quarterTurns)
    {
        ProjectRuntimeSession session = _getSession()
            ?? throw new InvalidOperationException("当前没有打开工程。");
        PoleAttachment attachment = session.PersistenceSession.Domain.PoleAttachments
            .SingleOrDefault(item => item.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException("所选附着设备不存在。");
        if (session.PersistenceSession.Domain.Devices
                .SingleOrDefault(item => item.Id == attachment.AttachedDeviceId) is not SwitchDevice)
        {
            throw new InvalidOperationException("电缆终端使用圆周拖动，不支持四方向旋转。");
        }
        AttachmentLayout before = session.Layout.DrawingLayout.Attachments[attachmentId];
        ChangeAttachmentLayoutCommand command = new(
            session.Layout.DrawingLayout,
            before,
            before.RotateBy(quarterTurns));
        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }
}
