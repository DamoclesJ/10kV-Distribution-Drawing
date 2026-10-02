using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Application.GroundingSafety;

public enum GroundingElectricalIdentityKind
{
    Terminal,
    Connection
}

public sealed record GroundingElectricalIdentity(
    GroundingElectricalIdentityKind Kind,
    Guid Id);

public sealed record GroundingTargetEnergizationResolution(
    GroundingElectricalIdentity? Identity,
    EnergizationState? State,
    string? FailureReason)
{
    public bool IsSuccess => Identity is not null && State is not null && FailureReason is null;

    internal static GroundingTargetEnergizationResolution Success(
        GroundingElectricalIdentity identity,
        EnergizationState state) => new(identity, state, null);

    internal static GroundingTargetEnergizationResolution Failure(string reason) =>
        new(null, null, reason);
}

public sealed class GroundingTargetEnergizationResolver
{
    public GroundingTargetEnergizationResolution Resolve(
        DrawingDocument drawing,
        GroundingTarget target,
        EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(target);
        if (result is null) return GroundingTargetEnergizationResolution.Failure("EA result is missing.");
        if (!result.IsSuccess)
            return GroundingTargetEnergizationResolution.Failure("EA result is not successful.");

        if (target.Kind == GroundingTargetKind.Terminal)
        {
            if (!drawing.Terminals.Any(terminal => terminal.Id == target.TargetId))
                return GroundingTargetEnergizationResolution.Failure(
                    $"Grounding terminal '{target.TargetId}' does not exist.");
            if (!TryGetState(result, target.TargetId, out EnergizationState state))
                return GroundingTargetEnergizationResolution.Failure(
                    $"EA has no conclusive result for terminal '{target.TargetId}'.");
            return GroundingTargetEnergizationResolution.Success(
                new GroundingElectricalIdentity(GroundingElectricalIdentityKind.Terminal, target.TargetId),
                state);
        }

        if (target.Kind == GroundingTargetKind.GroundingAccessPoint)
        {
            GroundingAccessPoint? point = drawing.GroundingAccessPoints.SingleOrDefault(candidate =>
                candidate.GroundingAccessPointId == target.TargetId);
            return point is null
                ? GroundingTargetEnergizationResolution.Failure(
                    $"Grounding access point '{target.TargetId}' does not exist.")
                : ResolveGroundingAccessPoint(drawing, point, result);
        }

        return GroundingTargetEnergizationResolution.Failure(
            $"Grounding target kind '{target.Kind}' is not supported.");
    }

    public GroundingTargetEnergizationResolution ResolveGroundingAccessPoint(
        DrawingDocument drawing,
        GroundingAccessPoint point,
        EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(point);
        if (result is null) return GroundingTargetEnergizationResolution.Failure("EA result is missing.");
        if (!result.IsSuccess)
            return GroundingTargetEnergizationResolution.Failure("EA result is not successful.");

        try
        {
            drawing.ValidateGroundingAccessPointIntegrity(point);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            return GroundingTargetEnergizationResolution.Failure(
                $"Grounding access point '{point.GroundingAccessPointId}' is invalid: {error.Message}");
        }

        var matchingEdges = result.ConductingEdges.Where(edge =>
            edge.Type == ElectricalConnectivityEdgeType.Connection &&
            edge.SourceId == point.ConnectionId).ToArray();
        if (matchingEdges.Length != 1)
            return GroundingTargetEnergizationResolution.Failure(
                $"EA does not contain one unique Connection result for '{point.ConnectionId}'.");

        Connection connection = drawing.Connections.Single(candidate => candidate.Id == point.ConnectionId);
        ElectricalConnectivityEdge edge = matchingEdges[0];
        if (!edge.Connects(connection.StartTerminalId, connection.EndTerminalId))
            return GroundingTargetEnergizationResolution.Failure(
                $"EA Connection edge '{point.ConnectionId}' does not match the drawing topology.");
        if (!TryGetState(result, connection.StartTerminalId, out EnergizationState startState) ||
            !TryGetState(result, connection.EndTerminalId, out EnergizationState endState))
        {
            return GroundingTargetEnergizationResolution.Failure(
                $"EA has no conclusive endpoint result for Connection '{point.ConnectionId}'.");
        }
        if (startState != endState)
            return GroundingTargetEnergizationResolution.Failure(
                $"EA endpoint states disagree across Connection '{point.ConnectionId}'.");

        return GroundingTargetEnergizationResolution.Success(
            new GroundingElectricalIdentity(GroundingElectricalIdentityKind.Connection, point.ConnectionId),
            startState);
    }

    private static bool TryGetState(
        EnergizationResult result,
        Guid terminalId,
        out EnergizationState state)
    {
        if (result.Terminals.TryGetValue(terminalId, out EnergizationPointResult? point) &&
            point.State is EnergizationState.Energized or EnergizationState.Deenergized)
        {
            state = point.State;
            return true;
        }
        state = default;
        return false;
    }
}
