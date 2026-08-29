using System.IO;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class SelectionSafetyTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(),
        $"selection-safety-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void MultipleSelectionDeleteIsRejectedWithoutChangingDocument()
    {
        ProjectRuntimeSession session = CreateSession();
        DeviceCommandFactory factory = new();
        AddPoleCommand first = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(10, 10));
        AddPoleCommand second = factory.CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(100, 10));
        session.CommandStack.ExecuteCommand(first);
        session.CommandStack.ExecuteCommand(second);
        session.RebuildScene();
        session.SelectionManager.Replace(
        [
            new SelectionReference(SelectionTargetKind.Device, first.Pole.Id),
            new SelectionReference(SelectionTargetKind.Device, second.Pole.Id)
        ]);
        var controller = new PlacementController(() => session);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            controller.RemoveSelected);

        Assert.Contains("批量删除", error.Message);
        Assert.Contains(session.PersistenceSession.Domain.Devices, item => item.Id == first.Pole.Id);
        Assert.Contains(session.PersistenceSession.Domain.Devices, item => item.Id == second.Pole.Id);
    }

    [Fact]
    public void SceneRebuildRetainsValidSelectionsAndRemovesStaleReferences()
    {
        ProjectRuntimeSession session = CreateSession();
        AddPoleCommand addPole = new DeviceCommandFactory().CreateAddPole(
            session.PersistenceSession.Domain,
            session.Layout,
            new DocumentPoint(10, 10));
        session.CommandStack.ExecuteCommand(addPole);
        session.RebuildScene();
        SelectionReference valid = new(SelectionTargetKind.Device, addPole.Pole.Id);
        SelectionReference stale = new(SelectionTargetKind.Device, Guid.NewGuid());
        session.SelectionManager.Replace([valid, stale]);

        session.RebuildScene();

        Assert.Equal([valid], session.SelectionManager.SelectionSet.SelectedReferences);
        Assert.Equal(valid, session.SelectionManager.Selected);
    }

    private ProjectRuntimeSession CreateSession()
    {
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(_filePath, "选择安全测试");
        return ProjectRuntimeSession.CreateEmpty(persistence, new DrawingSceneBuilder());
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
