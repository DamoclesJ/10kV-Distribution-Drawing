using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class PoleTests
{
    [Fact]
    public void Pole_can_be_created_and_added_as_a_device()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-01", "一号杆");

        document.AddDevice(pole);

        Assert.Contains(pole, document.Devices);
        Assert.Equal(DeviceType.Pole, pole.Type);
        Assert.Equal("P-01", pole.PoleNumber);
        Assert.Equal(PoleType.Cement, pole.PoleType);
        Assert.Equal("一号杆", pole.DisplayName);
    }

    [Fact]
    public void Pole_has_no_switch_state_and_does_not_create_an_electrical_node()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-02");

        document.AddDevice(pole);

        Assert.Null(pole.SwitchState);
        Assert.Empty(document.ElectricalNodes);
        Assert.DoesNotContain(
            document.ElectricalNodes,
            node => node.OwnerType == TopologyOwnerType.Device &&
                    node.OwnerId == pole.Id);
    }

    [Fact]
    public void Pole_anchor_terminal_is_optional_and_is_owned_by_the_pole()
    {
        var document = TestFixtures.CreateDocument();
        var pole = new Pole(Guid.NewGuid(), "P-03");
        var anchor = TestFixtures.CreatePoleAnchorTerminal(pole);

        document.AddDevice(pole);
        document.AddTerminal(anchor);

        Assert.True(pole.OwnsTerminal(anchor.Id));
        Assert.Equal(pole.Id, anchor.OwnerId);
        Assert.Single(anchor.AllowedConnectionTypes);
        Assert.Contains(ConnectionType.OverheadLine, anchor.AllowedConnectionTypes);
    }
}
