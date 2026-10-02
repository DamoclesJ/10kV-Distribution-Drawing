using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.GroundingSafety;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;

namespace DistributionDrawing.Desktop.GroundingSafety;

internal static class GroundingPointCommandPreflight
{
    public static void EnsureAllowed(
        ProjectRuntimeSession session,
        GroundingPointCommandSnapshot snapshot,
        GroundingAccessPoint? prospectiveGroundingAccessPoint = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshot);
        EnergizationResult? currentResult = session.Energization.CurrentResult;
        if (currentResult is null)
        {
            return;
        }

        GroundingPoint candidate = GroundingPoint.Create(
            snapshot.GroundingPointId,
            snapshot.Target,
            snapshot.Location,
            snapshot.Number,
            snapshot.Note);
        GroundingSafetyDecision decision = new GroundingSafetyGuard().Evaluate(
            session.PersistenceSession.Domain,
            currentResult,
            CurrentSwitchStateView.Instance,
            [candidate],
            prospectiveGroundingAccessPoint is null
                ? []
                : [prospectiveGroundingAccessPoint]);
        if (decision.IsAllowed)
        {
            return;
        }

        string details = string.Join("；", decision.Findings.Select(finding =>
            $"{finding.Location}：{finding.Detail}"));
        throw new InvalidOperationException(
            $"无法添加工作地线，Grounding Safety 校验未通过：{details}");
    }
}
