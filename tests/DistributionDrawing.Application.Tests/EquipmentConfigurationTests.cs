using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class EquipmentConfigurationTests
{
    [Fact]
    public void LoadSwitchConfiguration_IsAControlledEquipmentConfiguration()
    {
        BayEquipmentConfiguration configuration = new LoadSwitchConfiguration();

        Assert.IsType<LoadSwitchConfiguration>(configuration);
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding)]
    [InlineData(GroundingStructureKind.UpperLowerGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void IntegratedFeederConfiguration_PreservesGroundingStructure(
        GroundingStructureKind groundingStructureKind)
    {
        var configuration = new IntegratedFeederConfiguration(groundingStructureKind);

        Assert.Equal(groundingStructureKind, configuration.GroundingStructureKind);
    }

    [Fact]
    public void IntegratedFeederConfiguration_RejectsUndefinedGroundingStructure()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntegratedFeederConfiguration((GroundingStructureKind)int.MaxValue));
    }
}
