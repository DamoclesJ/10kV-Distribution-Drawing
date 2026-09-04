using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class GroundingPresentationAnchorResolverTests
{
    [Fact]
    public void OrdinaryTerminal_FallsBackToElectricalAnchorWithoutChangingIdentity()
    {
        PoleCreationResult result = new PoleCreationFactory().Create("P-1");
        DrawingDocument document = CreateDocument(result);
        Guid terminalId = Assert.Single(result.Pole.OverheadAnchorTerminalIds);
        var layout = new DrawingLayout();
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(10, 20));
        layout.Add(poleLayout);
        GroundingPoint groundingPoint = document.CreateGroundingPoint(
            Guid.NewGuid(), terminalId, "普通端子", "G01");
        TerminalAnchorIndex terminals = TerminalAnchorIndex.Build(
            document, layout, new Dictionary<Guid, RingCabinetLayout>());

        bool resolved = new GroundingPresentationAnchorResolver().TryResolve(
            groundingPoint,
            document,
            layout,
            terminals,
            out GroundingPresentationAnchor anchor);

        Assert.True(resolved);
        Assert.Equal(PoleProfessionalGeometry.GetPoleCenter(poleLayout), anchor.Position);
        Assert.Equal(TerminalAnchorDirection.Right, anchor.Direction);
        Assert.Equal(terminalId, groundingPoint.TerminalId);
    }

    [Fact]
    public void PoleSwitchTerminals_ResolveToOppositeCompositeOuterEdges()
    {
        SwitchScenario scenario = CreateSwitchScenario();
        GroundingPoint leftPoint = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[0], "左侧", "G01");
        GroundingPoint rightPoint = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[1], "右侧", "G02");
        TerminalAnchorIndex terminals = BuildTerminals(scenario);
        var resolver = new GroundingPresentationAnchorResolver();

        Assert.True(resolver.TryResolve(
            leftPoint, scenario.Document, scenario.Layout, terminals,
            out GroundingPresentationAnchor left));
        Assert.True(resolver.TryResolve(
            rightPoint, scenario.Document, scenario.Layout, terminals,
            out GroundingPresentationAnchor right));

        DocumentRect composite = CompositeBounds(scenario);
        Assert.Equal(composite.XMillimeters, left.Position.XMillimeters);
        Assert.Equal(
            composite.XMillimeters + composite.WidthMillimeters,
            right.Position.XMillimeters);
        Assert.Equal(TerminalAnchorDirection.Left, left.Direction);
        Assert.Equal(TerminalAnchorDirection.Right, right.Direction);
        PoleAttachmentGeometry switchGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            scenario.PoleLayout,
            scenario.AttachmentLayout,
            SymbolLibrary.ResolveAttachmentKind(scenario.Switch));
        Assert.True(terminals.TryGet(
            scenario.Switch.TerminalIds[0],
            out TerminalAnchor electricalLeft));
        Assert.Equal(switchGeometry.FirstTerminal, electricalLeft.Position);
        Assert.NotEqual(electricalLeft.Position, left.Position);
        Assert.Equal(scenario.Switch.TerminalIds[0], leftPoint.TerminalId);
        Assert.Equal(scenario.Switch.TerminalIds[1], rightPoint.TerminalId);
    }

    [Fact]
    public void AttachmentOffsetRotationAndUndoRedo_RebuildDerivedAnchor()
    {
        SwitchScenario scenario = CreateSwitchScenario();
        GroundingPoint point = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[0], "目标端子");
        GroundingPresentationAnchor before = Resolve(scenario, point);
        AttachmentLayout original = scenario.AttachmentLayout;
        AttachmentLayout changed = original
            .MoveTo(new DocumentPoint(35, -20))
            .RotateBy(1);
        var command = new ChangeAttachmentLayoutCommand(
            scenario.Layout,
            original,
            changed);

        command.Execute();
        GroundingPresentationAnchor after = Resolve(scenario, point);
        command.Undo();
        GroundingPresentationAnchor undone = Resolve(scenario, point);
        command.Redo();
        GroundingPresentationAnchor redone = Resolve(scenario, point);

        Assert.NotEqual(before, after);
        Assert.Equal(before, undone);
        Assert.Equal(after, redone);
        Assert.Equal(TerminalAnchorDirection.Up, after.Direction);
        Assert.Equal(scenario.Switch.TerminalIds[0], point.TerminalId);
    }

    [Fact]
    public void PoleMoveAndUndo_RebuildDerivedAnchorWithoutChangingTerminal()
    {
        SwitchScenario scenario = CreateSwitchScenario();
        GroundingPoint point = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[1], "目标端子");
        GroundingPresentationAnchor before = Resolve(scenario, point);
        PoleLayout moved = scenario.PoleLayout.MoveTo(new DocumentPoint(90, 120));
        var command = new MoveCommand(scenario.Layout, scenario.PoleLayout, moved);

        command.Execute();
        GroundingPresentationAnchor after = Resolve(scenario, point);
        command.Undo();
        GroundingPresentationAnchor undone = Resolve(scenario, point);

        Assert.NotEqual(before.Position, after.Position);
        Assert.Equal(before, undone);
        Assert.Equal(scenario.Switch.TerminalIds[1], point.TerminalId);
    }

    [Fact]
    public void SceneUsesPresentationGeometryForLinesAndHitTesting()
    {
        SwitchScenario scenario = CreateSwitchScenario();
        GroundingPoint leftPoint = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[0], "左侧", "G01");
        GroundingPoint rightPoint = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), scenario.Switch.TerminalIds[1], "右侧", "G02");

        DrawingScene scene = new DrawingSceneBuilder().Build(
            scenario.Document,
            new RuntimeLayoutDocument(
                scenario.Layout,
                new Dictionary<Guid, RingCabinetLayout>()));
        SelectionHitTestEntry leftHit = Assert.Single(scene.HitTestIndex.Entries,
            entry => entry.Target.Kind == SelectionTargetKind.GroundingPoint &&
                     entry.Target.ObjectId == leftPoint.GroundingPointId);
        SelectionHitTestEntry rightHit = Assert.Single(scene.HitTestIndex.Entries,
            entry => entry.Target.Kind == SelectionTargetKind.GroundingPoint &&
                     entry.Target.ObjectId == rightPoint.GroundingPointId);
        DocumentPoint leftAnchor = Center(leftHit.Bounds);
        DocumentPoint rightAnchor = Center(rightHit.Bounds);

        Assert.Contains(scene.Elements.OfType<SceneLine>(), line =>
            line.Start == leftAnchor && line.End.XMillimeters < line.Start.XMillimeters);
        Assert.Contains(scene.Elements.OfType<SceneLine>(), line =>
            line.Start == rightAnchor && line.End.XMillimeters > line.Start.XMillimeters);
        Assert.Empty(scene.Diagnostics);
    }

    [Fact]
    public void MissingAnchor_ProducesExplicitSceneDiagnostic()
    {
        PoleCreationResult result = new PoleCreationFactory().Create("P-missing");
        DrawingDocument document = CreateDocument(result);
        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(result.Pole.Id, new DocumentPoint(20, 30)));
        Guid terminalId = Guid.NewGuid();
        document.AddTerminal(new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            result.Pole.Id,
            "unindexed grounding terminal",
            "10kV",
            isExternal: true,
            allowsMultipleConnections: false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]));
        GroundingPoint groundingPoint = document.CreateGroundingPoint(
            Guid.NewGuid(), terminalId, "缺失布局");

        DrawingScene scene = new DrawingSceneBuilder().Build(
            document,
            new RuntimeLayoutDocument(
                layout,
                new Dictionary<Guid, RingCabinetLayout>()));

        SceneBuildDiagnostic diagnostic = Assert.Single(scene.Diagnostics);
        Assert.Equal("GroundingPresentationAnchorMissing", diagnostic.Code);
        Assert.Equal(groundingPoint.GroundingPointId, diagnostic.TargetId);
        Assert.DoesNotContain(scene.HitTestIndex.Entries,
            entry => entry.Target.ObjectId == groundingPoint.GroundingPointId);
    }

    private static GroundingPresentationAnchor Resolve(
        SwitchScenario scenario,
        GroundingPoint groundingPoint)
    {
        Assert.True(new GroundingPresentationAnchorResolver().TryResolve(
            groundingPoint,
            scenario.Document,
            scenario.Layout,
            BuildTerminals(scenario),
            out GroundingPresentationAnchor anchor));
        return anchor;
    }

    private static TerminalAnchorIndex BuildTerminals(SwitchScenario scenario) =>
        TerminalAnchorIndex.Build(
            scenario.Document,
            scenario.Layout,
            new Dictionary<Guid, RingCabinetLayout>());

    private static DocumentRect CompositeBounds(SwitchScenario scenario)
    {
        DocumentRect pole = PoleProfessionalGeometry.GetPoleBounds(scenario.PoleLayout);
        DocumentRect attachment = PoleProfessionalGeometry.GetAttachmentGeometry(
            scenario.PoleLayout,
            scenario.AttachmentLayout,
            SymbolLibrary.ResolveAttachmentKind(scenario.Switch)).LogicalBounds;
        double left = Math.Min(pole.XMillimeters, attachment.XMillimeters);
        double top = Math.Min(pole.YMillimeters, attachment.YMillimeters);
        double right = Math.Max(
            pole.XMillimeters + pole.WidthMillimeters,
            attachment.XMillimeters + attachment.WidthMillimeters);
        double bottom = Math.Max(
            pole.YMillimeters + pole.HeightMillimeters,
            attachment.YMillimeters + attachment.HeightMillimeters);
        return new DocumentRect(left, top, right - left, bottom - top);
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);

    private static SwitchScenario CreateSwitchScenario()
    {
        PoleCreationResult result = new PoleCreationFactory().CreateWithAttachments(
            "P-switch",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: false);
        DrawingDocument document = CreateDocument(result);
        SwitchDevice switchDevice = Assert.Single(result.Devices.OfType<SwitchDevice>());
        PoleAttachment attachment = Assert.Single(result.Attachments);
        var poleLayout = new PoleLayout(result.Pole.Id, new DocumentPoint(40, 50));
        var attachmentLayout = new AttachmentLayout(
            attachment.AttachmentId,
            PoleProfessionalGeometry.GetDefaultAttachmentOffset(switchDevice.SwitchKind));
        var layout = new DrawingLayout();
        layout.Add(poleLayout);
        layout.Add(attachmentLayout);
        return new SwitchScenario(
            document,
            switchDevice,
            attachment,
            layout,
            poleLayout,
            attachmentLayout);
    }

    private static DrawingDocument CreateDocument(PoleCreationResult result)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Grounding presentation tests");
        document.AddDevice(result.Pole);
        foreach (Device device in result.Devices) document.AddDevice(device);
        foreach (ElectricalNode node in result.ElectricalNodes) document.AddElectricalNode(node);
        foreach (Terminal terminal in result.Terminals) document.AddTerminal(terminal);
        foreach (PoleAttachment attachment in result.Attachments)
            document.AddPoleAttachment(attachment);
        return document;
    }

    private sealed record SwitchScenario(
        DrawingDocument Document,
        SwitchDevice Switch,
        PoleAttachment Attachment,
        DrawingLayout Layout,
        PoleLayout PoleLayout,
        AttachmentLayout AttachmentLayout);
}
