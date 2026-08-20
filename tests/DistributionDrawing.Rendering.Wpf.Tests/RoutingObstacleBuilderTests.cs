using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RoutingObstacleBuilderTests
{
    [Fact]
    public void Build_UsesProfessionalLogicalBoundsForSupportedDeviceKinds()
    {
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "柜",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Open, SwitchState.Open)
            ]));
        PoleCreationResult pole = new PoleCreationFactory().CreateWithAttachments(
            "P",
            PoleType.Cement,
            null,
            switchKinds: null,
            includeCableTerminal: true);
        var layout = new DrawingLayout();
        layout.Add(new PoleLayout(pole.Pole.Id, new DocumentPoint(100, 30)));
        foreach (PoleAttachment attachment in pole.Attachments)
        {
            layout.Add(new AttachmentLayout(attachment.AttachmentId, new DocumentPoint(15, 0)));
        }

        IReadOnlyList<RoutingObstacle> obstacles = new RoutingObstacleBuilder().Build(
            new Device[] { cabinet, pole.Pole }.Concat(pole.Devices),
            pole.Attachments,
            layout,
            new Dictionary<Guid, RingCabinetLayout>
            {
                [cabinet.Id] = new RingCabinetLayoutFactory().Create(
                    cabinet,
                    new DocumentPoint(10, 10))
            },
            [new JointLayout(Guid.NewGuid(), new DocumentPoint(70, 70))]);

        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.RingCabinet);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.Pole);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.PoleAttachment);
        Assert.Contains(obstacles, obstacle => obstacle.Kind == RoutingObstacleKind.IntermediateTerminal);
        Assert.All(obstacles, obstacle =>
        {
            Assert.True(obstacle.Bounds.WidthMillimeters > 0);
            Assert.True(obstacle.Bounds.HeightMillimeters > 0);
        });
    }
}
