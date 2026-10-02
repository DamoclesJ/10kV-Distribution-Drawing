using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.GroundingSafety;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;

namespace DistributionDrawing.Desktop.GroundingSafety;

internal static class GroundingPointRestorePreflight
{
    public static void EnsureAllowed(
        ProjectRuntimeSession session,
        IEnumerable<GroundingPointCommandSnapshot> snapshots,
        IEnumerable<GroundingAccessPoint>? prospectiveGroundingAccessPoints = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshots);
        EnergizationResult? currentResult = session.Energization.CurrentResult;
        if (currentResult is null)
        {
            return;
        }

        GroundingPoint[] candidates = snapshots.Select(snapshot => GroundingPoint.Create(
            snapshot.GroundingPointId,
            snapshot.Target,
            snapshot.Location,
            snapshot.Number,
            snapshot.Note)).ToArray();
        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            session.PersistenceSession.Domain,
            currentResult,
            CurrentSwitchStateView.Instance,
            candidates,
            prospectiveGroundingAccessPoints);
        if (decision.IsAllowed)
        {
            return;
        }

        string details = string.Join("；", decision.Findings.Select(finding =>
            $"{finding.Location}：{finding.Detail}"));
        throw new InvalidOperationException(
            $"Grounding Safety 阻止恢复工作地线：{details}");
    }
}
