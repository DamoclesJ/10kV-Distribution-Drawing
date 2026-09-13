using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
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
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class TransformerSliceCTests
{
    [Fact]
    public void PublicPoleMountedGeometry_KeepsTInsideMainCircleAndUsesTangentSeparatedSmallCircles()
    {
        TransformerCreation creation = Create(TransformerKind.PublicPoleMounted);
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            creation.Transformer,
            creation.Layout,
            DrawingMetrics.Default.Transformer);
        TransformerDrawingMetrics metrics = DrawingMetrics.Default.Transformer;
        DocumentPoint center = creation.Layout.Position;

        Assert.All(geometry.Lines.SelectMany(line => new[] { line.Start, line.End }), point =>
            Assert.True(Distance(center, point) <= metrics.MainRadius));
        Assert.Equal(
            new DocumentPoint(center.XMillimeters, center.YMillimeters + metrics.MainRadius),
            geometry.HvAnchor);

        DocumentPoint left = CircleCenter(geometry.Circles[1]);
        DocumentPoint right = CircleCenter(geometry.Circles[2]);
        Assert.Equal(metrics.MainRadius + metrics.SmallCircleRadius, Distance(center, left), 8);
        Assert.Equal(metrics.MainRadius + metrics.SmallCircleRadius, Distance(center, right), 8);
        Assert.True(Distance(left, right) > metrics.SmallCircleRadius * 2);
        Assert.True(metrics.SmallCircleRadius >= 2.5 * 1.5);
    }

    [Fact]
    public void DedicatedPoleMountedGeometry_UsesTangentSeparatedSmallCircles()
    {
        TransformerCreation creation = Create(TransformerKind.DedicatedPoleMounted);
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            creation.Transformer,
            creation.Layout,
            DrawingMetrics.Default.Transformer);
        TransformerDrawingMetrics metrics = DrawingMetrics.Default.Transformer;
        DocumentPoint left = CircleCenter(geometry.Circles[0]);
        DocumentPoint right = CircleCenter(geometry.Circles[1]);
        double triangleBaseY = creation.Layout.Position.YMillimeters + metrics.TriangleBaseY;

        Assert.Equal(triangleBaseY, left.YMillimeters - metrics.SmallCircleRadius, 8);
        Assert.Equal(triangleBaseY, right.YMillimeters - metrics.SmallCircleRadius, 8);
        Assert.True(left.XMillimeters < creation.Layout.Position.XMillimeters);
        Assert.True(right.XMillimeters > creation.Layout.Position.XMillimeters);
        Assert.True(Distance(left, right) > metrics.SmallCircleRadius * 2);
    }

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
        SceneText label = Assert.Single(elements.OfType<SceneText>());
        Assert.Equal("测试变压器", label.Text);
        Assert.Equal(DrawingMetrics.Default.Typography.TransformerNameFontSize,
            label.FontSizeMillimeters);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical, 100, 112, TerminalAnchorDirection.Down)]
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
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical)]
    public void DrawingSceneBuilder_RendersOneSelectableNameLabelBelowAndCentered(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        TransformerCreation creation = Create(kind, orientation);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);

        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtime);

        SelectionReference expected = new(SelectionTargetKind.Device, creation.Transformer.Id);
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            creation.Transformer,
            creation.Layout,
            DrawingMetrics.Default.Transformer);
        SceneText label = Assert.Single(scene.Elements.OfType<SceneText>(), text =>
            text.Text == creation.Transformer.DisplayName);
        Assert.Null(label.TargetKind);
        Assert.Null(label.TargetId);
        Assert.Equal(2, scene.HitTestIndex.FindAll(expected).Count);
        Assert.All(scene.HitTestIndex.FindAll(expected), entry =>
            Assert.Equal(expected, entry.Target));
        Assert.Equal(expected, scene.HitTestIndex.HitTest(creation.Layout.Position));
        DocumentRect labelBounds = Assert.IsType<DocumentRect>(label.HitTestBounds);
        Assert.Equal(expected, scene.HitTestIndex.HitTest(new DocumentPoint(
            labelBounds.XMillimeters + labelBounds.WidthMillimeters / 2,
            labelBounds.YMillimeters + labelBounds.HeightMillimeters / 2)));
        double glyphCenterX = geometry.Bounds.XMillimeters +
            geometry.Bounds.WidthMillimeters / 2;
        double glyphBottom = geometry.Bounds.YMillimeters +
            geometry.Bounds.HeightMillimeters;
        Assert.Equal(SceneTextHorizontalAlignment.Center, label.HorizontalAlignment);
        Assert.Equal(glyphCenterX, label.Origin.XMillimeters, 8);
        Assert.Equal(
            glyphCenterX,
            labelBounds.XMillimeters + labelBounds.WidthMillimeters / 2,
            8);
        Assert.Equal(
            glyphBottom + DrawingMetrics.Default.Transformer.NameLabelGap,
            label.Origin.YMillimeters,
            8);
        Assert.True(labelBounds.YMillimeters >= glyphBottom);
        TerminalAnchor anchor = Anchor(document, runtime, creation.HvTerminal.Id);
        Assert.Equal(geometry.HvAnchor, anchor.Position);
        Assert.Equal(geometry.HvDirection, anchor.Direction);
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
    [InlineData(TransformerKind.PublicPoleMounted, "柱上公变", true)]
    [InlineData(TransformerKind.DedicatedPoleMounted, "柱上专变", true)]
    [InlineData(TransformerKind.PublicIndoor, "站内公变", false)]
    public void ResolverAndInspector_ProjectDerivedTransformerFacts(
        TransformerKind kind,
        string expectedKindText,
        bool orientationIsReadOnly)
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
        Assert.Equal("测试变压器", snapshot.ObjectTitle);
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "业务类型" && row.DisplayValue == expectedKindText);
        PropertyRowViewModel name = Assert.Single(
            snapshot.Sections.SelectMany(section => section.Properties),
            row => row.PropertyKey == PropertyCommandFactory.TransformerDisplayNamePropertyKey);
        Assert.Equal("变压器名称", name.DisplayName);
        Assert.Equal("测试变压器", name.DisplayValue);
        Assert.False(name.IsReadOnly);
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "电压等级" && row.DisplayValue == Transformer.TenKilovolts);
        PropertyRowViewModel orientation = Assert.Single(
            snapshot.Sections.SelectMany(section => section.Properties),
            row => row.PropertyKey == "Orientation");
        Assert.Equal(orientationIsReadOnly, orientation.IsReadOnly);
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
    public void RenameTransformerCommand_TrimsAndPreservesIdentityThroughUndoRedo()
    {
        TransformerCreation creation = Create(TransformerKind.PublicIndoor);
        Guid transformerId = creation.Transformer.Id;
        Guid terminalId = creation.Transformer.HvTerminalId;
        TransformerKind kind = creation.Transformer.TransformerKind;
        var command = new RenameTransformerCommand(creation.Transformer, "  新名称  ");

        command.Execute();
        Assert.Equal("新名称", creation.Transformer.DisplayName);
        command.Undo();
        Assert.Equal("测试变压器", creation.Transformer.DisplayName);
        command.Redo();

        Assert.Equal("新名称", creation.Transformer.DisplayName);
        Assert.Equal(transformerId, creation.Transformer.Id);
        Assert.Equal(terminalId, creation.Transformer.HvTerminalId);
        Assert.Equal(kind, creation.Transformer.TransformerKind);
    }

    [Fact]
    public void LegacyIncompleteRename_UndoRedoRestoresPresentationAndInspectorState()
    {
        TransformerCreation creation = CreateLegacyIncomplete(TransformerKind.PublicIndoor);
        var command = new RenameTransformerCommand(creation.Transformer, "补录名称");

        Assert.Empty(new TransformerRenderer().Render(
            creation.Transformer, creation.Layout).OfType<SceneText>());
        PropertyInspectorSnapshot incomplete = Project(creation);
        Assert.Contains(incomplete.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "名称状态" && row.DisplayValue == "历史工程待补录");

        command.Execute();
        Assert.False(creation.Transformer.IsLegacyNamingIncomplete);
        Assert.Equal("补录名称", Assert.Single(new TransformerRenderer().Render(
            creation.Transformer, creation.Layout).OfType<SceneText>()).Text);

        command.Undo();
        Assert.True(creation.Transformer.IsLegacyNamingIncomplete);
        Assert.Null(creation.Transformer.DisplayName);
        Assert.Empty(new TransformerRenderer().Render(
            creation.Transformer, creation.Layout).OfType<SceneText>());

        command.Redo();
        Assert.False(creation.Transformer.IsLegacyNamingIncomplete);
        Assert.Equal("补录名称", creation.Transformer.DisplayName);
    }

    [Fact]
    public void TransformerPropertyEditor_ValidatesNoChangeAndTracksDirtyState()
    {
        TransformerCreation creation = Create(TransformerKind.DedicatedPoleMounted);
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Devices = [creation.Transformer],
            TransformerLayouts = new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            }
        });
        var stack = new CommandStack();
        stack.MarkSaved();
        var editor = new PropertyEditor(resolver, stack);
        var reference = new SelectionReference(SelectionTargetKind.Device, creation.Transformer.Id);

        PropertyEditResult invalid = editor.TryEdit(
            reference,
            PropertyCommandFactory.TransformerDisplayNamePropertyKey,
            "   ");
        Assert.False(invalid.IsSuccess);
        Assert.Equal("InputInvalid", invalid.ErrorCode);
        Assert.False(stack.IsDirty);

        PropertyEditResult noChange = editor.TryEdit(
            reference,
            PropertyCommandFactory.TransformerDisplayNamePropertyKey,
            " 测试变压器 ");
        Assert.False(noChange.IsSuccess);
        Assert.Equal("NoChange", noChange.ErrorCode);
        Assert.False(stack.IsDirty);

        Assert.True(editor.TryEdit(
            reference,
            PropertyCommandFactory.TransformerDisplayNamePropertyKey,
            " 新名称 ").IsSuccess);
        Assert.Equal("新名称", creation.Transformer.DisplayName);
        Assert.True(stack.IsDirty);
        Assert.True(stack.Undo());
        Assert.Equal("测试变压器", creation.Transformer.DisplayName);
        Assert.False(stack.IsDirty);
        Assert.True(stack.Redo());
        Assert.Equal("新名称", creation.Transformer.DisplayName);
        Assert.True(stack.IsDirty);
    }

    [Fact]
    public void SetPublicIndoorOrientation_ExecuteUndoRedoUpdatesAnchorAndConnectedCableRoute()
    {
        TransformerCreation first = Create(
            TransformerKind.PublicIndoor,
            TransformerOrientation.Horizontal);
        TransformerCreation second = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(160, 100),
            "第二台变压器");
        DrawingDocument document = DocumentWith(first);
        document.AddTransformer(second.Transformer, second.HvTerminal);
        RuntimeLayoutDocument runtime = RuntimeWith(first);
        runtime.AddTransformer(second.Layout, second.Transformer.TransformerKind);
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            first.HvTerminal.Id,
            second.HvTerminal.Id,
            "Transformer cable",
            Transformer.TenKilovolts);
        document.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(),
                "Transformer cable",
                "YJV",
                60,
                Transformer.TenKilovolts,
                connectionId,
                first.HvTerminal.Id,
                second.HvTerminal.Id),
            connection);
        Guid transformerId = first.Transformer.Id;
        Guid terminalId = first.Transformer.HvTerminalId;
        TransformerKind kind = first.Transformer.TransformerKind;
        var stack = new CommandStack();
        var command = new SetTransformerOrientationCommand(
            runtime,
            first.Transformer,
            TransformerOrientation.Vertical);

        stack.ExecuteCommand(command);
        DrawingScene verticalScene = new DrawingSceneBuilder().Build(document, runtime);
        TerminalAnchor verticalAnchor = Anchor(document, runtime, terminalId);
        Assert.Equal(TransformerOrientation.Vertical, runtime.TransformerLayouts[transformerId].Orientation);
        Assert.Equal(verticalAnchor.Position, Assert.Single(verticalScene.Routes).Points[0]);
        Assert.Equal(new DocumentPoint(100, 86), verticalAnchor.Position);

        Assert.True(stack.Undo());
        DrawingScene horizontalScene = new DrawingSceneBuilder().Build(document, runtime);
        TerminalAnchor horizontalAnchor = Anchor(document, runtime, terminalId);
        Assert.Equal(TransformerOrientation.Horizontal, runtime.TransformerLayouts[transformerId].Orientation);
        Assert.Equal(horizontalAnchor.Position, Assert.Single(horizontalScene.Routes).Points[0]);
        Assert.Equal(new DocumentPoint(86, 100), horizontalAnchor.Position);

        Assert.True(stack.Redo());
        Assert.Equal(TransformerOrientation.Vertical, runtime.TransformerLayouts[transformerId].Orientation);
        Assert.Equal(transformerId, first.Transformer.Id);
        Assert.Equal(terminalId, first.Transformer.HvTerminalId);
        Assert.Equal(kind, first.Transformer.TransformerKind);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    public void SetTransformerOrientation_RejectsPoleMountedKinds(TransformerKind kind)
    {
        TransformerCreation creation = Create(kind);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);

        Assert.Throws<InvalidOperationException>(() =>
            new SetTransformerOrientationCommand(
                runtime,
                creation.Transformer,
                TransformerOrientation.Horizontal));
        Assert.Equal(TransformerOrientation.Vertical,
            runtime.TransformerLayouts[creation.Transformer.Id].Orientation);
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
            new DocumentPoint(90, 40),
            "测试变压器");
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
        Assert.True(anchors.TryGet(fuse.Creation.SecondTerminal.Id, out TerminalAnchor fuseAnchor));

        Assert.Equal(pole.Pole.Id, Assert.Single(line.OverheadLine.SupportPoleIds));
        OrthogonalRoute route = Assert.Single(scene.Routes);
        Assert.Equal(fuseAnchor.Position, route.Points[0]);
        Assert.Equal(anchor.Position, route.Points[^1]);
        Assert.DoesNotContain(
            PoleProfessionalGeometry.GetPoleCenter(pole.Layout),
            route.Points.Skip(1).Take(route.Points.Count - 2));
        Assert.DoesNotContain(document.PoleAttachments, attachment =>
            attachment.AttachedDeviceId == transformer.Creation.Transformer.Id);
        Assert.Contains(document.Devices, device =>
            device.Id == fuse.Creation.SwitchDevice.Id && device is SwitchDevice);
    }

    [Fact]
    public void FormalTransformerAnchor_EnablesGroundingCreation()
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
        Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            document,
            creation.HvTerminal.Id));
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void TransformerHvGrounding_UsesExactTerminalAndDefaultLocation(
        TransformerKind kind)
    {
        TransformerCreation creation = Create(kind);
        DrawingDocument document = DocumentWith(creation);
        ICommand command = new ProfessionalCommandFactory().CreateAddGroundingPoint(
            document,
            creation.HvTerminal.Id,
            groundingPointId: Guid.NewGuid());

        command.Execute();

        GroundingPoint grounding = Assert.Single(document.GroundingPoints);
        Assert.Equal(GroundingTarget.ForTerminal(creation.Transformer.HvTerminalId), grounding.Target);
        Assert.Equal("变压器高压侧", grounding.Location);
        Assert.Throws<InvalidOperationException>(() =>
            document.RemoveDevice(creation.Transformer.Id));
        command.Undo();
        Assert.Empty(document.GroundingPoints);
        command.Redo();
        GroundingPoint restored = Assert.Single(document.GroundingPoints);
        Assert.Equal(grounding.GroundingPointId, restored.GroundingPointId);
        Assert.Equal(
            GroundingTarget.ForTerminal(creation.Transformer.HvTerminalId),
            restored.Target);
        command.Undo();
        document.RemoveDevice(creation.Transformer.Id);
        Assert.DoesNotContain(creation.Transformer, document.Devices);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransformerGroundingPresentation_FollowsConnectedRouteOrientation(
        bool horizontalIncoming)
    {
        TransformerCreation creation = Create(TransformerKind.PublicIndoor);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        var otherDevice = new Device(Guid.NewGuid(), DeviceType.PT);
        var otherTerminal = new Terminal(
            Guid.NewGuid(), TopologyOwnerType.Device, otherDevice.Id, "Cable",
            Transformer.TenKilovolts, true, false,
            allowedConnectionTypes: [ConnectionType.Cable]);
        document.AddDevice(otherDevice);
        document.AddTerminal(otherTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.Cable,
            creation.HvTerminal.Id, otherTerminal.Id,
            "测试电缆", Transformer.TenKilovolts);
        document.AddConnection(connection);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(creation.HvTerminal.Id),
            "变压器高压侧", "S01");
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);
        Assert.True(anchors.TryGet(creation.HvTerminal.Id, out TerminalAnchor terminalAnchor));
        DocumentPoint end = horizontalIncoming
            ? new DocumentPoint(terminalAnchor.Position.XMillimeters - 40, terminalAnchor.Position.YMillimeters)
            : new DocumentPoint(terminalAnchor.Position.XMillimeters, terminalAnchor.Position.YMillimeters - 40);
        var route = new OrthogonalRoute(
            connection.Id,
            connection.Type,
            connection.StartTerminalId,
            connection.EndTerminalId,
            [terminalAnchor.Position, end]);

        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            point,
            document,
            runtime.DrawingLayout,
            anchors,
            new Dictionary<Guid, OrthogonalRoute> { [connection.Id] = route },
            runtime.TransformerLayouts,
            null,
            out GroundingPresentationAnchor presentation));
        GroundingPointResolvedLayout resolved = new GroundingPointLayoutResolver().Resolve(
            point,
            presentation,
            null);
        var manualLayout = new GroundingPointLayout(
            point.GroundingPointId,
            new DocumentPoint(7, -3));
        GroundingPointResolvedLayout manual = new GroundingPointLayoutResolver().Resolve(
            point,
            presentation,
            manualLayout);
        Assert.Equal(
            new DocumentPoint(
                resolved.DefaultSymbolTop.XMillimeters + 7,
                resolved.DefaultSymbolTop.YMillimeters - 3),
            manual.SymbolTop);

        if (horizontalIncoming)
        {
            Assert.Equal(TerminalAnchorDirection.Down, presentation.Direction);
            Assert.True(Assert.Single(resolved.LeaderSegments).IsVertical);
        }
        else
        {
            Assert.Contains(presentation.Direction,
                new[] { TerminalAnchorDirection.Left, TerminalAnchorDirection.Right });
            Assert.True(resolved.LeaderSegments[0].IsHorizontal);
        }
        Assert.Equal(creation.HvTerminal.Id, point.Target.TargetId);
    }

    [Fact]
    public void TransformerGroundingPresentation_VerticalIncomingChoosesLeftAwayFromBody()
    {
        TransformerCreation creation = Create(
            TransformerKind.PublicIndoor,
            TransformerOrientation.Horizontal);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        var otherDevice = new Device(Guid.NewGuid(), DeviceType.PT);
        var otherTerminal = new Terminal(
            Guid.NewGuid(), TopologyOwnerType.Device, otherDevice.Id, "Cable",
            Transformer.TenKilovolts, true, false,
            allowedConnectionTypes: [ConnectionType.Cable]);
        document.AddDevice(otherDevice);
        document.AddTerminal(otherTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.Cable,
            creation.HvTerminal.Id, otherTerminal.Id,
            "Vertical incoming cable", Transformer.TenKilovolts);
        document.AddConnection(connection);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(creation.HvTerminal.Id),
            "变压器高压侧", "S01");
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);
        Assert.True(anchors.TryGet(
            creation.HvTerminal.Id,
            out TerminalAnchor terminalAnchor));
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            creation.Transformer,
            creation.Layout,
            DrawingMetrics.Default.Transformer);
        double bodyCenterX = geometry.Bounds.XMillimeters +
            geometry.Bounds.WidthMillimeters / 2;
        Assert.True(terminalAnchor.Position.XMillimeters < bodyCenterX);
        var route = new OrthogonalRoute(
            connection.Id,
            connection.Type,
            connection.StartTerminalId,
            connection.EndTerminalId,
            [
                terminalAnchor.Position,
                new DocumentPoint(
                    terminalAnchor.Position.XMillimeters,
                    terminalAnchor.Position.YMillimeters - 40)
            ]);

        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            point,
            document,
            runtime.DrawingLayout,
            anchors,
            new Dictionary<Guid, OrthogonalRoute> { [connection.Id] = route },
            runtime.TransformerLayouts,
            null,
            out GroundingPresentationAnchor presentation));
        GroundingPointResolvedLayout resolved = new GroundingPointLayoutResolver().Resolve(
            point,
            presentation,
            null);

        Assert.Equal(GroundingPresentationPolicy.TransformerTerminal, presentation.Policy);
        Assert.Equal(TerminalAnchorDirection.Left, presentation.Direction);
        Assert.True(resolved.LeaderSegments[0].IsHorizontal);
        Assert.Equal(creation.HvTerminal.Id, point.Target.TargetId);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void TransformerGroundingPresentation_WithoutConnectionUsesFormalDirection(
        TransformerKind kind)
    {
        TransformerCreation creation = Create(kind);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(creation.HvTerminal.Id),
            "变压器高压侧", "S01");
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);
        Assert.True(anchors.TryGet(
            creation.HvTerminal.Id,
            out TerminalAnchor formalAnchor));
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            creation.Transformer,
            creation.Layout,
            DrawingMetrics.Default.Transformer);

        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            point,
            document,
            runtime.DrawingLayout,
            anchors,
            new Dictionary<Guid, OrthogonalRoute>(),
            runtime.TransformerLayouts,
            null,
            out GroundingPresentationAnchor presentation));

        Assert.Equal(formalAnchor.Direction, presentation.Direction);
        switch (formalAnchor.Direction)
        {
            case TerminalAnchorDirection.Left:
                Assert.Equal(geometry.Bounds.XMillimeters, presentation.Position.XMillimeters);
                break;
            case TerminalAnchorDirection.Right:
                Assert.Equal(
                    geometry.Bounds.XMillimeters + geometry.Bounds.WidthMillimeters,
                    presentation.Position.XMillimeters);
                break;
            case TerminalAnchorDirection.Up:
                Assert.Equal(geometry.Bounds.YMillimeters, presentation.Position.YMillimeters);
                break;
            case TerminalAnchorDirection.Down:
                Assert.Equal(
                    geometry.Bounds.YMillimeters + geometry.Bounds.HeightMillimeters,
                    presentation.Position.YMillimeters);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        Assert.Equal(GroundingPresentationPolicy.TransformerTerminal, presentation.Policy);
        Assert.Equal(creation.HvTerminal.Id, point.Target.TargetId);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical)]
    public void TransformerGroundingManualPlacement_LeaderStartsTowardSymbol(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        TransformerCreation creation = Create(
            kind,
            kind == TransformerKind.PublicIndoor ? orientation : null);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        GroundingPoint point = document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForTerminal(creation.HvTerminal.Id),
            "变压器高压侧",
            "S01");
        GroundingPresentationAnchor presentation = ResolvePresentation(point, document, runtime);
        var resolver = new GroundingPointLayoutResolver();
        GroundingPointResolvedLayout automatic = resolver.Resolve(point, presentation, null);

        foreach (double side in new[] { -1d, 1d })
        {
            double targetX = presentation.Position.XMillimeters + side * 40;
            var offset = new DocumentPoint(
                targetX - automatic.DefaultSymbolTop.XMillimeters,
                8);
            var manualLayout = new GroundingPointLayout(point.GroundingPointId, offset);
            GroundingPointResolvedLayout manual = resolver.Resolve(
                point,
                presentation,
                manualLayout);
            OrthogonalRouteSegment first = Assert.Single(
                manual.LeaderSegments.Take(1));
            Assert.True(first.IsHorizontal);
            Assert.Equal(side, Math.Sign(
                first.End.XMillimeters - first.Start.XMillimeters));
            Assert.Equal(offset, manualLayout.SymbolOffset);
            Assert.Equal(creation.HvTerminal.Id, point.Target.TargetId);
            Assert.DoesNotContain(manual.LeaderSegments.Zip(
                    manual.LeaderSegments.Skip(1)),
                pair => pair.First.IsHorizontal && pair.Second.IsHorizontal &&
                        Math.Sign(pair.First.End.XMillimeters - pair.First.Start.XMillimeters) ==
                        -Math.Sign(pair.Second.End.XMillimeters - pair.Second.Start.XMillimeters));
        }
    }

    [Fact]
    public void PublicIndoorOrientationChange_ExistingGroundingKeepsIdentityAndFollowsAnchor()
    {
        TransformerCreation creation = Create(
            TransformerKind.PublicIndoor,
            TransformerOrientation.Horizontal);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        GroundingPoint grounding = document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForTerminal(creation.HvTerminal.Id),
            "变压器高压侧",
            "S01");
        Guid groundingPointId = grounding.GroundingPointId;
        Guid hvTerminalId = creation.Transformer.HvTerminalId;
        GroundingTarget target = grounding.Target;
        TerminalAnchor formalBefore = Anchor(document, runtime, hvTerminalId);
        GroundingPresentationAnchor presentationBefore = ResolvePresentation(
            grounding,
            document,
            runtime);
        var stack = new CommandStack();
        var command = new SetTransformerOrientationCommand(
            runtime,
            creation.Transformer,
            TransformerOrientation.Vertical);

        stack.ExecuteCommand(command);

        GroundingPoint afterExecute = Assert.Single(document.GroundingPoints);
        TerminalAnchor formalAfter = Anchor(document, runtime, hvTerminalId);
        GroundingPresentationAnchor presentationAfter = ResolvePresentation(
            afterExecute,
            document,
            runtime);
        Assert.Equal(hvTerminalId, creation.Transformer.HvTerminalId);
        Assert.Equal(groundingPointId, afterExecute.GroundingPointId);
        Assert.Equal(target, afterExecute.Target);
        Assert.NotEqual(formalBefore.Position, formalAfter.Position);
        Assert.NotEqual(presentationBefore.Position, presentationAfter.Position);

        Assert.True(stack.Undo());
        GroundingPoint afterUndo = Assert.Single(document.GroundingPoints);
        Assert.Equal(hvTerminalId, creation.Transformer.HvTerminalId);
        Assert.Equal(groundingPointId, afterUndo.GroundingPointId);
        Assert.Equal(target, afterUndo.Target);
        Assert.Equal(formalBefore, Anchor(document, runtime, hvTerminalId));
        Assert.Equal(
            presentationBefore,
            ResolvePresentation(afterUndo, document, runtime));

        Assert.True(stack.Redo());
        GroundingPoint afterRedo = Assert.Single(document.GroundingPoints);
        Assert.Equal(hvTerminalId, creation.Transformer.HvTerminalId);
        Assert.Equal(groundingPointId, afterRedo.GroundingPointId);
        Assert.Equal(target, afterRedo.Target);
        Assert.Equal(formalAfter, Anchor(document, runtime, hvTerminalId));
        Assert.Equal(
            presentationAfter,
            ResolvePresentation(afterRedo, document, runtime));
    }

    private static TransformerCreation Create(
        TransformerKind kind,
        TransformerOrientation? orientation = null) =>
        new TransformerCreationFactory().Create(
            kind,
            new DocumentPoint(100, 100),
            "测试变压器",
            orientation);

    private static TransformerCreation CreateLegacyIncomplete(TransformerKind kind)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        Transformer transformer = Transformer.RestoreLegacy(
            transformerId,
            kind,
            terminalId,
            displayName: null);
        var terminal = new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            transformerId,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: false,
            electricalNodeId: null,
            allowedConnectionTypes: [transformer.AllowedConnectionType]);
        var layout = new TransformerLayout(
            transformerId,
            new DocumentPoint(100, 100),
            kind == TransformerKind.PublicIndoor
                ? TransformerOrientation.Horizontal
                : TransformerOrientation.Vertical,
            kind);
        return new TransformerCreation(transformer, terminal, layout);
    }

    private static PropertyInspectorSnapshot Project(TransformerCreation creation)
    {
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Devices = [creation.Transformer],
            TransformerLayouts = new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            }
        });
        return new PropertyProjector().Project(resolver.Resolve(
            new SelectionReference(SelectionTargetKind.Device, creation.Transformer.Id)));
    }

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

    private static TerminalAnchor Anchor(
        DrawingDocument document,
        RuntimeLayoutDocument runtime,
        Guid terminalId)
    {
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            runtime.TransformerLayouts);
        Assert.True(anchors.TryGet(terminalId, out TerminalAnchor anchor));
        return anchor;
    }

    private static GroundingPresentationAnchor ResolvePresentation(
        GroundingPoint groundingPoint,
        DrawingDocument document,
        RuntimeLayoutDocument runtime)
    {
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            transformerLayouts: runtime.TransformerLayouts);
        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            groundingPoint,
            document,
            runtime.DrawingLayout,
            anchors,
            new Dictionary<Guid, OrthogonalRoute>(),
            runtime.TransformerLayouts,
            null,
            out GroundingPresentationAnchor presentation));
        return presentation;
    }

    private static DocumentPoint CircleCenter(DocumentRect circle) => new(
        circle.XMillimeters + circle.WidthMillimeters / 2,
        circle.YMillimeters + circle.HeightMillimeters / 2);

    private static double Distance(DocumentPoint first, DocumentPoint second) => Math.Sqrt(
        Math.Pow(first.XMillimeters - second.XMillimeters, 2) +
        Math.Pow(first.YMillimeters - second.YMillimeters, 2));
}
