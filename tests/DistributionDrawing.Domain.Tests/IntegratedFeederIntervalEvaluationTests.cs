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
        Dictionary<Guid, SwitchState?> candidateStates = interval.SwitchDevices.ToDictionary(
            switchDevice => switchDevice.Id,
            switchDevice => switchDevice.SwitchKind switch
            {
                SwitchKind.IsolationSwitch => (SwitchState?)isolationSwitchState,
                SwitchKind.CircuitBreaker => circuitBreakerState,
                SwitchKind.GroundSwitch => groundSwitchState,
                _ => throw new ArgumentOutOfRangeException()
            });
        SwitchAssemblyEvaluation candidateEvaluation = cabinet.EvaluateIntegratedFeederInterval(
            interval.IntervalId,
            new DictionarySwitchStateView(candidateStates));

        Assert.Equal(expectedIsValid, evaluation.IsValid);
        Assert.Equal(expectedOperationalState, evaluation.OperationalState);
        Assert.Equal(expectedIsEffectivelyGrounded, evaluation.IsEffectivelyGrounded);
        Assert.Equal(evaluation.IsValid, candidateEvaluation.IsValid);
        Assert.Equal(evaluation.OperationalState, candidateEvaluation.OperationalState);
        Assert.Equal(evaluation.IsEffectivelyGrounded, candidateEvaluation.IsEffectivelyGrounded);

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

    [Fact]
    public void EffectiveGroundingLocations_UseCandidateStateWithoutMutatingCurrentFacts()
    {
        RingCabinet cabinet = CreateCabinet(
            GroundingStructureKind.UpperLowerGrounding,
            SwitchState.Open,
            SwitchState.Open,
            SwitchState.Open);
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice groundSwitch = GetSwitch(interval, SwitchKind.GroundSwitch);
        Dictionary<Guid, SwitchState?> before = interval.SwitchDevices.ToDictionary(
            item => item.Id,
            item => item.SwitchState);
        var view = new DictionarySwitchStateView(new Dictionary<Guid, SwitchState?>
        {
            [groundSwitch.Id] = SwitchState.Closed
        });

        EffectiveGroundingLocation location = Assert.Single(
            cabinet.GetEffectiveGroundingLocations(view));

        Assert.Equal(cabinet.Id, location.CabinetId);
        Assert.Equal(interval.IntervalId, location.IntervalId);
        Assert.Equal(interval.CircuitNodeId, location.CircuitNodeId);
        Assert.Equal(interval.CableTerminalId, location.CableTerminalId);
        Assert.All(interval.SwitchDevices, item => Assert.Equal(before[item.Id], item.SwitchState));
        Assert.Empty(cabinet.GetEffectiveGroundingLocations());

        SwitchDevice isolationSwitch = GetSwitch(interval, SwitchKind.IsolationSwitch);
        var invalidView = new DictionarySwitchStateView(new Dictionary<Guid, SwitchState?>
        {
            [groundSwitch.Id] = SwitchState.Closed,
            [isolationSwitch.Id] = SwitchState.Closed
        });
        Assert.Throws<InvalidOperationException>(() =>
            cabinet.GetEffectiveGroundingLocations(invalidView));
    }

    [Fact]
    public void EffectiveGroundingLocations_ReuseOrdinaryLoadSwitchAssemblyRule()
    {
        RingCabinet cabinet = TestFixtures.CreateLoadSwitchRingCabinet([1, 2]);
        cabinet.SetIntervalCableTerminal(cabinet.Intervals[0].IntervalId, null);
        RingCabinetInterval interval = cabinet.Intervals[0];
        SwitchDevice groundSwitch = GetSwitch(interval, SwitchKind.GroundSwitch);
        var view = new DictionarySwitchStateView(new Dictionary<Guid, SwitchState?>
        {
            [groundSwitch.Id] = SwitchState.Closed
        });

        EffectiveGroundingLocation location = Assert.Single(
            cabinet.GetEffectiveGroundingLocations(view));

        Assert.Equal(interval.CircuitNodeId, location.CircuitNodeId);
        Assert.Null(location.CableTerminalId);
        Assert.Equal(SwitchState.Open, groundSwitch.SwitchState);
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
        int[] bayIndexes = [1, 2, 5, 7];
        RingCabinetIntervalDefinition[] intervals = bayIndexes
            .Select(bayIndex => RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                bayIndex,
                groundingStructureKind,
                isolationSwitchState,
                circuitBreakerState,
                groundSwitchState,
                $"负{bayIndex}间隔"))
            .ToArray();

        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试一二次融合环网柜",
            intervals));
    }

    private static SwitchDevice GetSwitch(
        RingCabinetInterval interval,
        SwitchKind switchKind)
    {
        return Assert.Single(
            interval.SwitchDevices,
            switchDevice => switchDevice.SwitchKind == switchKind);
    }

    private sealed class DictionarySwitchStateView(
        IReadOnlyDictionary<Guid, SwitchState?> overrides) : ISwitchStateView
    {
        public SwitchState? GetSwitchState(SwitchDevice switchDevice) =>
            overrides.TryGetValue(switchDevice.Id, out SwitchState? state)
                ? state
                : switchDevice.SwitchState;
    }
}
