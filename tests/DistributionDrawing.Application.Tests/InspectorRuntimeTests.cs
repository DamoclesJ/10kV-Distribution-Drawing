using DistributionDrawing.Application.Interaction;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class InspectorRuntimeTests
{
    [Fact]
    public void ResolveCableSegment_ShowsCableTypeAndLength()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        IntermediateTerminalCreationResult first =
            new IntermediateTerminalCreationFactory().Create("A");
        IntermediateTerminalCreationResult second =
            new IntermediateTerminalCreationFactory().Create("B");
        document.AddIntermediateTerminal(first.IntermediateTerminal, first.Terminal);
        document.AddIntermediateTerminal(second.IntermediateTerminal, second.Terminal);
        CableSegmentCreationResult result = new CableSegmentCreationFactory().Create(
            document,
            first.Terminal.Id,
            second.Terminal.Id,
            "Cable-1",
            "XLPE",
            12.5);
        document.AddCableSegment(result.CableSegment, result.Connection);

        InspectorModel? model = Resolve(document, SelectionTargetKind.CableSegment, result.CableSegment.Id);

        Assert.NotNull(model);
        Assert.Contains(model.Properties, property => property is { Key: "CableType", Value: "XLPE" });
        Assert.Contains(model.Properties, property => property is { Key: "Length", Value: "12.5" });
    }

    [Fact]
    public void ResolveSwitch_ShowsKindAndState()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.CircuitBreaker,
            Guid.NewGuid(),
            Guid.NewGuid(),
            SwitchState.Closed);
        document.AddDevice(switchDevice);

        InspectorModel? model = Resolve(document, SelectionTargetKind.SwitchDevice, switchDevice.Id);

        Assert.NotNull(model);
        Assert.Contains(model.Properties, property => property.Value == SwitchKind.CircuitBreaker.ToString());
        Assert.Contains(model.Properties, property => property.Value == SwitchState.Closed.ToString());
    }

    [Fact]
    public void ResolvePole_ShowsAttachmentCount()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");
        Pole pole = new(Guid.NewGuid(), "P-1");
        SwitchDevice switchDevice = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.LoadSwitch,
            Guid.NewGuid(),
            Guid.NewGuid());
        document.AddDevice(pole);
        document.AddDevice(switchDevice);
        document.AddPoleAttachment(new PoleAttachment(Guid.NewGuid(), pole.Id, switchDevice.Id));

        InspectorModel? model = Resolve(document, SelectionTargetKind.Pole, pole.Id);

        Assert.NotNull(model);
        Assert.Contains(model.Properties, property => property is { Key: "AttachmentCount", Value: "1" });
    }

    [Fact]
    public void ResolveUnknownTarget_ReturnsNull()
    {
        DrawingDocument document = new(Guid.NewGuid(), "Test");

        Assert.Null(Resolve(document, SelectionTargetKind.Pole, Guid.NewGuid()));
    }

    private static InspectorModel? Resolve(
        DrawingDocument document,
        SelectionTargetKind kind,
        Guid id)
    {
        return new InspectorResolver(document).Resolve(new SelectionTarget(kind, id));
    }
}
