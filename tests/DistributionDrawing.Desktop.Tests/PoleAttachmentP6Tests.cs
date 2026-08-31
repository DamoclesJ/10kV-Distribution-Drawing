using System.IO;
using DistributionDrawing.Desktop.PoleAttachmentManagement;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class PoleAttachmentP6Tests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"pole-attachment-p6-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void CableTerminationCanBeDeletedWithoutDeletingPoleAndUndoRestoresIt()
    {
        ProjectRuntimeSession session = CreateSession();
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 30));
        pole.Execute();
        AddCableTerminationAttachmentCommand addTermination =
            factory.CreateAddCableTerminationAttachment(
                session.PersistenceSession.Domain,
                session.Layout,
                pole.Pole.Id,
                "终端 1",
                new DocumentPoint(12, 0));
        addTermination.Execute();
        session.RebuildScene();
        var controller = new PoleAttachmentManagementController(() => session);

        controller.Remove(addTermination.Creation.Attachment.AttachmentId);

        Assert.Contains(session.PersistenceSession.Domain.Devices, item => item.Id == pole.Pole.Id);
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices,
            item => item.Id == addTermination.Creation.CableTermination.Id);
        Assert.DoesNotContain(session.PersistenceSession.Domain.PoleAttachments,
            item => item.AttachmentId == addTermination.Creation.Attachment.AttachmentId);
        Assert.True(session.CommandStack.Undo());
        Assert.Contains(session.PersistenceSession.Domain.Devices,
            item => item.Id == addTermination.Creation.CableTermination.Id);
        Assert.True(session.CommandStack.Redo());
        Assert.DoesNotContain(session.PersistenceSession.Domain.Devices,
            item => item.Id == addTermination.Creation.CableTermination.Id);
    }

    [Fact]
    public void PoleSwitchAttachmentSelectionProjectsSwitchStateAndBusinessActions()
    {
        ProjectRuntimeSession session = CreateSession();
        var factory = new DeviceCommandFactory();
        AddPoleCommand pole = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(20, 30));
        pole.Execute();
        AddPoleSwitchAttachmentCommand addSwitch = factory.CreateAddPoleSwitchAttachment(
            session.PersistenceSession.Domain,
            session.Layout,
            pole.Pole.Id,
            SwitchKind.IsolationSwitch,
            new DocumentPoint(12, 0));
        addSwitch.Execute();
        session.RebuildScene();
        session.SelectionResolver.SetSource(new PropertyInspectionSource
        {
            Document = session.PersistenceSession.Domain,
            Devices = session.PersistenceSession.Domain.Devices,
            PoleAttachments = session.PersistenceSession.Domain.PoleAttachments,
            DrawingLayout = session.Layout.DrawingLayout
        });
        SelectionReference reference = new(
            SelectionTargetKind.PoleAttachment,
            addSwitch.Creation.Attachment.AttachmentId,
            pole.Pole.Id);

        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(
            session.SelectionResolver.Resolve(reference));
        PropertyInspectorSnapshot snapshot = session.PropertyProjector.Project(resolved);

        Assert.Same(addSwitch.Creation.SwitchDevice, resolved.SwitchDevice);
        Assert.Equal("柱上开关", snapshot.ObjectType);
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties),
            property => property.DisplayName == "机械状态");
        Assert.NotNull(resolved.PoleAttachment);
        Assert.NotNull(resolved.AttachmentLayout);
    }

    private ProjectRuntimeSession CreateSession()
    {
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(_path, "P6 附件测试");
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
