using DistributionDrawing.Application.Energization;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.GroundingSafety;

public sealed record GroundingSafetyAnalysisPreparation(
    EnergizationResult CandidateResult,
    GroundingSafetyDecision SafetyDecision);

public sealed class GroundingSafetyAnalysisSubmission
{
    public GroundingSafetyAnalysisPreparation Prepare(
        DrawingDocument drawing,
        EnergizationScenario candidateScenario)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(candidateScenario);
        CandidateElectricalState candidate = CandidateElectricalState.Create(
            drawing,
            switchOverrides: null,
            candidateScenario);
        EnergizationResult result = new EnergizationAnalyzer().Analyze(
            drawing,
            candidate.CreateScenarioSnapshot(),
            candidate);
        GroundingSafetyDecision decision = result.Validity == EnergizationValidity.NoSeeds
            ? new GroundingSafetyDecision([])
            : new GroundingSafetyGuard().Evaluate(drawing, result, candidate);
        return new GroundingSafetyAnalysisPreparation(result, decision);
    }
}
