using System.IO;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Desktop.Placement;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Desktop.DrawingTools;
using DistributionDrawing.Desktop.Selection;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class PlacementPreviewTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(),
        $"placement-preview-{Guid.NewGuid():N}.kvdrawing");

    [Fact]
    public void PoleGhostUsesTheFinalSnappedPositionWithoutChangingTheDocument()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);

        controller.BeginPole();
        controller.UpdatePointer(new DocumentPoint(13, 17), snapEnabled: true);

        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.NotEmpty(controller.CreatePreviewElements());
        Assert.Equal(
            new DocumentPoint(10, 20),
            controller.ResolvePlacementPosition(new DocumentPoint(13, 17), true));

        Assert.True(controller.Place(new DocumentPoint(13, 17), snapEnabled: true));
        Pole pole = Assert.Single(session.PersistenceSession.Domain.Devices.OfType<Pole>());
        Assert.Equal(new DocumentPoint(10, 20), session.Layout.DrawingLayout.Poles[pole.Id].Position);
        Assert.Single(session.CommandStack.History);
        Assert.True(session.CommandStack.IsDirty);

        controller.Cancel();
        Assert.Empty(controller.CreatePreviewElements());
    }

    [Fact]
    public void RingCabinetGhostUsesTheConfiguredIntervalsAndCommitsOnlyOnClick()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            5,
            includePTInterval: true,
            ptPlacement: RingCabinetPTPlacement.Left);
        var configuration = new RingCabinetCreationConfiguration("Ghost cabinet", template);

        controller.BeginRingCabinet(configuration);
        controller.UpdatePointer(new DocumentPoint(26, 34), snapEnabled: true);
        IReadOnlyList<SceneElement> preview = controller.CreatePreviewElements();

        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.Empty(session.CommandStack.History);
        Assert.False(session.CommandStack.IsDirty);
        Assert.Contains(preview.OfType<SceneText>(), text => text.Text == "PT");
        Assert.True(preview.OfType<SceneLine>().Count() > 5);

        Assert.True(controller.Place(new DocumentPoint(26, 34), snapEnabled: true));
        RingCabinet cabinet = Assert.Single(
            session.PersistenceSession.Domain.Devices.OfType<RingCabinet>());
        Assert.Equal(5, cabinet.Intervals.Count);
        Assert.Equal(1, Assert.Single(cabinet.Intervals, interval =>
            interval.IntervalKind == IntervalKind.PTInterval).BayIndex);
        Assert.Equal(new DocumentPoint(30, 30), session.Layout.RingCabinetLayouts[cabinet.Id].Position);
        Assert.Single(session.CommandStack.History);
        Assert.Equal(PlacementMode.Idle, controller.Mode);
        Assert.Empty(controller.CreatePreviewElements());
    }

    [Fact]
    public void CancelClearsAnUncommittedRingCabinetGhost()
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);
        RingCabinetTemplate template = new RingCabinetCreationTemplateFactory().Create(
            RingCabinetTemplateType.Conventional,
            4);

        controller.BeginRingCabinet(new RingCabinetCreationConfiguration("Canceled", template));
        controller.UpdatePointer(new DocumentPoint(20, 20), snapEnabled: false);
        Assert.NotEmpty(controller.CreatePreviewElements());

        controller.Cancel();

        Assert.Equal(PlacementMode.Idle, controller.Mode);
        Assert.Empty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.Devices);
        Assert.False(session.CommandStack.IsDirty);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical)]
    public void TransformerPlacementUsesTypedCreationAndSelectsNewDevice(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        ProjectRuntimeSession session = CreateSession();
        var controller = new PlacementController(() => session);

        controller.BeginTransformer(kind, orientation);
        controller.UpdatePointer(new DocumentPoint(23, 37), snapEnabled: true);

        Assert.NotEmpty(controller.CreatePreviewElements());
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.True(controller.Place(new DocumentPoint(23, 37), snapEnabled: true));

        Transformer transformer = Assert.Single(session.PersistenceSession.Domain.Transformers);
        Assert.Equal(kind, transformer.TransformerKind);
        Assert.Equal(new DocumentPoint(20, 40), session.Layout.TransformerLayouts[transformer.Id].Position);
        Assert.Equal(orientation, session.Layout.TransformerLayouts[transformer.Id].Orientation);
        Assert.Equal(new SelectionReference(SelectionTargetKind.Device, transformer.Id), session.SelectionManager.Selected);
        Assert.Equal(PlacementMode.Idle, controller.Mode);
    }

    [Fact]
    public void UnifiedDeleteRemovesTransformerAndUndoRestoresExactIdentity()
    {
        ProjectRuntimeSession session = CreateSession();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(30, 40));
        new AddTransformerCommand(session.PersistenceSession.Domain, session.Layout, creation).Execute();
        SelectionSet selection = SelectionSet.Create(
            [new SelectionReference(SelectionTargetKind.Device, creation.Transformer.Id)]);
        ICommand command = new SelectionDeletePlanner().Create(session, selection);

        session.CommandStack.ExecuteCommand(command, session.RebuildScene);
        Assert.Empty(session.PersistenceSession.Domain.Transformers);
        Assert.Empty(session.Layout.TransformerLayouts);

        Assert.True(session.CommandStack.Undo());
        session.RebuildScene();
        Assert.Same(creation.Transformer, Assert.Single(session.PersistenceSession.Domain.Transformers));
        Assert.Same(creation.HvTerminal, Assert.Single(session.PersistenceSession.Domain.Terminals));
        Assert.Same(creation.Layout, session.Layout.TransformerLayouts[creation.Transformer.Id]);
    }

    private ProjectRuntimeSession CreateSession()
    {
        var service = new ProjectService();
        ProjectSession persistence = service.CreateProject(_filePath, "Placement preview");
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
