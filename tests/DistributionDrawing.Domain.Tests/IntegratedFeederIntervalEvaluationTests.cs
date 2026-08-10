using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Devices.SwitchAssemblies;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class IntegratedFeederIntervalEvaluationTests
{
    private const string MutualExclusionRuleCode = "IF-IS-GS-MUTUAL-EXCLUSION";

    public static TheoryData<
        GroundingStructureKind,
        SwitchState,
        SwitchState,
        SwitchState,
        bool,
        OperationalState,
        bool,
        string?> EvaluationCases =>
        new()
        {
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Open,
                true, OperationalState.ColdStandby, false, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Closed,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Closed,
                true, OperationalState.Maintenance, true, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Open,
                true, OperationalState.HotStandby, false, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Running, false, null
            },
            {
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Open,
                true, OperationalState.ColdStandby, false, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Closed,
                true, OperationalState.Grounded, true, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Closed,
                true, OperationalState.Unclassified, true, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Open,
                true, OperationalState.HotStandby, false, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Running, false, null
            },
            {
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Open, SwitchState.Open, SwitchState.Closed,
                true, OperationalState.Grounded, true, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Open, SwitchState.Closed, SwitchState.Closed,
                true, OperationalState.Unclassified, true, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed, SwitchState.Open, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Open,
                true, OperationalState.Unclassified, false, null
            },
            {
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed, SwitchState.Closed, SwitchState.Closed,
                false, OperationalState.Unclassified, false, MutualExclusionRuleCode
            }
        };

    public static TheoryData<GroundingStructureKind, SwitchKind> IllegalStateChangeCases =>
        new()
        {
            { GroundingStructureKind.UpperIsolationGrounding, SwitchKind.IsolationSwitch },
            { GroundingStructureKind.UpperIsolationGrounding, SwitchKind.GroundSwitch },
            { GroundingStructureKind.UpperLowerGrounding, SwitchKind.IsolationSwitch },
            { GroundingStructureKind.UpperLowerGrounding, SwitchKind.GroundSwitch },
            { GroundingStructureKind.LowerLowerGrounding, SwitchKind.IsolationSwitch },
            { GroundingStructureKind.LowerLowerGrounding, SwitchKind.GroundSwitch }
        };

    [Theory]
    [MemberData(nameof(EvaluationCases))]
    public void EvaluateIntegratedFeederInterval_ReturnsExpectedResult(
        GroundingStructureKind groundingStructureKind,
        SwitchState isolationSwitchState,
        SwitchState circuitBreakerState,
        SwitchState groundSwitchState,
        bool expectedIsValid,
        OperationalState expectedOperationalState,
        bool expectedIsEffectivelyGrounded,
        string? expectedViolationCode)
    {
        RingCabinet cabinet = CreateCabinet(
            groundingStructureKind,
            isolationSwitchState,
            circuitBreakerState,
            groundSwitchState);
        RingCabinetInterval interval = cabinet.Intervals[0];

        SwitchAssemblyEvaluation evaluation =
            cabinet.EvaluateIntegratedFeederInterval(interval.IntervalId);

        Assert.Equal(expectedIsValid, evaluation.IsValid);
        Assert.Equal(expectedOperationalState, evaluation.OperationalState);
        Assert.Equal(expectedIsEffectivelyGrounded, evaluation.IsEffectivelyGrounded);

        if (expectedViolationCode is null)
        {
            Assert.Empty(evaluation.ViolatedRuleCodes);
        }
        else
        {
            Assert.Equal(
                expectedViolationCode,
                Assert.Single(evaluation.ViolatedRuleCodes));
        }
    }

    [Theory]
    [MemberData(nameof(IllegalStateChangeCases))]
    public void ChangeSwitchState_WhenTargetCombinationViolatesInterlock_LeavesAllStatesUnchanged(
        GroundingStructureKind groundingStructureKind,
        SwitchKind targetSwitchKind)
    {
        SwitchState initialIsolationState = targetSwitchKind == SwitchKind.IsolationSwitch
            ? SwitchState.Open
            : SwitchState.Closed;
        SwitchState initialGroundState = targetSwitchKind == SwitchKind.GroundSwitch
            ? SwitchState.Open
            : SwitchState.Closed;

        RingCabinet cabinet = CreateCabinet(
            groundingStructureKind,
            initialIsolationState,
            SwitchState.Closed,
            initialGroundState);
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice targetSwitch = GetSwitch(interval, targetSwitchKind);
        Dictionary<Guid, SwitchState?> statesBeforeChange = interval.SwitchDevices.ToDictionary(
            switchDevice => switchDevice.Id,
            switchDevice => switchDevice.SwitchState);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => interval.SwitchAssembly.ChangeSwitchState(
                targetSwitch.Id,
                SwitchState.Closed));

        Assert.Contains(MutualExclusionRuleCode, exception.Message, StringComparison.Ordinal);
        Assert.All(
            interval.SwitchDevices,
            switchDevice => Assert.Equal(
                statesBeforeChange[switchDevice.Id],
                switchDevice.SwitchState));
    }

    private static RingCabinet CreateCabinet(
        GroundingStructureKind groundingStructureKind,
        SwitchState isolationSwitchState,
        SwitchState circuitBreakerState,
        SwitchState groundSwitchState)
    {
        return RingCabinet.CreatePrimarySecondaryIntegratedCabinetBase(
            Guid.NewGuid(),
            "测试一二次融合环网柜",
            4,
            groundingStructureKind,
            isolationSwitchState,
            circuitBreakerState,
            groundSwitchState);
    }

    private static SwitchDevice GetSwitch(
        RingCabinetInterval interval,
        SwitchKind switchKind)
    {
        return Assert.Single(
            interval.SwitchDevices,
            switchDevice => switchDevice.SwitchKind == switchKind);
    }
}
