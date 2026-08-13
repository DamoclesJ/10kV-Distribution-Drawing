using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class PoleAttachmentTests
{
    [Fact]
    public void One_pole_can_have_multiple_cable_termination_attachments()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-10");
        var firstTermination = TestFixtures.CreateCableTermination();
        var secondTermination = TestFixtures.CreateCableTermination();

        document.AddDevice(pole);
        TestFixtures.AddCableTerminationTopology(document, firstTermination);
        TestFixtures.AddCableTerminationTopology(document, secondTermination);

        document.AddPoleAttachment(
            new PoleAttachment(Guid.NewGuid(), pole.Id, firstTermination.Id));
        document.AddPoleAttachment(
            new PoleAttachment(Guid.NewGuid(), pole.Id, secondTermination.Id));

        Assert.Equal(2, document.PoleAttachments.Count);
        Assert.All(document.PoleAttachments, attachment => Assert.Equal(pole.Id, attachment.PoleId));
    }

    [Fact]
    public void Pole_switch_can_be_installed_through_pole_attachment()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-11");
        var switchDevice = TestFixtures.CreatePoleSwitch();

        document.AddDevice(pole);
        document.AddDevice(switchDevice);
        var attachment = new PoleAttachment(Guid.NewGuid(), pole.Id, switchDevice.Id);

        document.AddPoleAttachment(attachment);

        Assert.Contains(attachment, document.PoleAttachments);
        Assert.Equal(switchDevice.Id, attachment.AttachedDeviceId);
    }

    [Fact]
    public void Invalid_pole_or_device_references_are_rejected()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-12");
        var termination = TestFixtures.CreateCableTermination();

        document.AddDevice(pole);
        TestFixtures.AddCableTerminationTopology(document, termination);

        Assert.Throws<InvalidOperationException>(() =>
            document.AddPoleAttachment(
                new PoleAttachment(Guid.NewGuid(), Guid.NewGuid(), termination.Id)));

        Assert.Throws<InvalidOperationException>(() =>
            document.AddPoleAttachment(
                new PoleAttachment(Guid.NewGuid(), pole.Id, Guid.NewGuid())));
    }

    [Fact]
    public void Cabinet_switch_cannot_be_attached_as_a_pole_switch()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-13");
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([2, 4, 6]);

        document.AddDevice(pole);
        document.AddDevice(cabinet);
        var cabinetSwitch = cabinet.Intervals[0].SwitchDevices[0];

        Assert.Throws<InvalidOperationException>(() =>
            document.AddPoleAttachment(
                new PoleAttachment(Guid.NewGuid(), pole.Id, cabinetSwitch.Id)));
    }
}
