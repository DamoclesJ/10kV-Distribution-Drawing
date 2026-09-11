using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Desktop.Clipboard;

internal sealed record ElectricalNodeSnapshot(
    Guid Id,
    ElectricalNodeType Type,
    TopologyOwnerType OwnerType,
    Guid OwnerId,
    ElectricalState? ElectricalState);

internal sealed record TerminalSnapshot(
    Guid Id,
    TopologyOwnerType OwnerType,
    Guid OwnerId,
    string Role,
    string? VoltageLevel,
    bool IsExternal,
    bool AllowsMultipleConnections,
    Guid? ElectricalNodeId,
    IReadOnlyList<ConnectionType> AllowedConnectionTypes);

internal sealed record PoleSnapshot(
    Guid Id,
    string PoleNumber,
    string? DisplayName,
    PoleType PoleType,
    IReadOnlyList<Guid> OverheadAnchorTerminalIds,
    IReadOnlyList<ElectricalNodeSnapshot> Nodes,
    IReadOnlyList<TerminalSnapshot> Terminals,
    PoleLayout Layout);

internal sealed record PoleSwitchAttachmentSnapshot(
    Guid AttachmentId,
    Guid PoleId,
    Guid DeviceId,
    SwitchKind SwitchKind,
    SwitchState SwitchState,
    string DisplayName,
    string VoltageLevel,
    string? DispatchNumber,
    TerminalSnapshot FirstTerminal,
    TerminalSnapshot SecondTerminal,
    ElectricalState? ControlledNodeState,
    AttachmentLayout Layout);

internal sealed record CableTerminationAttachmentSnapshot(
    Guid AttachmentId,
    Guid PoleId,
    Guid DeviceId,
    string? DisplayName,
    string VoltageLevel,
    Guid InternalNodeId,
    ElectricalNodeSnapshot InternalNode,
    TerminalSnapshot CableSideTerminal,
    TerminalSnapshot OverheadSideTerminal,
    AttachmentLayout Layout);

internal sealed record RingCabinetSnapshot(
    RingCabinetRestoreDefinition Definition,
    RingCabinetLayout Layout);

internal sealed record TransformerSnapshot(
    Guid Id,
    TransformerKind TransformerKind,
    TerminalSnapshot HvTerminal,
    TransformerLayout Layout);

internal sealed record IncomingFeederSnapshot(
    Guid IncomingFeederId,
    int Sequence,
    string DisplayName,
    Guid CableTerminalId,
    Guid StationTerminalId,
    Guid ElectricalNodeId,
    Guid IsolationSwitchId,
    SwitchState SwitchState,
    string SwitchDisplayName,
    string SwitchVoltageLevel,
    string? DispatchNumber,
    TerminalSnapshot CableTerminal,
    TerminalSnapshot StationTerminal,
    ElectricalNodeSnapshot ElectricalNode);

internal sealed record CustomerStationSnapshot(
    Guid CustomerStationId,
    StationKind StationKind,
    IReadOnlyList<IncomingFeederSnapshot> IncomingFeeders,
    CustomerStationLayout Layout);

internal sealed record OverheadLineSnapshot(
    Connection Connection,
    OverheadLine OverheadLine,
    OverheadLineLayout Layout);

internal sealed record CableSegmentSnapshot(
    Connection Connection,
    CableSegment CableSegment,
    CableRouteGuide? RouteGuide);

internal sealed record GroundingAccessPointSnapshot(
    Guid GroundingAccessPointId,
    Guid ConnectionId,
    Guid PoleId,
    GroundingAdjacentEndpoint AdjacentEndpoint,
    GroundingAccessLineSide LineSide);

internal sealed class ClipboardDrawingFragment
{
    public ClipboardDrawingFragment(
        SelectionReference? primarySelection,
        IEnumerable<SelectionReference> rootSelections,
        IEnumerable<PoleSnapshot> poles,
        IEnumerable<PoleSwitchAttachmentSnapshot> poleSwitches,
        IEnumerable<CableTerminationAttachmentSnapshot> cableTerminations,
        IEnumerable<RingCabinetSnapshot> ringCabinets,
        IEnumerable<TransformerSnapshot> transformers,
        IEnumerable<CustomerStationSnapshot> customerStations,
        IEnumerable<OverheadLineSnapshot> overheadLines,
        IEnumerable<CableSegmentSnapshot> cableSegments,
        IEnumerable<GroundingAccessPointSnapshot> groundingAccessPoints)
    {
        PrimarySelection = primarySelection;
        RootSelections = Array.AsReadOnly(rootSelections.ToArray());
        Poles = Array.AsReadOnly(poles.ToArray());
        PoleSwitches = Array.AsReadOnly(poleSwitches.ToArray());
        CableTerminations = Array.AsReadOnly(cableTerminations.ToArray());
        RingCabinets = Array.AsReadOnly(ringCabinets.ToArray());
        Transformers = Array.AsReadOnly(transformers.ToArray());
        CustomerStations = Array.AsReadOnly(customerStations.ToArray());
        OverheadLines = Array.AsReadOnly(overheadLines.ToArray());
        CableSegments = Array.AsReadOnly(cableSegments.ToArray());
        GroundingAccessPoints = Array.AsReadOnly(groundingAccessPoints.ToArray());
    }

    public SelectionReference? PrimarySelection { get; }

    public IReadOnlyList<SelectionReference> RootSelections { get; }

    public IReadOnlyList<PoleSnapshot> Poles { get; }

    public IReadOnlyList<PoleSwitchAttachmentSnapshot> PoleSwitches { get; }

    public IReadOnlyList<CableTerminationAttachmentSnapshot> CableTerminations { get; }

    public IReadOnlyList<RingCabinetSnapshot> RingCabinets { get; }

    public IReadOnlyList<TransformerSnapshot> Transformers { get; }

    public IReadOnlyList<CustomerStationSnapshot> CustomerStations { get; }

    public IReadOnlyList<OverheadLineSnapshot> OverheadLines { get; }

    public IReadOnlyList<CableSegmentSnapshot> CableSegments { get; }

    public IReadOnlyList<GroundingAccessPointSnapshot> GroundingAccessPoints { get; }

    public bool IsEmpty => Poles.Count == 0 && RingCabinets.Count == 0 &&
        Transformers.Count == 0 && CustomerStations.Count == 0;
}

internal sealed record CopyPlanResult(
    ClipboardDrawingFragment? Fragment,
    IReadOnlyList<string> Warnings)
{
    public bool IsSuccess => Fragment is not null && !Fragment.IsEmpty;
}
