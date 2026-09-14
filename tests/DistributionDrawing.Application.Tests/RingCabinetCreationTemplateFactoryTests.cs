using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class RingCabinetCreationTemplateFactoryTests
{
    private readonly RingCabinetCreationTemplateFactory _factory = new();

    [Theory]
    [InlineData(RingCabinetTemplateType.Conventional, 3)]
    [InlineData(RingCabinetTemplateType.Conventional, 4)]
    [InlineData(RingCabinetTemplateType.Conventional, 5)]
    [InlineData(RingCabinetTemplateType.Conventional, 6)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 4)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 6)]
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

    [Theory]
    [InlineData(4, RingCabinetPTPlacement.Left, 1, 5)]
    [InlineData(4, RingCabinetPTPlacement.Right, 5, 5)]
    [InlineData(6, RingCabinetPTPlacement.Left, 1, 7)]
    [InlineData(6, RingCabinetPTPlacement.Right, 7, 7)]
    public void Create_IntegratedWithPTAddsOneBayAtTheRequestedEnd(
        int businessIntervalCount,
        RingCabinetPTPlacement placement,
        int expectedPTIndex,
        int expectedTotalCount)
    {
        RingCabinetTemplate template = _factory.Create(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            businessIntervalCount,
            includePTInterval: true,
            ptPlacement: placement);

        BayTemplate pt = Assert.Single(template.Bays, bay =>
            bay.EquipmentConfiguration is PTConfiguration);
        Assert.Equal(expectedTotalCount, template.Bays.Count);
        Assert.Equal(Enumerable.Range(1, expectedTotalCount), template.Bays.Select(bay => bay.Index));
        Assert.Equal(expectedPTIndex, pt.Index);
        Assert.Equal("PT", pt.DisplayName);
        Assert.Equal(businessIntervalCount, template.Bays.Count(bay =>
            bay.EquipmentConfiguration is IntegratedFeederConfiguration));
        Assert.All(template.Bays.Where(bay => bay.Index != expectedPTIndex), bay =>
            Assert.IsType<IntegratedFeederConfiguration>(bay.EquipmentConfiguration));
        Assert.Contains(TemplateCapability.PTInterval, template.RequiredCapabilities);
        Assert.DoesNotContain(TemplateCapability.DtuSecondary, template.RequiredCapabilities);
    }

    [Theory]
    [InlineData(RingCabinetTemplateType.Conventional, 2)]
    [InlineData(RingCabinetTemplateType.Conventional, 7)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 3)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 5)]
    [InlineData(RingCabinetTemplateType.PrimarySecondaryIntegrated, 7)]
    public void Create_RejectsCountsOutsideTheSupportedProductRange(
        RingCabinetTemplateType type,
        int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create(
            type,
            count));
    }

    [Fact]
    public void Create_ConventionalRejectsPTInterval()
    {
        Assert.Throws<ArgumentException>(() => _factory.Create(
            RingCabinetTemplateType.Conventional,
            4,
            includePTInterval: true));
    }
}
