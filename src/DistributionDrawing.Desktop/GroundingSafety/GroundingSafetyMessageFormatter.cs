using DistributionDrawing.Application.GroundingSafety;

namespace DistributionDrawing.Desktop.GroundingSafety;

internal static class GroundingSafetyMessageFormatter
{
    public static string Format(string action, GroundingSafetyDecision decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(decision);
        string details = string.Join("；", decision.Findings.Select(finding =>
            $"{finding.Location}：{Describe(finding.Code)}"));
        return $"{action}被接地安全校验阻止：{details}";
    }

    private static string Describe(GroundingSafetyFindingCode code) => code switch
    {
        GroundingSafetyFindingCode.AnalysisUnavailable => "候选带电分析不可用。",
        GroundingSafetyFindingCode.GroundingTargetUnresolved => "无法可靠解析接地点的电气身份。",
        GroundingSafetyFindingCode.EnergizedGroundedLocation => "候选状态下该接地点将处于带电状态。",
        GroundingSafetyFindingCode.EffectiveGroundingUnresolved => "无法确定环网柜的有效接地位置。",
        _ => "接地安全校验未通过。"
    };
}
