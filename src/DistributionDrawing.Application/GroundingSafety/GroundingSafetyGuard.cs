using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Application.GroundingSafety;

public sealed class GroundingSafetyGuard
{
    private readonly GroundingTargetEnergizationResolver _resolver = new();

    public GroundingSafetyDecision Evaluate(
        DrawingDocument drawing,
        EnergizationResult result,
        ISwitchStateView switchStateView,
        IEnumerable<GroundingPoint>? additionalGroundingPoints = null,
        IEnumerable<GroundingAccessPoint>? prospectiveGroundingAccessPoints = null)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(switchStateView);
        var findings = new List<GroundingSafetyFinding>();
        if (result is null || !result.IsSuccess)
        {
            findings.Add(new GroundingSafetyFinding(
                GroundingSafetyFindingCode.AnalysisUnavailable,
                "EA",
                null,
                "A successful EA result is required for Grounding Safety."));
            return new GroundingSafetyDecision(findings);
        }

        Dictionary<Guid, GroundingAccessPoint> prospectiveGaps;
        try
        {
            prospectiveGaps = (prospectiveGroundingAccessPoints ?? [])
                .ToDictionary(point => point.GroundingAccessPointId);
        }
        catch (ArgumentException error)
        {
            findings.Add(new GroundingSafetyFinding(
                GroundingSafetyFindingCode.GroundingTargetUnresolved,
                "Candidate GAP",
                null,
                error.Message));
            return new GroundingSafetyDecision(findings);
        }

        IEnumerable<GroundingPoint> groundingPoints = drawing.GroundingPoints
            .Concat(additionalGroundingPoints ?? []);
        foreach (GroundingPoint point in groundingPoints)
        {
            GroundingTargetEnergizationResolution resolution;
            if (point.Target.Kind == GroundingTargetKind.GroundingAccessPoint &&
                !drawing.GroundingAccessPoints.Any(candidate =>
                    candidate.GroundingAccessPointId == point.Target.TargetId))
            {
                resolution = prospectiveGaps.TryGetValue(point.Target.TargetId,
                    out GroundingAccessPoint? prospective)
                    ? _resolver.ResolveGroundingAccessPoint(drawing, prospective, result)
                    : GroundingTargetEnergizationResolution.Failure(
                        $"Grounding access point '{point.Target.TargetId}' does not exist.");
            }
            else
            {
                resolution = _resolver.Resolve(drawing, point.Target, result);
            }

            string location = Describe(point);
            if (!resolution.IsSuccess)
            {
                findings.Add(new GroundingSafetyFinding(
                    GroundingSafetyFindingCode.GroundingTargetUnresolved,
                    location,
                    resolution.Identity,
                    resolution.FailureReason ?? "Grounding target could not be resolved."));
            }
            else if (resolution.State == EnergizationState.Energized)
            {
                findings.Add(new GroundingSafetyFinding(
                    GroundingSafetyFindingCode.EnergizedGroundedLocation,
                    location,
                    resolution.Identity,
                    "Grounded location is Energized in the candidate EA."));
            }
        }

        foreach (RingCabinet cabinet in drawing.Devices.OfType<RingCabinet>())
        {
            IReadOnlyList<EffectiveGroundingLocation> locations;
            try
            {
                locations = cabinet.GetEffectiveGroundingLocations(switchStateView);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            {
                findings.Add(new GroundingSafetyFinding(
                    GroundingSafetyFindingCode.EffectiveGroundingUnresolved,
                    cabinet.DisplayName ?? cabinet.Id.ToString(),
                    null,
                    error.Message));
                continue;
            }

            foreach (EffectiveGroundingLocation location in locations)
            {
                GroundingElectricalIdentity identity;
                EnergizationState state;
                if (location.CableTerminalId is Guid cableTerminalId)
                {
                    identity = new GroundingElectricalIdentity(
                        GroundingElectricalIdentityKind.Terminal, cableTerminalId);
                    if (!TryGetState(result.Terminals, cableTerminalId, out state))
                    {
                        findings.Add(new GroundingSafetyFinding(
                            GroundingSafetyFindingCode.EffectiveGroundingUnresolved,
                            Describe(cabinet, location),
                            identity,
                            "EA has no conclusive result for the CableSide terminal."));
                        continue;
                    }
                }
                else
                {
                    identity = new GroundingElectricalIdentity(
                        GroundingElectricalIdentityKind.ElectricalNode, location.CircuitNodeId);
                    if (!TryGetState(result.Nodes, location.CircuitNodeId, out state))
                    {
                        findings.Add(new GroundingSafetyFinding(
                            GroundingSafetyFindingCode.EffectiveGroundingUnresolved,
                            Describe(cabinet, location),
                            identity,
                            "EA has no conclusive result for the CableSide circuit node."));
                        continue;
                    }
                }

                if (state == EnergizationState.Energized)
                {
                    findings.Add(new GroundingSafetyFinding(
                        GroundingSafetyFindingCode.EnergizedGroundedLocation,
                        Describe(cabinet, location),
                        identity,
                        "Effective grounding location is Energized in the candidate EA."));
                }
            }
        }

        return new GroundingSafetyDecision(findings);
    }

    private static bool TryGetState(
        IReadOnlyDictionary<Guid, EnergizationPointResult> states,
        Guid id,
        out EnergizationState state)
    {
        if (states.TryGetValue(id, out EnergizationPointResult? point) &&
            point.State is EnergizationState.Energized or EnergizationState.Deenergized)
        {
            state = point.State;
            return true;
        }
        state = default;
        return false;
    }

    private static string Describe(GroundingPoint point) =>
        $"工作接地点 {point.Number ?? point.GroundingPointId.ToString()} · {point.Location}";

    private static string Describe(
        RingCabinet cabinet,
        EffectiveGroundingLocation location)
    {
        RingCabinetInterval interval = cabinet.Intervals.Single(item =>
            item.IntervalId == location.IntervalId);
        return $"{cabinet.DisplayName} · {interval.DisplayName} 电缆侧";
    }
}
