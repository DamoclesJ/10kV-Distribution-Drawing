using DistributionDrawing.Application.Topology;

namespace DistributionDrawing.Application.GroundingSafety;

public enum GroundingSafetyFindingCode
{
    AnalysisUnavailable,
    GroundingTargetUnresolved,
    EnergizedGroundedLocation,
    EffectiveGroundingUnresolved
}

public sealed record GroundingSafetyFinding(
    GroundingSafetyFindingCode Code,
    string Location,
    GroundingElectricalIdentity? Identity,
    string Detail);

public sealed class GroundingSafetyDecision
{
    internal GroundingSafetyDecision(IEnumerable<GroundingSafetyFinding> findings)
    {
        Findings = Array.AsReadOnly(findings.ToArray());
    }

    public bool IsAllowed => Findings.Count == 0;

    public IReadOnlyList<GroundingSafetyFinding> Findings { get; }
}
