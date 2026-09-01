using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class RingCabinetCreationTemplateFactoryTests
{
    private readonly RingCabinetCreationTemplateFactory _factory = new();

    [Theory]
    [InlineData(RingCabinetTemplateType.Conventional, 4)]
    [InlineData(RingCabinetTemplateType.Conventional, 5)]
    [InlineData(RingCabinetTemplateType.Conventional, 6)]
    [InlineData(RingCabinetTemplateType.Conventional, 7)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 4)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 5)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 6)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 7)]
    public void Create_GeneratesSupportedBusinessIntervalsWithStableNames(
        RingCabinetTemplateType type,
        int count)
    {
        RingCabinetTemplate template = _factory.Create(type, count);

        Assert.Equal(count, template.Bays.Count);
        Assert.Equal(Enumerable.Range(1, count), template.Bays.Select(bay => bay.Index));
        Assert.Equal(
            Enumerable.Range(1, count).Select(index => $"负{index}"),
            template.Bays.Select(bay => bay.DisplayName));
        if (type == RingCabinetTemplateType.Conventional)
        {
            Assert.All(template.Bays, bay =>
                Assert.IsType<LoadSwitchConfiguration>(bay.EquipmentConfiguration));
        }
        else
        {
            Assert.All(template.Bays, bay =>
                Assert.IsType<IntegratedFeederConfiguration>(bay.EquipmentConfiguration));
        }
    }

    [Fact]
    public void Create_WithPT_ReplacesTheRightmostBayByDefaultWithoutChangingCount()
    {
        RingCabinetTemplate template = _factory.Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            4,
            includePTInterval: true);

        BayTemplate pt = Assert.Single(template.Bays, bay =>
            bay.EquipmentConfiguration is PTConfiguration);
        Assert.Equal(4, template.Bays.Count);
        Assert.Equal(4, pt.Index);
        Assert.Equal("PT", pt.DisplayName);
        Assert.Contains(TemplateCapability.PTInterval, template.RequiredCapabilities);
        Assert.DoesNotContain(TemplateCapability.DtuSecondary, template.RequiredCapabilities);
    }

    [Theory]
    [InlineData(RingCabinetPTPlacement.Left, 1)]
    [InlineData(RingCabinetPTPlacement.Right, 5)]
    public void Create_WithPTPlacesItAtTheRequestedEnd(
        RingCabinetPTPlacement placement,
        int expectedIndex)
    {
        RingCabinetTemplate template = _factory.Create(
            RingCabinetTemplateType.Conventional,
            5,
            includePTInterval: true,
            ptPlacement: placement);

        BayTemplate pt = Assert.Single(template.Bays, bay =>
            bay.EquipmentConfiguration is PTConfiguration);
        Assert.Equal(5, template.Bays.Count);
        Assert.Equal(expectedIndex, pt.Index);
        Assert.All(template.Bays.Where(bay => bay.Index != expectedIndex), bay =>
            Assert.IsType<LoadSwitchConfiguration>(bay.EquipmentConfiguration));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public void Create_RejectsCountsOutsideTheSupportedProductRange(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(
            RingCabinetTemplateType.Conventional,
            count));
    }
}
