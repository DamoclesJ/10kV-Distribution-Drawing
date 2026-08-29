using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using DistributionDrawing.Rendering.Wpf.Symbols.Library.Definitions;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class PoleProfessionalSymbolTests
{
    public static IEnumerable<object[]> PoleSwitchKindsAndRotations()
    {
        SwitchKind[] kinds =
        [
            SwitchKind.IsolationSwitch,
            SwitchKind.LoadSwitch,
            SwitchKind.CircuitBreaker,
            SwitchKind.DropoutFuse
        ];
        foreach (SwitchKind kind in kinds)
        {
            for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
            {
                yield return [kind, quarterTurns];
            }
        }
    }

    [Fact]
    public void CementPole_UsesOneCircleAndNoLegacyPoleLines()
    {
        PoleCreationResult result = new PoleCreationFactory().Create("45300001");

        IReadOnlyList<SceneElement> elements = new PoleRenderer().Render(
            result.Pole,
            new PoleLayout(result.Pole.Id, new DocumentPoint(20, 30)));

        SceneEllipse circle = Assert.Single(elements.OfType<SceneEllipse>());
        Assert.Equal(circle.Bounds.WidthMillimeters, circle.Bounds.HeightMillimeters);
        Assert.Empty(elements.OfType<SceneLine>());
        Assert.Single(elements.OfType<SceneText>(), text => text.Text == "45300001");
    }

    [Theory]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void PoleSwitch_OpenAndClosedUseDifferentProfessionalGeometry(SwitchKind kind)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "45300002",
            PoleType.Cement,
            null,
            [kind],
            includeCableTerminal: false);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var document = CreateDocument(result);
        PoleLayout poleLayout = new(result.Pole.Id, new DocumentPoint(20, 30));
        AttachmentLayout attachmentLayout = new(
            attachment.AttachmentId,
            new DocumentPoint(18, 2));
        var renderer = new SwitchAttachmentRenderer();

        IReadOnlyList<SceneElement> open = renderer.Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(attachment, switchDevice, attachmentLayout)]);
        document.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        IReadOnlyList<SceneElement> closed = renderer.Render(
            result.Pole,
            poleLayout,
            [new SwitchAttachmentRenderInput(attachment, switchDevice, attachmentLayout)]);

        Assert.False(open.SequenceEqual(closed));
        Assert.DoesNotContain(open.OfType<SceneText>(), text => text.Text is "合" or "分");
        Assert.DoesNotContain(closed.OfType<SceneText>(), text => text.Text is "合" or "分");
        Assert.Equal(SwitchState.Closed, switchDevice.SwitchState);
    }

    [Fact]
    public void FourPoleSwitchKindsUseDistinctGeometryContracts()
    {
        SceneElement[][] geometries =
        [
            RenderSwitch(SwitchKind.CircuitBreaker),
            RenderSwitch(SwitchKind.LoadSwitch),
            RenderSwitch(SwitchKind.IsolationSwitch),
            RenderSwitch(SwitchKind.DropoutFuse)
        ];

        Assert.Contains(geometries[0], element => element is SceneRectangle);
        Assert.Contains(geometries[1], element => element is SceneEllipse ellipse &&
            ellipse.Bounds.WidthMillimeters < 14);
        Assert.Contains(geometries[2], element => element is SceneRectangle);
        Assert.NotEqual(geometries[0], geometries[2]);
        Assert.Contains(geometries[3], element => element is ScenePolyline polyline && polyline.IsClosed);
    }

    [Theory]
    [MemberData(nameof(PoleSwitchKindsAndRotations))]
    public void PoleSwitch_InternalProfessionalGeometryRotatesAsOneRigidAttachment(
        SwitchKind kind,
        int quarterTurns)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [kind],
            includeCableTerminal: false);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        PoleLayout poleLayout = new(result.Pole.Id, new DocumentPoint(40, 50));
        AttachmentLayout right = new(
            attachment.AttachmentId,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(kind));
        AttachmentLayout rotated = right.RotateBy(quarterTurns);
        var library = new SymbolLibrary();
        SceneElement[] baseline = library.CreateAttachment(
                attachment,
                device,
                poleLayout,
                right,
                includeLabel: false)
            .ToArray();
        SceneElement[] actual = library.CreateAttachment(
                attachment,
                device,
                poleLayout,
                rotated,
                includeLabel: false)
            .ToArray();
        SceneElement[] expected = baseline
            .Select(element => RotateElement(element, poleLayout, quarterTurns))
            .ToArray();

        SceneElementAssertions.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(PoleSwitchKindsAndRotations))]
    public void PoleSwitch_RotationKeepsTerminalIdentityAndAlignsAnchorsAndHitBounds(
        SwitchKind kind,
        int quarterTurns)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [kind],
            includeCableTerminal: false);
        var document = CreateDocument(result);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        Terminal[] terminals = device.TerminalIds
            .Select(id => document.Terminals.Single(terminal => terminal.Id == id))
            .ToArray();
        Guid?[] nodeIds = terminals.Select(terminal => terminal.ElectricalNodeId).ToArray();
        PoleLayout poleLayout = new(result.Pole.Id, new DocumentPoint(40, 50));
        AttachmentLayout attachmentLayout = new AttachmentLayout(
                attachment.AttachmentId,
                PoleProfessionalGeometry.GetDefaultAttachmentOffset(kind))
            .RotateBy(quarterTurns);
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(poleLayout);
        drawingLayout.Add(attachmentLayout);
        var runtimeLayout = new RuntimeLayoutDocument(
            drawingLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        PoleAttachmentGeometry geometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            poleLayout,
            attachmentLayout,
            SymbolLibrary.ResolveAttachmentKind(device));

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            drawingLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        Assert.True(anchors.TryGet(device.TerminalIds[0], out TerminalAnchor firstAnchor));
        Assert.True(anchors.TryGet(device.TerminalIds[1], out TerminalAnchor secondAnchor));
        Assert.Equal(geometry.FirstTerminal, firstAnchor.Position);
        Assert.Equal(geometry.SecondTerminal, secondAnchor.Position);

        DrawingScene scene = new DrawingSceneBuilder().Build(document, runtimeLayout);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == DistributionDrawing.Rendering.Wpf.Interaction.SelectionTargetKind.Device &&
            entry.Target.ObjectId == device.Id &&
            entry.Bounds == geometry.LogicalBounds);
        Assert.Equal(terminals.Select(terminal => terminal.Id), device.TerminalIds);
        Assert.Equal(nodeIds, terminals.Select(terminal => terminal.ElectricalNodeId));
    }

    [Fact]
    public void PoleSwitch_RotationCommandUndoRedoRestoresInternalGeometry()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [SwitchKind.CircuitBreaker],
            includeCableTerminal: false);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        PoleLayout poleLayout = new(result.Pole.Id, new DocumentPoint(40, 50));
        AttachmentLayout before = new(
            attachment.AttachmentId,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(SwitchKind.CircuitBreaker));
        AttachmentLayout after = before.RotateBy(1);
        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(poleLayout);
        drawingLayout.Add(before);
        var command = new ChangeAttachmentLayoutCommand(drawingLayout, before, after);
        var library = new SymbolLibrary();
        SceneElement[] right = library.CreateAttachment(
            attachment, device, poleLayout, before, includeLabel: false).ToArray();

        command.Execute();
        SceneElement[] down = library.CreateAttachment(
            attachment, device, poleLayout,
            drawingLayout.Attachments[attachment.AttachmentId],
            includeLabel: false).ToArray();
        command.Undo();
        SceneElement[] undone = library.CreateAttachment(
            attachment, device, poleLayout,
            drawingLayout.Attachments[attachment.AttachmentId],
            includeLabel: false).ToArray();
        command.Redo();
        SceneElement[] redone = library.CreateAttachment(
            attachment, device, poleLayout,
            drawingLayout.Attachments[attachment.AttachmentId],
            includeLabel: false).ToArray();

        Assert.NotEqual(Geometry(right), Geometry(down));
        SceneElementAssertions.Equal(right, undone);
        SceneElementAssertions.Equal(down, redone);
        Assert.Equal(device.Id, attachment.AttachedDeviceId);
    }

    [Fact]
    public void PoleSwitch_RotationReroutesConnectedOverheadLineWithoutChangingTopologyIdentity()
    {
        var factory = new PoleCreationFactory();
        PoleCreationResult switched = factory.CreateWithAttachments(
            "P-1",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: false);
        PoleCreationResult target = factory.Create("P-2");
        var document = new DrawingDocument(Guid.NewGuid(), "Pole switch route rotation");
        AddPoleAggregate(document, switched);
        AddPoleAggregate(document, target);
        SwitchDevice switchDevice = Assert.Single(switched.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(switched.Attachments);
        Guid targetTerminalId = Assert.Single(target.Pole.OverheadAnchorTerminalIds);
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            switchDevice.TerminalIds[1],
            targetTerminalId,
            "架空线路",
            "10kV");
        var overheadLine = new OverheadLine(
            connection.Id,
            "JKLYJ-10kV",
            [switched.Pole.Id, target.Pole.Id]);
        document.AddConnection(connection);
        document.AddOverheadLine(overheadLine);
        var drawingLayout = new DrawingLayout();
        var switchedPoleLayout = new PoleLayout(
            switched.Pole.Id,
            new DocumentPoint(40, 50));
        drawingLayout.Add(switchedPoleLayout);
        drawingLayout.Add(new PoleLayout(target.Pole.Id, new DocumentPoint(240, 160)));
        var right = new AttachmentLayout(
            attachment.AttachmentId,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(
                SwitchKind.IsolationSwitch));
        drawingLayout.Add(right);
        drawingLayout.Add(new OverheadLineLayout(
            connection.Id,
            new DocumentPoint(0, 0),
            new DocumentPoint(1, 1)));
        var layout = new RuntimeLayoutDocument(
            drawingLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        var builder = new DrawingSceneBuilder();
        string before = GeometryKey(builder.Build(document, layout), connection.Id);

        new ChangeAttachmentLayoutCommand(
            drawingLayout,
            right,
            right.RotateBy(1)).Execute();
        string after = GeometryKey(builder.Build(document, layout), connection.Id);

        Assert.NotEmpty(before);
        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);
        Assert.Same(connection, Assert.Single(document.Connections));
        Assert.Same(overheadLine, Assert.Single(document.OverheadLines));
        Assert.Equal(switchDevice.TerminalIds[1], connection.StartTerminalId);
        Assert.Equal(targetTerminalId, connection.EndTerminalId);
    }

    [Fact]
    public void DropoutFuse_UsesNoContactCircleAndKeepsVerticalTerminalAxis()
    {
        SceneElement[] geometry = RenderSwitch(SwitchKind.DropoutFuse);
        Assert.DoesNotContain(geometry.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds.WidthMillimeters <
            DrawingMetrics.Default.Pole.PoleRadius * 2);
        ScenePolyline tube = Assert.Single(geometry.OfType<ScenePolyline>(), polyline =>
            polyline.IsClosed && polyline.Points.Count == 4);
        double centerX = (tube.Points[2].XMillimeters +
                          tube.Points[3].XMillimeters) / 2;

        Assert.Contains(geometry.OfType<SceneLine>(), line =>
            Math.Abs(line.Start.XMillimeters - centerX) < 0.001 &&
            Math.Abs(line.End.XMillimeters - centerX) < 0.001 &&
            line.End.YMillimeters > line.Start.YMillimeters);
    }

    [Theory]
    [InlineData(SymbolVisualState.Open, true)]
    [InlineData(SymbolVisualState.Closed, false)]
    public void DropoutFuse_UsesStateSpecificTubeAndOperationArrow(
        SymbolVisualState state,
        bool expectsSlantedTube)
    {
        SceneElement[] geometry = new SwitchSymbolDefinition(SymbolKind.DropoutFuse)
            .Create(new SymbolRenderContext(
                new DocumentPoint(0, 0),
                DrawingMetrics.Default.PoleAttachment.SymbolWidth,
                DrawingMetrics.Default.PoleAttachment.SymbolHeight,
                state: state,
                includeLabel: false))
            .Where(element => element is not SceneLogicalBounds)
            .ToArray();
        ScenePolyline tube = Assert.Single(geometry.OfType<ScenePolyline>(), polyline =>
            polyline.IsClosed && polyline.Points.Count == 4);
        DocumentPoint topCenter = new(
            (tube.Points[0].XMillimeters + tube.Points[1].XMillimeters) / 2,
            (tube.Points[0].YMillimeters + tube.Points[1].YMillimeters) / 2);
        DocumentPoint bottomCenter = new(
            (tube.Points[2].XMillimeters + tube.Points[3].XMillimeters) / 2,
            (tube.Points[2].YMillimeters + tube.Points[3].YMillimeters) / 2);

        Assert.Contains(geometry.OfType<SceneLine>(), line =>
            line.Start == topCenter && line.End == bottomCenter);
        Assert.Equal(
            expectsSlantedTube,
            Math.Abs(topCenter.XMillimeters - bottomCenter.XMillimeters) > 0.001);
        Assert.Contains(geometry.OfType<ScenePolyline>(), polyline =>
            polyline.IsClosed && polyline.Points.Count == 3);
        Assert.Contains(geometry.OfType<SceneLine>(), line =>
            line.End.XMillimeters < line.Start.XMillimeters);
    }

    [Theory]
    [InlineData(SwitchKind.IsolationSwitch, false)]
    [InlineData(SwitchKind.CircuitBreaker, false)]
    [InlineData(SwitchKind.DropoutFuse, true)]
    public void PoleSwitch_DefaultPlacementMatchesReferenceOrientation(
        SwitchKind kind,
        bool abovePole)
    {
        DocumentPoint offset = PoleProfessionalGeometry.GetDefaultAttachmentOffset(kind);

        Assert.Equal(abovePole, offset.YMillimeters < 0);
        Assert.Equal(abovePole, offset.XMillimeters < 0);
    }

    [Fact]
    public void SwitchCommandUndoRedoRestoresPoleGeometryAndStableId()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [SwitchKind.CircuitBreaker],
            includeCableTerminal: false);
        var document = CreateDocument(result);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        PoleLayout poleLayout = new(result.Pole.Id, new DocumentPoint(0, 0));
        AttachmentLayout attachmentLayout = new(attachment.AttachmentId, new DocumentPoint(18, 2));
        var input = new SwitchAttachmentRenderInput(attachment, device, attachmentLayout);
        var renderer = new SwitchAttachmentRenderer();
        SceneElement[] original = Geometry(renderer.Render(result.Pole, poleLayout, [input]));
        var command = new ChangeSwitchStateCommand(document, device.Id, SwitchState.Closed);

        command.Execute();
        SceneElement[] changed = Geometry(renderer.Render(result.Pole, poleLayout, [input]));
        command.Undo();
        SceneElement[] undone = Geometry(renderer.Render(result.Pole, poleLayout, [input]));
        command.Redo();
        SceneElement[] redone = Geometry(renderer.Render(result.Pole, poleLayout, [input]));

        Assert.NotEqual(original, changed);
        Assert.Equal(original, undone);
        Assert.Equal(changed, redone);
        Assert.Equal(device.Id, input.SwitchDevice.Id);
    }

    [Fact]
    public void CableTermination_UsesTriangleAndComposesWithPoleCircle()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "45300005",
            PoleType.Cement,
            null,
            null,
            includeCableTerminal: true);
        CableTermination termination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment attachment = Assert.Single(result.Attachments);

        IReadOnlyList<SceneElement> elements = new PoleRenderer().Render(
            result.Pole,
            new PoleLayout(result.Pole.Id, new DocumentPoint(20, 30)),
            [new PoleAttachmentRenderInput(
                attachment,
                termination,
                new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(2, -10)))]);

        Assert.Single(elements.OfType<SceneEllipse>());
        ScenePolyline triangle = Assert.Single(elements.OfType<ScenePolyline>(), item => item.IsClosed);
        Assert.Equal(3, triangle.Points.Count);
        DocumentPoint baseCenter = new(
            (triangle.Points[1].XMillimeters + triangle.Points[2].XMillimeters) / 2,
            (triangle.Points[1].YMillimeters + triangle.Points[2].YMillimeters) / 2);
        Assert.Equal(
            DrawingMetrics.Default.CableTermination.TriangleWidth,
            EuclideanDistance(triangle.Points[1], triangle.Points[2]),
            6);
        Assert.Equal(
            DrawingMetrics.Default.CableTermination.TriangleHeight,
            EuclideanDistance(triangle.Points[0], baseCenter),
            6);
        Assert.Empty(elements.OfType<SceneLine>());
        Assert.Empty(elements.OfType<SceneRectangle>());
    }

    private static double EuclideanDistance(DocumentPoint first, DocumentPoint second)
    {
        double x = second.XMillimeters - first.XMillimeters;
        double y = second.YMillimeters - first.YMillimeters;
        return Math.Sqrt(x * x + y * y);
    }

    [Fact]
    public void TerminalAnchorsFollowProfessionalGeometryAndMoveWithLayout()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "45300006",
            PoleType.Cement,
            null,
            [SwitchKind.CircuitBreaker],
            includeCableTerminal: true);
        Terminal poleTerminal = result.Pole.CreateOverheadAnchorTerminal(Guid.NewGuid());
        var document = CreateDocument(result);
        document.AddTerminal(poleTerminal);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        CableTermination termination = Assert.Single(result.Devices.OfType<CableTermination>());
        PoleAttachment switchAttachment = Assert.Single(result.Attachments, item => item.AttachedDeviceId == switchDevice.Id);
        PoleAttachment terminationAttachment = Assert.Single(result.Attachments, item => item.AttachedDeviceId == termination.Id);
        var firstLayout = new DrawingLayout();
        firstLayout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(20, 30)));
        firstLayout.Add(new AttachmentLayout(switchAttachment.AttachmentId, new DocumentPoint(18, 2)));
        firstLayout.Add(new AttachmentLayout(terminationAttachment.AttachmentId, new DocumentPoint(2, -10)));

        TerminalAnchorIndex first = TerminalAnchorIndex.Build(
            document,
            firstLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        Assert.True(first.TryGet(poleTerminal.Id, out TerminalAnchor poleAnchor));
        Assert.Equal(
            new DocumentPoint(
                20 + DrawingMetrics.Default.Pole.PoleRadius,
                30 + DrawingMetrics.Default.Pole.PoleRadius),
            poleAnchor.Position);
        Assert.Equal(TerminalAnchorDirection.Auto, poleAnchor.Direction);
        Assert.True(first.TryGet(switchDevice.TerminalIds[0], out TerminalAnchor firstSwitch));
        Assert.True(first.TryGet(switchDevice.TerminalIds[1], out TerminalAnchor secondSwitch));
        Assert.NotEqual(firstSwitch.Position, secondSwitch.Position);
        Assert.Equal(TerminalAnchorDirection.Left, firstSwitch.Direction);
        Assert.Equal(TerminalAnchorDirection.Right, secondSwitch.Direction);
        Assert.True(first.TryGet(termination.CableSideTerminalId, out TerminalAnchor cableSide));
        Assert.True(first.TryGet(termination.OverheadSideTerminalId, out TerminalAnchor overheadSide));
        Assert.NotEqual(cableSide.Position, overheadSide.Position);
        Assert.Equal(TerminalAnchorDirection.Up, cableSide.Direction);
        Assert.Equal(TerminalAnchorDirection.Down, overheadSide.Direction);

        var movedLayout = new DrawingLayout();
        movedLayout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(60, 70)));
        movedLayout.Add(new AttachmentLayout(switchAttachment.AttachmentId, new DocumentPoint(18, 2)));
        movedLayout.Add(new AttachmentLayout(terminationAttachment.AttachmentId, new DocumentPoint(2, -10)));
        TerminalAnchorIndex moved = TerminalAnchorIndex.Build(
            document,
            movedLayout,
            new Dictionary<Guid, RingCabinetLayout>());
        Assert.True(moved.TryGet(termination.CableSideTerminalId, out TerminalAnchor movedCableSide));
        Assert.NotEqual(cableSide.Position, movedCableSide.Position);
    }

    [Theory]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.DropoutFuse)]
    public void EveryPoleSwitchMapsTwoTerminalsToDistinctVisualEndpoints(SwitchKind kind)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [kind],
            includeCableTerminal: false);
        var document = CreateDocument(result);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(10, 10)));
        layout.Add(new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(18, 2)));

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            layout,
            new Dictionary<Guid, RingCabinetLayout>());

        Assert.True(anchors.TryGet(device.TerminalIds[0], out TerminalAnchor first));
        Assert.True(anchors.TryGet(device.TerminalIds[1], out TerminalAnchor second));
        Assert.NotEqual(first.Position, second.Position);
        if (kind == SwitchKind.DropoutFuse)
        {
            Assert.Equal(TerminalAnchorDirection.Up, first.Direction);
            Assert.Equal(TerminalAnchorDirection.Down, second.Direction);
            Assert.Equal(first.Position.XMillimeters, second.Position.XMillimeters);
            Assert.NotEqual(first.Position.YMillimeters, second.Position.YMillimeters);
        }
        else
        {
            Assert.Equal(TerminalAnchorDirection.Left, first.Direction);
            Assert.Equal(TerminalAnchorDirection.Right, second.Direction);
            Assert.NotEqual(first.Position.XMillimeters, second.Position.XMillimeters);
            Assert.Equal(first.Position.YMillimeters, second.Position.YMillimeters);
        }
    }

    private static SceneElement[] RenderSwitch(SwitchKind kind)
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            [kind],
            includeCableTerminal: false);
        SwitchDevice device = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        return new SwitchAttachmentRenderer().Render(
                result.Pole,
                new PoleLayout(result.Pole.Id, new DocumentPoint(0, 0)),
                [new SwitchAttachmentRenderInput(
                    attachment,
                    device,
                    new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(18, 2)))])
            .Where(element => element is not SceneText and not SceneLogicalBounds)
            .ToArray();
    }

    private static SceneElement[] Geometry(IEnumerable<SceneElement> elements) =>
        elements.Where(element => element is not SceneText).ToArray();

    private static string GeometryKey(DrawingScene scene, Guid targetId) =>
        string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetId == targetId)
            .Select(line =>
                $"{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));

    private static SceneElement RotateElement(
        SceneElement element,
        PoleLayout poleLayout,
        int quarterTurns) => element switch
        {
            SceneLine line => line with
            {
                Start = PoleProfessionalGeometry.RotateAroundPole(
                    poleLayout, line.Start, quarterTurns),
                End = PoleProfessionalGeometry.RotateAroundPole(
                    poleLayout, line.End, quarterTurns)
            },
            SceneRectangle rectangle => rectangle with
            {
                Bounds = PoleProfessionalGeometry.RotateBoundsAroundPole(
                    poleLayout, rectangle.Bounds, quarterTurns)
            },
            SceneEllipse ellipse => new SceneEllipse(
                PoleProfessionalGeometry.RotateBoundsAroundPole(
                    poleLayout, ellipse.Bounds, quarterTurns),
                ellipse.Stroke,
                ellipse.ThicknessMillimeters,
                ellipse.Fill,
                ellipse.StrokeStyle),
            ScenePolyline polyline => new ScenePolyline(
                polyline.Points.Select(point => PoleProfessionalGeometry.RotateAroundPole(
                    poleLayout, point, quarterTurns)),
                polyline.IsClosed,
                polyline.Stroke,
                polyline.ThicknessMillimeters,
                polyline.Fill,
                polyline.StrokeStyle),
            SceneLogicalBounds bounds => new SceneLogicalBounds(
                PoleProfessionalGeometry.RotateBoundsAroundPole(
                    poleLayout, bounds.Bounds, quarterTurns)),
            _ => element
        };

    private static DrawingDocument CreateDocument(PoleCreationResult result)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Professional pole symbols");
        AddPoleAggregate(document, result);
        return document;
    }

    private static void AddPoleAggregate(
        DrawingDocument document,
        PoleCreationResult result)
    {
        document.AddDevice(result.Pole);
        foreach (Device device in result.Devices)
        {
            document.AddDevice(device);
        }

        foreach (ElectricalNode node in result.ElectricalNodes)
        {
            document.AddElectricalNode(node);
        }

        foreach (Terminal terminal in result.Terminals)
        {
            document.AddTerminal(terminal);
        }

        foreach (PoleAttachment attachment in result.Attachments)
        {
            document.AddPoleAttachment(attachment);
        }
    }
}
