using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class RingCabinetTemplateDomainBuilderTests
{
    private readonly RingCabinetTemplateDomainBuilder _builder = new();

    [Fact]
    public void Build_CreatesLoadSwitchCabinetAndPreservesBayMetadata()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));

        RingCabinetDomainBuildResult result = BuildSuccessfully(template);

        Assert.Equal("生产环网柜", result.Cabinet.DisplayName);
        Assert.Equal(3, result.Cabinet.Intervals.Count);
        Assert.All(
            result.Cabinet.Intervals,
            interval => Assert.Equal(
                IntervalKind.LoadSwitchInterval,
                interval.IntervalKind));
        Assert.Equal(new[] { 1, 2, 3 }, result.Cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 1, 2, 3 }, result.Cabinet.Intervals.Select(x => x.BayIndex));
        Assert.Equal(result.Definition.CabinetId, result.Cabinet.Id);
    }

    [Fact]
    public void Build_PreservesTemplateOrderAndNonContinuousBayIndexes()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            new BayTemplate(10, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()),
            new BayTemplate(8, new LoadSwitchConfiguration()));

        RingCabinetDomainBuildResult result = BuildSuccessfully(template);

        Assert.Equal(new[] { 1, 2, 3 }, result.Cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(new[] { 10, 3, 8 }, result.Cabinet.Intervals.Select(x => x.BayIndex));
    }

    [Fact]
    public void Build_CreatesIntegratedFeederCabinetAndPreservesGroundingStructures()
    {
        GroundingStructureKind[] structures =
        [
            GroundingStructureKind.UpperIsolationGrounding,
            GroundingStructureKind.UpperLowerGrounding,
            GroundingStructureKind.LowerLowerGrounding,
            GroundingStructureKind.UpperLowerGrounding
        ];
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            structures.Select((structure, index) => new BayTemplate(
                index + 1,
                new IntegratedFeederConfiguration(structure))).ToArray());

        RingCabinetDomainBuildResult result = BuildSuccessfully(template);

        Assert.All(
            result.Cabinet.Intervals,
            interval => Assert.Equal(
                IntervalKind.IntegratedFeederInterval,
                interval.IntervalKind));
        Assert.Equal(
            structures,
            result.Cabinet.Intervals.Select(x => x.GroundingStructureKind!.Value));
    }

    [Fact]
    public void Build_CreatesMixedCabinet()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Mixed,
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(
                4,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.LowerLowerGrounding)));

        RingCabinetDomainBuildResult result = BuildSuccessfully(template);

        Assert.Equal(CabinetCompositionKind.Mixed, result.Cabinet.CompositionKind);
        Assert.Equal(
            new[]
            {
                IntervalKind.LoadSwitchInterval,
                IntervalKind.IntegratedFeederInterval
            },
            result.Cabinet.Intervals.Select(x => x.IntervalKind));
    }

    [Fact]
    public void Build_RejectsDtuCapabilityBeforeDomainCreation()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            new DtuSecondaryConfiguration(),
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));

        RingCabinetDomainBuildOutcome outcome = _builder.Build(template, "DTU模板柜");

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            RingCabinetDomainBuildFailureKind.UnsupportedCapability,
            outcome.Failure!.Kind);
        Assert.Contains(
            TemplateCapability.DtuSecondary,
            outcome.Failure.UnsupportedCapabilities);
        Assert.Null(outcome.Failure.Cause);
    }

    [Fact]
    public void Build_ReturnsDomainCreationFailureForTwoLoadSwitchBays()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()));

        RingCabinetDomainBuildOutcome outcome = _builder.Build(template, "两间隔柜");

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            RingCabinetDomainBuildFailureKind.DomainCreationFailure,
            outcome.Failure!.Kind);
        Assert.IsType<InvalidOperationException>(outcome.Failure.Cause);
    }

    [Fact]
    public void Build_DoesNotUseTemplateIdAsDomainStableId()
    {
        Guid templateGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var template = new RingCabinetTemplate(
            new TemplateId(templateGuid.ToString()),
            "稳定标识测试",
            RingCabinetTemplateType.Conventional,
            [
                new BayTemplate(1, new LoadSwitchConfiguration()),
                new BayTemplate(2, new LoadSwitchConfiguration()),
                new BayTemplate(3, new LoadSwitchConfiguration())
            ],
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);

        RingCabinetDomainBuildResult first = BuildSuccessfully(template);
        RingCabinetDomainBuildResult second = BuildSuccessfully(template);

        Assert.NotEqual(templateGuid, first.Cabinet.Id);
        Assert.NotEqual(first.Cabinet.Id, second.Cabinet.Id);
        Assert.Empty(
            first.Cabinet.Intervals.Select(x => x.IntervalId)
                .Intersect(second.Cabinet.Intervals.Select(x => x.IntervalId)));
        Assert.Equal(templateGuid.ToString(), template.TemplateId.Value);
    }

    [Fact]
    public void Build_ReturnsInvalidTemplateForMissingDisplayName()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));

        RingCabinetDomainBuildOutcome outcome = _builder.Build(template, " ");

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            RingCabinetDomainBuildFailureKind.InvalidTemplate,
            outcome.Failure!.Kind);
    }

    private RingCabinetDomainBuildResult BuildSuccessfully(RingCabinetTemplate template)
    {
        RingCabinetDomainBuildOutcome outcome = _builder.Build(template, " 生产环网柜 ");

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Failure);
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result);
    }

    private static RingCabinetTemplate CreateTemplate(
        RingCabinetTemplateType templateType,
        params BayTemplate[] bays)
    {
        return CreateTemplate(
            templateType,
            NoSecondaryConfiguration.Instance,
            bays);
    }

    private static RingCabinetTemplate CreateTemplate(
        RingCabinetTemplateType templateType,
        SecondaryConfiguration secondaryConfiguration,
        params BayTemplate[] bays)
    {
        return new RingCabinetTemplate(
            new TemplateId("builtin:builder-test"),
            "Builder Test",
            templateType,
            bays,
            RingCabinetLayoutRule.Default,
            secondaryConfiguration);
    }
}
