using DistributionDrawing.Application.Devices;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class TopologySceneIntegrationTests
{
    [Fact]
    public void BuildDocumentScene_RendersCabinetCableJointCableMixedPole()
    {
        TopologyFixture fixture = CreateFixture();

        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Contains(scene.Elements, element => element is SceneLine);
        Assert.Contains(scene.Elements, element => element is SceneRectangle);
        Assert.Contains(scene.Elements, element => element is SceneText text &&
            text.Text == fixture.Joint.DisplayName);
        Assert.Contains(scene.Elements, element => element is SceneLine line &&
            line.TargetKind == DistributionDrawing.Application.Interaction.SelectionTargetKind.CableSegment &&
            line.TargetId == fixture.FirstCable.Id);
        Assert.Contains(scene.Elements, element => element is SceneRectangle rectangle &&
            rectangle.TargetKind == DistributionDrawing.Application.Interaction.SelectionTargetKind.IntermediateTerminal &&
            rectangle.TargetId == fixture.Joint.Id);
    }

    [Fact]
    public void BuildDocumentScene_RegistersCableAndJointHitTestTargets()
    {
        TopologyFixture fixture = CreateFixture();

        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Layout);

        Assert.Contains(
            scene.HitTestIndex.Entries,
            entry => entry.Target.Kind == SelectionTargetKind.CableSegment &&
                     entry.Target.ObjectId == fixture.FirstCable.Id);
        Assert.Contains(
            scene.HitTestIndex.Entries,
            entry => entry.Target.Kind == SelectionTargetKind.IntermediateTerminal &&
                     entry.Target.ObjectId == fixture.Joint.Id);
    }

    [Fact]
    public void BuildDocumentScene_IsDeterministicAndDoesNotModifyDomain()
    {
        TopologyFixture fixture = CreateFixture();
        Guid[] connectionIds = fixture.Document.Connections.Select(connection => connection.Id).ToArray();
        Guid[] terminalIds = fixture.Document.Terminals.Select(terminal => terminal.Id).ToArray();

        DrawingScene first = fixture.Builder.Build(fixture.Document, fixture.Layout);
        DrawingScene second = fixture.Builder.Build(fixture.Document, fixture.Layout);

        SceneElementAssertions.Equal(first.Elements, second.Elements);
        Assert.Equal(connectionIds, fixture.Document.Connections.Select(connection => connection.Id));
        Assert.Equal(terminalIds, fixture.Document.Terminals.Select(terminal => terminal.Id));
        Assert.Equal(fixture.Joint.Id, fixture.Document.FindIntermediateTerminal(fixture.Joint.Id)?.Id);
    }

    [Fact]
    public void RingCabinetMove_ReroutesCableAndRestoringLayoutRestoresRoute()
    {
        TopologyFixture fixture = CreateFixture();
        RingCabinet cabinet = Assert.Single(fixture.Document.Devices.OfType<RingCabinet>());
        RingCabinetLayout before = fixture.Layout.RingCabinetLayouts[cabinet.Id];
        string initial = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));

        fixture.Layout.ReplaceRingCabinet(before.MoveTo(new DocumentPoint(70, 90)));
        string moved = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));
        fixture.Layout.ReplaceRingCabinet(before);
        string restored = CableGeometryKey(fixture.Builder.Build(fixture.Document, fixture.Layout));

        Assert.NotEqual(initial, moved);
        Assert.Equal(initial, restored);
    }

    [Fact]
    public void CompletedCableRoute_KeepsFiftyMillimeterDownwardCabinetExit()
    {
        TopologyFixture fixture = CreateFixture();
        RingCabinet cabinet = Assert.Single(fixture.Document.Devices.OfType<RingCabinet>());
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            fixture.Document,
            fixture.Layout.DrawingLayout,
            fixture.Layout.RingCabinetLayouts,
            fixture.Document.Connections,
            fixture.Document.CableSegments);
        Assert.True(anchors.TryGet(
            cabinet.Intervals[0].ExternalTerminalId,
            out TerminalAnchor terminal));

        SceneLine exit = Assert.Single(
            fixture.Builder.Build(fixture.Document, fixture.Layout).Elements.OfType<SceneLine>(),
            line => line.TargetId == fixture.FirstCable.Id &&
                    line.Start == terminal.Position);

        Assert.Equal(exit.Start.XMillimeters, exit.End.XMillimeters);
        Assert.True(exit.End.YMillimeters - exit.Start.YMillimeters >= 50);
    }

    private static TopologyFixture CreateFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Topology scene integration test");
        RingCabinetDefinition cabinetDefinition = RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Cabinet-A",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    1,
                    SwitchState.Open,
                    SwitchState.Open,
                    "负1"),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    2,
                    SwitchState.Open,
                    SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    3,
                    SwitchState.Open,
                    SwitchState.Open)]);
        RingCabinet cabinet = RingCabinet.Create(cabinetDefinition);
        document.AddDevice(cabinet);

        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P-601",
            PoleType.Cement,
            null,
            [SwitchKind.IsolationSwitch],
            includeCableTerminal: true);
        AddPoleAggregate(document, pole);

        IntermediateTerminalCreationResult joint =
            new IntermediateTerminalCreationFactory().Create("Joint-601");
        document.AddIntermediateTerminal(joint.IntermediateTerminal, joint.Terminal);

        CableTermination termination = Assert.Single(
            pole.Devices.OfType<CableTermination>());
        CableSegment firstCable = AddCable(
            document,
            cabinet.Intervals[0].ExternalTerminalId,
            joint.Terminal.Id,
            "Cable-601-A");
        _ = AddCable(
            document,
            joint.Terminal.Id,
            termination.CableSideTerminalId,
            "Cable-601-B");

        var drawingLayout = new DrawingLayout();
        drawingLayout.Add(new PoleLayout(pole.Pole.Id, new DocumentPoint(240, 20)));
        foreach (PoleAttachment attachment in pole.Attachments)
        {
            drawingLayout.Add(new AttachmentLayout(
                attachment.AttachmentId,
                new DocumentPoint(0, 0)));
        }

        return new TopologyFixture(
            document,
            joint.IntermediateTerminal,
            firstCable,
            new RuntimeLayoutDocument(
                drawingLayout,
                new Dictionary<Guid, RingCabinetLayout>
                {
                    [cabinet.Id] = new RingCabinetLayoutFactory().Create(
                        cabinet,
                        new DocumentPoint(20, 20))
                }),
            new DrawingSceneBuilder());
    }

    private static CableSegment AddCable(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId,
        string name)
    {
        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            name,
            "10kV");
        var cable = new CableSegment(
            Guid.NewGuid(),
            name,
            "YJV22-8.7/15kV",
            80,
            "10kV",
            connection.Id,
            startTerminalId,
            endTerminalId);
        document.AddCableSegment(cable, connection);
        return cable;
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

    private static string CableGeometryKey(DrawingScene scene)
    {
        return string.Join(';', scene.Elements.OfType<SceneLine>()
            .Where(line => line.TargetKind ==
                DistributionDrawing.Application.Interaction.SelectionTargetKind.CableSegment)
            .Select(line =>
                $"{line.TargetId}:{line.Start.XMillimeters:R},{line.Start.YMillimeters:R}-" +
                $"{line.End.XMillimeters:R},{line.End.YMillimeters:R}"));
    }

    private sealed record TopologyFixture(
        DrawingDocument Document,
        IntermediateTerminal Joint,
        CableSegment FirstCable,
        RuntimeLayoutDocument Layout,
        DrawingSceneBuilder Builder);
}
