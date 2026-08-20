using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class PoleProfessionalSymbolTests
{
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
        Assert.Contains(open.OfType<SceneText>(), text => text.Text == "分");
        Assert.Contains(closed.OfType<SceneText>(), text => text.Text == "合");
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
        Assert.DoesNotContain(geometries[2], element => element is SceneRectangle);
        Assert.Contains(geometries[3], element => element is ScenePolyline polyline && polyline.IsClosed);
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
        Assert.Empty(elements.OfType<SceneRectangle>());
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
        Assert.Equal(new DocumentPoint(27, 37), poleAnchor.Position);
        Assert.True(first.TryGet(switchDevice.TerminalIds[0], out TerminalAnchor firstSwitch));
        Assert.True(first.TryGet(switchDevice.TerminalIds[1], out TerminalAnchor secondSwitch));
        Assert.NotEqual(firstSwitch.Position, secondSwitch.Position);
        Assert.True(first.TryGet(termination.CableSideTerminalId, out TerminalAnchor cableSide));
        Assert.True(first.TryGet(termination.OverheadSideTerminalId, out TerminalAnchor overheadSide));
        Assert.NotEqual(cableSide.Position, overheadSide.Position);

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
            Assert.Equal(first.Position.XMillimeters, second.Position.XMillimeters);
            Assert.NotEqual(first.Position.YMillimeters, second.Position.YMillimeters);
        }
        else
        {
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

    private static DrawingDocument CreateDocument(PoleCreationResult result)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Professional pole symbols");
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

        return document;
    }
}
