using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetTemplateLayoutBuilderTests
{
    private readonly RingCabinetTemplateDomainBuilder _domainBuilder = new();
    private readonly RingCabinetTemplateLayoutBuilder _layoutBuilder = new();

    [Fact]
    public void Build_CreatesDefaultLayoutAtRequestedPosition()
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        var position = new DocumentPoint(125.5, -40.25);

        RingCabinetLayout layout = BuildLayout(domainResult, position);

        Assert.Equal(domainResult.Cabinet.Id, layout.CabinetId);
        Assert.Equal(position, layout.Position);
        Assert.Equal(domainResult.Cabinet.Intervals.Count, layout.IntervalLayouts.Count);
    }

    [Fact]
    public void Build_UsesPositionAsInstanceInput()
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));

        RingCabinetLayout first = BuildLayout(
            domainResult,
            new DocumentPoint(10, 20));
        RingCabinetLayout second = BuildLayout(
            domainResult,
            new DocumentPoint(80, 90));

        Assert.Equal(new DocumentPoint(10, 20), first.Position);
        Assert.Equal(new DocumentPoint(80, 90), second.Position);
        Assert.Equal(first.CabinetId, second.CabinetId);
        Assert.Equal(
            first.IntervalLayouts.Keys.Order(),
            second.IntervalLayouts.Keys.Order());
    }

    [Fact]
    public void Build_MapsIntervalsByDomainSequenceWithoutSortingBayIndexes()
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(10, 3, 8));

        RingCabinetLayout layout = BuildLayout(domainResult, new DocumentPoint(0, 0));
        Guid[] layoutOrder = layout.IntervalLayouts.Values
            .OrderBy(interval => interval.RelativePosition.XMillimeters)
            .Select(interval => interval.IntervalId)
            .ToArray();

        Assert.Equal(new[] { 10, 3, 8 }, domainResult.Cabinet.Intervals.Select(x => x.BayIndex));
        Assert.Equal(
            domainResult.Cabinet.Intervals.Select(interval => interval.IntervalId),
            layoutOrder);
    }

    [Fact]
    public void Build_CreatesIntegratedFeederLayout()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.PrimarySecondaryIntegrated,
            Enumerable.Range(1, 4)
                .Select(index => new BayTemplate(
                    index,
                    index == 1 ? BayFunction.Incoming : BayFunction.Outgoing,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding)))
                .ToArray());
        RingCabinetDomainBuildResult domainResult = BuildDomain(template);

        RingCabinetLayout layout = BuildLayout(domainResult, new DocumentPoint(0, 0));

        Assert.Equal(4, layout.IntervalLayouts.Count);
        Assert.All(
            layout.IntervalLayouts.Values,
            interval => Assert.Equal(3, interval.SwitchLayouts.Count));
    }

    [Fact]
    public void Build_CreatesMixedCabinetLayout()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Mixed,
            new BayTemplate(2, BayFunction.Incoming, new LoadSwitchConfiguration()),
            new BayTemplate(
                7,
                BayFunction.Outgoing,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.LowerLowerGrounding)));
        RingCabinetDomainBuildResult domainResult = BuildDomain(template);

        RingCabinetLayout layout = BuildLayout(domainResult, new DocumentPoint(5, 6));

        Assert.Equal(2, layout.IntervalLayouts.Count);
        Assert.Equal(
            domainResult.Cabinet.Intervals.Select(interval => interval.IntervalId),
            layout.IntervalLayouts.Values
                .OrderBy(interval => interval.RelativePosition.XMillimeters)
                .Select(interval => interval.IntervalId));
    }

    [Fact]
    public void Build_RejectsUnknownLayoutRuleWithoutFallback()
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        var rule = new RingCabinetLayoutRule("custom:unsupported");

        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            domainResult,
            rule,
            new DocumentPoint(0, 0));

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Result);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.UnsupportedLayoutRule,
            outcome.Failure!.Kind);
        Assert.Equal(rule.RuleId, outcome.Failure.UnsupportedRuleId);
        Assert.Null(outcome.Failure.Cause);
    }

    [Fact]
    public void Build_RejectsMissingLayoutCapability()
    {
        RingCabinetDomainBuildResult complete = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        var withoutLayoutCapability = new RingCabinetDomainBuildResult(
            complete.Definition,
            complete.Cabinet,
            complete.RequiredCapabilities.Where(
                capability => capability != TemplateCapability.RingCabinetLayout));

        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            withoutLayoutCapability,
            RingCabinetLayoutRule.Default,
            new DocumentPoint(0, 0));

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.MissingRequiredCapability,
            outcome.Failure!.Kind);
        Assert.Equal(
            TemplateCapability.RingCabinetLayout,
            outcome.Failure.MissingCapability);
    }

    [Theory]
    [InlineData(TemplateCapability.PTBay)]
    [InlineData(TemplateCapability.DtuSecondary)]
    public void Build_RejectsUnsupportedDomainBuildCapability(
        TemplateCapability unsupportedCapability)
    {
        RingCabinetDomainBuildResult complete = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        RingCabinetDomainBuildResult inconsistent = WithAdditionalCapabilities(
            complete,
            unsupportedCapability);

        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            inconsistent,
            RingCabinetLayoutRule.Default,
            new DocumentPoint(0, 0));

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Result);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.UnsupportedCapability,
            outcome.Failure!.Kind);
        Assert.Contains(
            unsupportedCapability,
            outcome.Failure.UnsupportedCapabilities);
        Assert.Null(outcome.Failure.Cause);
    }

    [Fact]
    public void Build_RejectsCombinedPtAndDtuCapabilities()
    {
        RingCabinetDomainBuildResult complete = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        RingCabinetDomainBuildResult inconsistent = WithAdditionalCapabilities(
            complete,
            TemplateCapability.PTBay,
            TemplateCapability.DtuSecondary);

        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            inconsistent,
            RingCabinetLayoutRule.Default,
            new DocumentPoint(0, 0));

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Result);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.UnsupportedCapability,
            outcome.Failure!.Kind);
        Assert.Equal(
            new[] { TemplateCapability.PTBay, TemplateCapability.DtuSecondary },
            outcome.Failure.UnsupportedCapabilities.Order());
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void Build_RejectsNonFinitePosition(double x, double y)
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));

        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            domainResult,
            RingCabinetLayoutRule.Default,
            new DocumentPoint(x, y));

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.InvalidInput,
            outcome.Failure!.Kind);
    }

    [Fact]
    public void Build_RejectsMissingDomainResultAndRule()
    {
        RingCabinetLayoutBuildOutcome missingDomain = _layoutBuilder.Build(
            null,
            RingCabinetLayoutRule.Default,
            new DocumentPoint(0, 0));
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        RingCabinetLayoutBuildOutcome missingRule = _layoutBuilder.Build(
            domainResult,
            null,
            new DocumentPoint(0, 0));

        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.InvalidInput,
            missingDomain.Failure!.Kind);
        Assert.Equal(
            RingCabinetLayoutBuildFailureKind.InvalidInput,
            missingRule.Failure!.Kind);
    }

    [Fact]
    public void Build_DoesNotChangeDomainStableIds()
    {
        RingCabinetDomainBuildResult domainResult = BuildDomain(
            CreateLoadSwitchTemplate(1, 2, 3));
        Guid cabinetId = domainResult.Cabinet.Id;
        Guid[] intervalIds = domainResult.Cabinet.Intervals
            .Select(interval => interval.IntervalId)
            .ToArray();
        Guid[] switchIds = domainResult.Cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.Id)
            .ToArray();

        RingCabinetLayout layout = BuildLayout(domainResult, new DocumentPoint(12, 34));

        Assert.Equal(cabinetId, domainResult.Cabinet.Id);
        Assert.Equal(intervalIds, domainResult.Cabinet.Intervals.Select(x => x.IntervalId));
        Assert.Equal(
            switchIds,
            domainResult.Cabinet.Intervals
                .SelectMany(interval => interval.SwitchDevices)
                .Select(device => device.Id));
        Assert.Equal(cabinetId, layout.CabinetId);
        Assert.Equal(intervalIds.Order(), layout.IntervalLayouts.Keys.Order());
    }

    private RingCabinetDomainBuildResult BuildDomain(RingCabinetTemplate template)
    {
        RingCabinetDomainBuildOutcome outcome = _domainBuilder.Build(template, "布局测试柜");

        Assert.True(outcome.IsSuccess);
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result);
    }

    private RingCabinetLayout BuildLayout(
        RingCabinetDomainBuildResult domainResult,
        DocumentPoint position)
    {
        RingCabinetLayoutBuildOutcome outcome = _layoutBuilder.Build(
            domainResult,
            RingCabinetLayoutRule.Default,
            position);

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Failure);
        return Assert.IsType<RingCabinetLayoutBuildResult>(outcome.Result).Layout;
    }

    private static RingCabinetDomainBuildResult WithAdditionalCapabilities(
        RingCabinetDomainBuildResult result,
        params TemplateCapability[] capabilities)
    {
        return new RingCabinetDomainBuildResult(
            result.Definition,
            result.Cabinet,
            result.RequiredCapabilities.Concat(capabilities));
    }

    private static RingCabinetTemplate CreateLoadSwitchTemplate(params int[] indexes)
    {
        return CreateTemplate(
            RingCabinetTemplateType.Conventional,
            indexes.Select((index, sequence) => new BayTemplate(
                index,
                sequence == 0 ? BayFunction.Incoming : BayFunction.Outgoing,
                new LoadSwitchConfiguration())).ToArray());
    }

    private static RingCabinetTemplate CreateTemplate(
        RingCabinetTemplateType templateType,
        params BayTemplate[] bays)
    {
        return new RingCabinetTemplate(
            new TemplateId("builtin:layout-builder-test"),
            "Layout Builder Test",
            templateType,
            bays,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);
    }
}
