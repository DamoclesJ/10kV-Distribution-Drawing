using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class RingCabinetTemplateTests
{
    [Fact]
    public void Constructor_CreatesImmutableTemplateAndDerivesCapabilities()
    {
        var source = new List<BayTemplate>
        {
            new(1, new LoadSwitchConfiguration()),
            new(
                5,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding))
        };

        RingCabinetTemplate template = CreateTemplate(source);
        source.Add(new BayTemplate(7, new LoadSwitchConfiguration()));

        Assert.Equal("builtin:test", template.TemplateId.Value);
        Assert.Equal("测试模板", template.Name);
        Assert.Equal(new[] { 1, 5 }, template.Bays.Select(bay => bay.Index));
        Assert.Contains(TemplateCapability.BasicRingCabinet, template.RequiredCapabilities);
        Assert.Contains(TemplateCapability.LoadSwitchBay, template.RequiredCapabilities);
        Assert.Contains(TemplateCapability.IntegratedFeederBay, template.RequiredCapabilities);
        Assert.Contains(TemplateCapability.RingCabinetLayout, template.RequiredCapabilities);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BayTemplate>)template.Bays).Add(
                new BayTemplate(9, new LoadSwitchConfiguration())));
        Assert.All(
            typeof(RingCabinetTemplate).GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Constructor_RejectsDuplicateBayIndexes()
    {
        BayTemplate[] bays =
        [
            new(1, new LoadSwitchConfiguration()),
            new(1, new LoadSwitchConfiguration())
        ];

        Assert.Throws<ArgumentException>(() => CreateTemplate(bays));
    }

    [Fact]
    public void Constructor_RejectsEmptyBays()
    {
        Assert.Throws<ArgumentException>(() => CreateTemplate([]));
    }

    [Fact]
    public void Constructor_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new RingCabinetTemplate(
            new TemplateId("builtin:test"),
            " ",
            RingCabinetTemplateType.Conventional,
            [new BayTemplate(1, new LoadSwitchConfiguration())],
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance));
    }

    [Fact]
    public void TemplateId_RejectsEmptyValue()
    {
        Assert.Throws<ArgumentException>(() => new TemplateId(" "));
    }

    [Fact]
    public void DtuConfiguration_DerivesUnsupportedCapabilityMarker()
    {
        var template = new RingCabinetTemplate(
            new TemplateId("builtin:dtu-test"),
            "DTU能力模板",
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            [new BayTemplate(1, new LoadSwitchConfiguration())],
            RingCabinetLayoutRule.Default,
            new DtuSecondaryConfiguration());

        Assert.Contains(TemplateCapability.DtuSecondary, template.RequiredCapabilities);
    }

    [Fact]
    public void CapabilityModel_DoesNotExposeLegacyPtFunctionCapability()
    {
        Assert.DoesNotContain("PTBay", Enum.GetNames<TemplateCapability>());
    }

    private static RingCabinetTemplate CreateTemplate(IEnumerable<BayTemplate> bays)
    {
        return new RingCabinetTemplate(
            new TemplateId("builtin:test"),
            " 测试模板 ",
            RingCabinetTemplateType.Mixed,
            bays,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);
    }
}
