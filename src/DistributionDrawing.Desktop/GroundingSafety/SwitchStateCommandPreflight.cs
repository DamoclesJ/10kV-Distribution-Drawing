using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.GroundingSafety;
using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Desktop.GroundingSafety;

internal static class SwitchStateCommandPreflight
{
    public static void EnsureAllowed(ProjectRuntimeSession session, Guid switchDeviceId, SwitchState targetState)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Energization.CurrentResult is null)
        {
            return;
        }

        var overrides = new Dictionary<Guid, SwitchState> { [switchDeviceId] = targetState };
        CandidateElectricalState candidate = CandidateElectricalState.Create(
            session.PersistenceSession.Domain,
            overrides,
            session.PersistenceSession.EnergizationScenario);
        EnergizationResult candidateResult = new EnergizationAnalyzer().Analyze(
            session.PersistenceSession.Domain,
            candidate.CreateScenarioSnapshot(),
            candidate);
        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            session.PersistenceSession.Domain,
            candidateResult,
            candidate);
        if (decision.IsAllowed)
        {
            return;
        }

        string details = string.Join("；", decision.Findings.Select(finding =>
            $"{finding.Location}：{finding.Detail}"));
        throw new InvalidOperationException(
            $"[GroundingSafety] Grounding Safety 阻止开关操作：{details}");
    }
}
