using System.Collections.Frozen;
using System.Collections.ObjectModel;
using DistributionDrawing.Application.Topology;

namespace DistributionDrawing.Application.Energization;

// Unknown is retained for source compatibility; successful analysis never emits it.
public enum EnergizationState { Energized, Deenergized, Unknown }

// ForwardOnly/Incomplete are legacy values and are no longer emitted by the analyzer.
public enum EnergizationValidity { NoSeeds, ForwardOnly, Complete, Incomplete, Failed }

public enum EnergizationDiagnosticCode
{
    None,
    SourceSetUnconfirmed,
    EmptyScenario,
    MissingBoundaryDevice,
    UnsupportedBoundary,
    InvalidSide,
    UnresolvedSide,
    MissingTerminal,
    InvalidTopology
}

public sealed record EnergizationDiagnostic(
    EnergizationDiagnosticCode Code,
    Guid? SeedId = null,
    string? Detail = null);

public sealed class EnergizationPointResult
{
    public EnergizationPointResult(EnergizationState state, IEnumerable<Guid> energizedBy)
    {
        State = state;
        EnergizedBy = energizedBy.ToFrozenSet();
    }

    public EnergizationState State { get; }
    public IReadOnlySet<Guid> EnergizedBy { get; }
}

public sealed record GroundingSwitchConnection(
    Guid SwitchDeviceId,
    Guid DeviceSideTerminalId,
    Guid EarthSideTerminalId);

public sealed class EnergizationResult
{
    public EnergizationResult(
        EnergizationValidity validity,
        IReadOnlyDictionary<Guid, EnergizationPointResult> terminals,
        IReadOnlyDictionary<Guid, EnergizationPointResult> nodes,
        IEnumerable<EnergizationDiagnostic> diagnostics,
        IEnumerable<ElectricalConnectivityEdge> conductingEdges,
        IEnumerable<GroundingSwitchConnection> groundingSwitchConnections)
    {
        Validity = validity;
        Terminals = new ReadOnlyDictionary<Guid, EnergizationPointResult>(
            new Dictionary<Guid, EnergizationPointResult>(terminals));
        Nodes = new ReadOnlyDictionary<Guid, EnergizationPointResult>(
            new Dictionary<Guid, EnergizationPointResult>(nodes));
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        ConductingEdges = Array.AsReadOnly(conductingEdges.ToArray());
        GroundingSwitchConnections = Array.AsReadOnly(groundingSwitchConnections.ToArray());
    }

    public EnergizationValidity Validity { get; }
    public bool IsSuccess => Validity == EnergizationValidity.Complete;
    public bool CanConcludeDeenergized => Validity == EnergizationValidity.Complete;
    public IReadOnlyDictionary<Guid, EnergizationPointResult> Terminals { get; }
    public IReadOnlyDictionary<Guid, EnergizationPointResult> Nodes { get; }
    public IReadOnlyList<EnergizationDiagnostic> Diagnostics { get; }
    public IReadOnlyList<ElectricalConnectivityEdge> ConductingEdges { get; }
    public IReadOnlyList<GroundingSwitchConnection> GroundingSwitchConnections { get; }
}
