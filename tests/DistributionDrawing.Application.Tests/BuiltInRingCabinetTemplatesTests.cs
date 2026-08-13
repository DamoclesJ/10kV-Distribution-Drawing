using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;
using DistributionDrawing.Application.Templates.RingCabinets.Library;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class BuiltInRingCabinetTemplatesTests
{
    [Fact]
    public void CreateLibrary_RegistersConventionalTemplate()
    {
        RingCabinetTemplateLibrary library = BuiltInRingCabinetTemplates.CreateLibrary();

        bool found = library.TryGet(
            new TemplateId(Conventional10kVRingCabinetTemplate.TemplateIdValue),
            out RingCabinetTemplate? template);

        Assert.True(found);
        Assert.NotNull(template);
        Assert.Single(library.Templates);
        Assert.Same(template, library.Templates[0]);
    }

    [Fact]
    public void ConventionalTemplate_HasApprovedMetadataAndDefaults()
    {
        RingCabinetTemplate template = Conventional10kVRingCabinetTemplate.Create();

        Assert.Equal(
            "builtin:ring-cabinet/conventional/3-bay",
            template.TemplateId.Value);
        Assert.Same(
            Conventional10kVRingCabinetTemplate.TemplateId,
            template.TemplateId);
        Assert.Equal("10kV 常规三间隔环网柜", template.Name);
        Assert.Equal(1, Conventional10kVRingCabinetTemplate.SchemaVersion);
        Assert.Equal(
            RingCabinetTemplateType.Conventional,
            template.CabinetType);
        Assert.Equal(
            RingCabinetTemplateType.Conventional,
            Conventional10kVRingCabinetTemplate.DefaultCabinetType);
        Assert.Equal(
            RingCabinetLayoutRule.Default,
            template.LayoutRule);
        Assert.Same(
            RingCabinetLayoutRule.Default,
            Conventional10kVRingCabinetTemplate.LayoutReference);
        Assert.Same(
            NoSecondaryConfiguration.Instance,
            template.SecondaryConfiguration);
        Assert.Same(
            NoSecondaryConfiguration.Instance,
            Conventional10kVRingCabinetTemplate.DefaultSecondaryConfiguration);
    }

    [Fact]
    public void ConventionalTemplate_DefinesThreeLoadSwitchIntervals()
    {
        RingCabinetTemplate template = Conventional10kVRingCabinetTemplate.Create();

        Assert.Equal(
            Conventional10kVRingCabinetTemplate.DefaultIntervalCount,
            template.Bays.Count);
        Assert.Equal(
            template.Bays.Select(bay => bay.Index),
            Conventional10kVRingCabinetTemplate.DefaultIntervals.Select(
                bay => bay.Index));
        Assert.Equal(new[] { 1, 2, 3 }, template.Bays.Select(bay => bay.Index));
        Assert.All(
            template.Bays,
            bay => Assert.IsType<LoadSwitchConfiguration>(
                bay.EquipmentConfiguration));
        Assert.Equal(
            new[]
            {
                TemplateCapability.BasicRingCabinet,
                TemplateCapability.LoadSwitchBay,
                TemplateCapability.RingCabinetLayout
            },
            template.RequiredCapabilities.OrderBy(capability => capability));
    }

    [Fact]
    public void ConventionalTemplate_BuildsApprovedDomainStructure()
    {
        RingCabinetTemplate template = Conventional10kVRingCabinetTemplate.Create();
        var builder = new RingCabinetTemplateDomainBuilder();

        RingCabinetDomainBuildOutcome outcome = builder.Build(
            template,
            "测试常规环网柜");

        Assert.True(outcome.IsSuccess);
        RingCabinetDomainBuildResult result = Assert.IsType<RingCabinetDomainBuildResult>(
            outcome.Result);
        Assert.Equal(new[] { 1, 2, 3 }, result.Cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 1, 2, 3 }, result.Cabinet.Intervals.Select(x => x.BayIndex));
        Assert.All(
            result.Cabinet.Intervals,
            interval =>
            {
                Assert.Equal(IntervalKind.LoadSwitchInterval, interval.IntervalKind);
                Assert.Contains(
                    interval.SwitchDevices,
                    device => device.SwitchKind == SwitchKind.LoadSwitch &&
                              device.SwitchState == SwitchState.Open);
                Assert.Contains(
                    interval.SwitchDevices,
                    device => device.SwitchKind == SwitchKind.GroundSwitch &&
                              device.SwitchState == SwitchState.Open);
                Assert.NotEqual(Guid.Empty, interval.ExternalTerminalId);
            });
        Assert.Null(typeof(RingCabinetInterval).GetProperty("Function"));
        Assert.Null(typeof(BayTemplate).GetProperty("Function"));
    }

    [Fact]
    public void ConventionalTemplate_CreatesIndependentDomainInstances()
    {
        RingCabinetTemplate template = Conventional10kVRingCabinetTemplate.Create();
        var builder = new RingCabinetTemplateDomainBuilder();

        RingCabinetDomainBuildResult first = BuildSuccessfully(builder, template, "柜一");
        RingCabinetDomainBuildResult second = BuildSuccessfully(builder, template, "柜二");

        Assert.NotEqual(first.Cabinet.Id, second.Cabinet.Id);
        Assert.Empty(
            first.Cabinet.Intervals.Select(interval => interval.IntervalId)
                .Intersect(second.Cabinet.Intervals.Select(interval => interval.IntervalId)));
    }

    private static RingCabinetDomainBuildResult BuildSuccessfully(
        RingCabinetTemplateDomainBuilder builder,
        RingCabinetTemplate template,
        string displayName)
    {
        RingCabinetDomainBuildOutcome outcome = builder.Build(template, displayName);

        Assert.True(outcome.IsSuccess);
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result);
    }
}
