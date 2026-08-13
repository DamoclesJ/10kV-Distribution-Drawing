using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetTemplateBuildCoordinatorTests
{
    private readonly RingCabinetTemplateBuildCoordinator _coordinator = new();

    [Fact]
    public void Build_CreatesCompleteLoadSwitchResult()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2,
            3);
        var position = new DocumentPoint(45.5, -12.25);

        RingCabinetTemplateBuildResult result = BuildSuccessfully(
            new RingCabinetTemplateBuildRequest(
                template,
                "模板环网柜",
                position));

        Assert.Equal("模板环网柜", result.Cabinet.DisplayName);
        Assert.Equal(result.Definition.CabinetId, result.Cabinet.Id);
        Assert.Equal(result.Cabinet.Id, result.Layout.CabinetId);
        Assert.Equal(position, result.Layout.Position);
        Assert.Same(result.DomainResult.Definition, result.Definition);
        Assert.Same(result.DomainResult.Cabinet, result.Cabinet);
        Assert.Same(result.LayoutResult.Layout, result.Layout);
        Assert.Same(
            result.DomainResult.RequiredCapabilities,
            result.RequiredCapabilities);
        Assert.Contains(
            TemplateCapability.RingCabinetLayout,
            result.RequiredCapabilities);
        Assert.Contains(
            TemplateCapability.LoadSwitchBay,
            result.RequiredCapabilities);
    }

    [Fact]
    public void Build_CreatesIntegratedFeederResult()
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
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance,
            structures.Select((structure, index) => new BayTemplate(
                index + 1,
                index == 0 ? BayFunction.Incoming : BayFunction.Outgoing,
                new IntegratedFeederConfiguration(structure))).ToArray());

        RingCabinetTemplateBuildResult result = BuildSuccessfully(
            new RingCabinetTemplateBuildRequest(
                template,
                "一二次融合柜",
                new DocumentPoint(0, 0)));

        Assert.Equal(
            structures,
            result.Cabinet.Intervals.Select(x => x.GroundingStructureKind!.Value));
        Assert.Equal(4, result.Layout.IntervalLayouts.Count);
        Assert.All(
            result.Layout.IntervalLayouts.Values,
            interval => Assert.Equal(3, interval.SwitchLayouts.Count));
        Assert.Contains(
            TemplateCapability.IntegratedFeederBay,
            result.RequiredCapabilities);
    }

    [Fact]
    public void Build_CreatesMixedCabinetResult()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Mixed,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance,
            new BayTemplate(2, BayFunction.Incoming, new LoadSwitchConfiguration()),
            new BayTemplate(
                7,
                BayFunction.Outgoing,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.LowerLowerGrounding)));

        RingCabinetTemplateBuildResult result = BuildSuccessfully(
            new RingCabinetTemplateBuildRequest(
                template,
                "混合柜",
                new DocumentPoint(10, 20)));

        Assert.Equal(CabinetCompositionKind.Mixed, result.Cabinet.CompositionKind);
        Assert.Equal(2, result.Layout.IntervalLayouts.Count);
        Assert.Contains(TemplateCapability.LoadSwitchBay, result.RequiredCapabilities);
        Assert.Contains(
            TemplateCapability.IntegratedFeederBay,
            result.RequiredCapabilities);
    }

    [Fact]
    public void Build_PreservesTemplateOrderAndNonContinuousBayIndexes()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            10,
            3,
            8);

        RingCabinetTemplateBuildResult result = BuildSuccessfully(
            new RingCabinetTemplateBuildRequest(
                template,
                "非连续编号柜",
                new DocumentPoint(0, 0)));
        Guid[] layoutOrder = result.Layout.IntervalLayouts.Values
            .OrderBy(interval => interval.RelativePosition.XMillimeters)
            .Select(interval => interval.IntervalId)
            .ToArray();

        Assert.Equal(
            new[] { 1, 2, 3 },
            result.Cabinet.Intervals.Select(x => x.Sequence));
        Assert.Equal(
            new[] { 10, 3, 8 },
            result.Cabinet.Intervals.Select(x => x.BayIndex));
        Assert.Equal(
            result.Cabinet.Intervals.Select(x => x.IntervalId),
            layoutOrder);
    }

    [Fact]
    public void Build_MapsPtFailureFromDomainStage()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance,
            new BayTemplate(1, BayFunction.PT, new LoadSwitchConfiguration()),
            new BayTemplate(2, BayFunction.Outgoing, new LoadSwitchConfiguration()),
            new BayTemplate(3, BayFunction.Tie, new LoadSwitchConfiguration()));

        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                "PT模板柜",
                new DocumentPoint(0, 0)));

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Domain,
            RingCabinetTemplateBuildFailureKind.UnsupportedCapability);
        Assert.Contains(
            TemplateCapability.PTBay,
            outcome.Failure!.UnsupportedCapabilities);
    }

    [Fact]
    public void Build_MapsDtuFailureFromDomainStage()
    {
        RingCabinetTemplate template = CreateTemplate(
            RingCabinetTemplateType.Conventional,
            RingCabinetLayoutRule.Default,
            new DtuSecondaryConfiguration(),
            new BayTemplate(1, BayFunction.Incoming, new LoadSwitchConfiguration()),
            new BayTemplate(2, BayFunction.Outgoing, new LoadSwitchConfiguration()),
            new BayTemplate(3, BayFunction.Tie, new LoadSwitchConfiguration()));

        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                "DTU模板柜",
                new DocumentPoint(0, 0)));

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Domain,
            RingCabinetTemplateBuildFailureKind.UnsupportedCapability);
        Assert.Contains(
            TemplateCapability.DtuSecondary,
            outcome.Failure!.UnsupportedCapabilities);
    }

    [Fact]
    public void Build_MapsUnknownRuleFailureFromLayoutStage()
    {
        var rule = new RingCabinetLayoutRule("custom:unknown");
        RingCabinetTemplate template = CreateLoadSwitchTemplate(rule, 1, 2, 3);

        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                "未知布局规则柜",
                new DocumentPoint(0, 0)));

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Layout,
            RingCabinetTemplateBuildFailureKind.UnsupportedLayoutRule);
        Assert.Equal(rule.RuleId, outcome.Failure!.UnsupportedRuleId);
    }

    [Fact]
    public void Build_MapsNonFinitePositionFromLayoutStage()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2,
            3);

        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                "非法位置柜",
                new DocumentPoint(double.NaN, 0)));

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Layout,
            RingCabinetTemplateBuildFailureKind.InvalidLayoutInput);
    }

    [Fact]
    public void Build_MapsTwoBayFailureFromDomainStage()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2);

        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                "两间隔柜",
                new DocumentPoint(0, 0)));

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Domain,
            RingCabinetTemplateBuildFailureKind.DomainCreationFailure);
        Assert.IsType<InvalidOperationException>(outcome.Failure!.Cause);
    }

    [Fact]
    public void Build_ReturnsTypedFailureForMissingRequest()
    {
        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(null);

        AssertFailure(
            outcome,
            RingCabinetTemplateBuildFailureStage.Coordinator,
            RingCabinetTemplateBuildFailureKind.InvalidTemplate);
    }

    [Fact]
    public void Build_MapsMissingTemplateAndDisplayNameThroughDomainStage()
    {
        RingCabinetTemplateBuildOutcome missingTemplate = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                null,
                "名称",
                new DocumentPoint(0, 0)));
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2,
            3);
        RingCabinetTemplateBuildOutcome missingName = _coordinator.Build(
            new RingCabinetTemplateBuildRequest(
                template,
                " ",
                new DocumentPoint(0, 0)));

        AssertFailure(
            missingTemplate,
            RingCabinetTemplateBuildFailureStage.Domain,
            RingCabinetTemplateBuildFailureKind.InvalidTemplate);
        AssertFailure(
            missingName,
            RingCabinetTemplateBuildFailureStage.Domain,
            RingCabinetTemplateBuildFailureKind.InvalidTemplate);
    }

    [Fact]
    public void Build_TwiceCreatesDifferentDomainStableIds()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2,
            3);
        var request = new RingCabinetTemplateBuildRequest(
            template,
            "重复构建测试柜",
            new DocumentPoint(0, 0));

        RingCabinetTemplateBuildResult first = BuildSuccessfully(request);
        RingCabinetTemplateBuildResult second = BuildSuccessfully(request);

        Assert.NotEqual(first.Cabinet.Id, second.Cabinet.Id);
        Assert.Empty(
            first.Cabinet.Intervals.Select(x => x.IntervalId)
                .Intersect(second.Cabinet.Intervals.Select(x => x.IntervalId)));
        Assert.Equal(first.Cabinet.Id, first.Layout.CabinetId);
        Assert.Equal(second.Cabinet.Id, second.Layout.CabinetId);
    }

    [Fact]
    public void FullResult_RejectsMismatchedCabinetAndLayout()
    {
        RingCabinetTemplate template = CreateLoadSwitchTemplate(
            RingCabinetLayoutRule.Default,
            1,
            2,
            3);
        var request = new RingCabinetTemplateBuildRequest(
            template,
            "标识校验柜",
            new DocumentPoint(0, 0));
        RingCabinetTemplateBuildResult first = BuildSuccessfully(request);
        RingCabinetTemplateBuildResult second = BuildSuccessfully(request);

        Assert.Throws<ArgumentException>(() =>
            new RingCabinetTemplateBuildResult(
                first.DomainResult,
                second.LayoutResult));
    }

    [Fact]
    public void BuildApi_ExposesReadOnlyResultAndRequestProperties()
    {
        string[] requestProperties = ["Template", "DisplayName", "Position"];
        string[] resultProperties =
        [
            "DomainResult",
            "LayoutResult",
            "Definition",
            "Cabinet",
            "Layout",
            "RequiredCapabilities"
        ];

        Assert.All(
            requestProperties,
            name => Assert.False(
                typeof(RingCabinetTemplateBuildRequest)
                    .GetProperty(name)!
                    .CanWrite));
        Assert.All(
            resultProperties,
            name => Assert.False(
                typeof(RingCabinetTemplateBuildResult)
                    .GetProperty(name)!
                    .CanWrite));
    }

    [Fact]
    public void FailureMapping_PreservesLayoutCapabilityDiagnostics()
    {
        RingCabinetTemplateBuildFailure missingCapability =
            RingCabinetTemplateBuildFailure.FromLayoutFailure(
                RingCabinetLayoutBuildFailure.MissingRequiredCapability(
                    TemplateCapability.RingCabinetLayout));
        RingCabinetTemplateBuildFailure unsupportedCapability =
            RingCabinetTemplateBuildFailure.FromLayoutFailure(
                RingCabinetLayoutBuildFailure.UnsupportedCapability(
                    [TemplateCapability.PTBay, TemplateCapability.DtuSecondary]));

        Assert.Equal(
            RingCabinetTemplateBuildFailureStage.Layout,
            missingCapability.Stage);
        Assert.Equal(
            RingCabinetTemplateBuildFailureKind.MissingRequiredCapability,
            missingCapability.Kind);
        Assert.Equal(
            TemplateCapability.RingCabinetLayout,
            missingCapability.MissingCapability);
        Assert.Equal(
            RingCabinetTemplateBuildFailureKind.UnsupportedCapability,
            unsupportedCapability.Kind);
        Assert.Equal(
            new[] { TemplateCapability.PTBay, TemplateCapability.DtuSecondary },
            unsupportedCapability.UnsupportedCapabilities.Order());
    }

    private RingCabinetTemplateBuildResult BuildSuccessfully(
        RingCabinetTemplateBuildRequest request)
    {
        RingCabinetTemplateBuildOutcome outcome = _coordinator.Build(request);

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Failure);
        return Assert.IsType<RingCabinetTemplateBuildResult>(outcome.Result);
    }

    private static void AssertFailure(
        RingCabinetTemplateBuildOutcome outcome,
        RingCabinetTemplateBuildFailureStage stage,
        RingCabinetTemplateBuildFailureKind kind)
    {
        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Result);
        RingCabinetTemplateBuildFailure failure =
            Assert.IsType<RingCabinetTemplateBuildFailure>(outcome.Failure);
        Assert.Equal(stage, failure.Stage);
        Assert.Equal(kind, failure.Kind);
    }

    private static RingCabinetTemplate CreateLoadSwitchTemplate(
        RingCabinetLayoutRule layoutRule,
        params int[] indexes)
    {
        return CreateTemplate(
            RingCabinetTemplateType.Conventional,
            layoutRule,
            NoSecondaryConfiguration.Instance,
            indexes.Select((index, sequence) => new BayTemplate(
                index,
                sequence == 0 ? BayFunction.Incoming : BayFunction.Outgoing,
                new LoadSwitchConfiguration())).ToArray());
    }

    private static RingCabinetTemplate CreateTemplate(
        RingCabinetTemplateType templateType,
        RingCabinetLayoutRule layoutRule,
        SecondaryConfiguration secondaryConfiguration,
        params BayTemplate[] bays)
    {
        return new RingCabinetTemplate(
            new TemplateId("builtin:coordinator-test"),
            "Coordinator Test",
            templateType,
            bays,
            layoutRule,
            secondaryConfiguration);
    }
}
