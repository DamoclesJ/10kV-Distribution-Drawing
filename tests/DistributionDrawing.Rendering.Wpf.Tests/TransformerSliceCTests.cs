using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class TransformerSliceCTests
{
    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, 3, 2, 0)]
    [InlineData(TransformerKind.DedicatedPoleMounted, 2, 0, 1)]
    [InlineData(TransformerKind.PublicIndoor, 2, 0, 0)]
    public void Renderer_UsesDistinctProfessionalGlyph(
        TransformerKind kind,
        int ellipseCount,
        int lineCount,
        int polylineCount)
    {
        TransformerCreation creation = Create(kind);

        IReadOnlyList<SceneElement> elements = new TransformerRenderer().Render(
            creation.Transformer,
            creation.Layout);

        Assert.Equal(ellipseCount, elements.OfType<SceneEllipse>().Count());
        Assert.Equal(lineCount, elements.OfType<SceneLine>().Count());
        Assert.Equal(polylineCount, elements.OfType<ScenePolyline>().Count());
        Assert.Empty(elements.OfType<SceneText>());
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical, 100, 117.5, TerminalAnchorDirection.Down)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical, 100, 88, TerminalAnchorDirection.Up)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal, 86, 100, TerminalAnchorDirection.Left)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical, 100, 86, TerminalAnchorDirection.Up)]
    public void TerminalAnchorIndex_UsesFrozenTransformerAnchor(
        TransformerKind kind,
        TransformerOrientation orientation,
        double expectedX,
        double expectedY,
        TerminalAnchorDirection expectedDirection)
    {
        TransformerCreation creation = Create(kind, orientation);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);

        TerminalAnchor anchor = Assert.Single(anchors.Anchors);
        Assert.Equal(creation.HvTerminal.Id, anchor.TerminalId);
        Assert.Equal(new DocumentPoint(expectedX, expectedY), anchor.Position);
        Assert.Equal(expectedDirection, anchor.Direction);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void DrawingSceneBuilder_RendersSelectableTransformer(TransformerKind kind)
    {
        TransformerCreation creation = Create(kind);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);

        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);

        SelectionReference expected = new(SelectionTargetKind.Device, creation.Transformer.Id);
        Assert.NotNull(scene.HitTestIndex.Find(expected));
        Assert.Equal(expected, scene.HitTestIndex.HitTest(creation.Layout.Position));
        Assert.DoesNotContain(scene.Elements.OfType<SceneText>(), text =>
            text.Text is "公变" or "专变" or "站内公变");
    }

    [Fact]
    public void DrawingSceneBuilder_RejectsMissingTransformerLayout()
    {
        TransformerCreation creation = Create(TransformerKind.PublicIndoor);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = new(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());

        Assert.Throws<InvalidOperationException>(() =>
            new DrawingSceneBuilder().Build(document, runtime));
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, "柱上公变")]
    [InlineData(TransformerKind.DedicatedPoleMounted, "柱上专变")]
    [InlineData(TransformerKind.PublicIndoor, "站内公变")]
    public void ResolverAndInspector_ProjectDerivedTransformerFacts(
        TransformerKind kind,
        string expectedKindText)
    {
        TransformerCreation creation = Create(kind);
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Devices = [creation.Transformer],
            TransformerLayouts = new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            }
        });
        SelectionReference reference = new(SelectionTargetKind.Device, creation.Transformer.Id);

        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(resolver.Resolve(reference));
        PropertyInspectorSnapshot snapshot = new PropertyProjector().Project(resolved);

        Assert.Same(creation.Transformer, resolved.Transformer);
        Assert.Same(creation.Layout, resolved.TransformerLayout);
        Assert.Equal(expectedKindText, snapshot.ObjectTitle);
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "电压等级" && row.DisplayValue == Transformer.TenKilovolts);
    }

    [Fact]
    public void OrientationChangeProducesNewFormalAnchorWithoutChangingDomain()
    {
        TransformerCreation horizontal = Create(
            TransformerKind.PublicIndoor,
            TransformerOrientation.Horizontal);
        TransformerLayout vertical = new(
            horizontal.Transformer.Id,
            horizontal.Layout.Position,
            TransformerOrientation.Vertical,
            horizontal.Transformer.TransformerKind);

        TransformerProfessionalGeometry horizontalGeometry = TransformerProfessionalGeometry.Create(
            horizontal.Transformer,
            horizontal.Layout,
            DrawingMetrics.Default.Transformer);
        TransformerProfessionalGeometry verticalGeometry = TransformerProfessionalGeometry.Create(
            horizontal.Transformer,
            vertical,
            DrawingMetrics.Default.Transformer);

        Assert.NotEqual(horizontalGeometry.HvAnchor, verticalGeometry.HvAnchor);
        Assert.Equal(TransformerKind.PublicIndoor, horizontal.Transformer.TransformerKind);
        Assert.Equal(horizontal.Transformer.Id, vertical.TransformerId);
    }

    [Fact]
    public void DropoutFuseToTransformer_ShortOverheadLineUsesFormalAnchorAndRealPole()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Short OHL");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        var deviceFactory = new DeviceCommandFactory();
        AddPoleCommand pole = deviceFactory.CreateAddPole(
            document,
            runtime,
            new DocumentPoint(20, 40));
        pole.Execute();
        AddPoleSwitchAttachmentCommand fuse = deviceFactory.CreateAddPoleSwitchAttachment(
            document,
            runtime,
            pole.Pole.Id,
            SwitchKind.DropoutFuse,
            new DocumentPoint(15, 0));
        fuse.Execute();
        AddTransformerCommand transformer = deviceFactory.CreateAddTransformer(
            document,
            runtime,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(90, 40));
        transformer.Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            document,
            runtime,
            fuse.Creation.SecondTerminal.Id,
            transformer.Creation.HvTerminal.Id,
            new DocumentPoint(35, 40),
            new DocumentPoint(90, 40));
        line.Execute();

        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            runtime.TransformerLayouts);
        Assert.True(anchors.TryGet(transformer.Creation.HvTerminal.Id, out TerminalAnchor anchor));

        Assert.Equal(pole.Pole.Id, Assert.Single(line.OverheadLine.SupportPoleIds));
        Assert.Contains(anchor.Position, Assert.Single(scene.Routes).Points);
        Assert.DoesNotContain(document.PoleAttachments, attachment =>
            attachment.AttachedDeviceId == transformer.Creation.Transformer.Id);
        Assert.Contains(document.Devices, device =>
            device.Id == fuse.Creation.SwitchDevice.Id && device is SwitchDevice);
    }

    [Fact]
    public void FormalTransformerAnchor_DoesNotEnableGroundingCreation()
    {
        TransformerCreation creation = Create(TransformerKind.PublicIndoor);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);

        Assert.True(anchors.TryGet(creation.HvTerminal.Id, out _));
        Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            document,
            creation.HvTerminal.Id));
    }

    private static TransformerCreation Create(
        TransformerKind kind,
        TransformerOrientation? orientation = null) =>
        new TransformerCreationFactory().Create(kind, new DocumentPoint(100, 100), orientation);

    private static DrawingDocument DocumentWith(TransformerCreation creation)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Transformer scene");
        document.AddTransformer(creation.Transformer, creation.HvTerminal);
        return document;
    }

    private static RuntimeLayoutDocument RuntimeWith(TransformerCreation creation) => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>(),
        transformerLayouts: new Dictionary<Guid, TransformerLayout>
        {
            [creation.Transformer.Id] = creation.Layout
        });
}
