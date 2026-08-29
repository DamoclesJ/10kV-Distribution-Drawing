using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
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

internal sealed record OverheadLineSnapshot(
    Connection Connection,
    OverheadLine OverheadLine,
    OverheadLineLayout Layout);

internal sealed record CableSegmentSnapshot(
    Connection Connection,
    CableSegment CableSegment,
    CableRouteGuide? RouteGuide);

internal sealed class ClipboardDrawingFragment
{
    public ClipboardDrawingFragment(
        SelectionReference? primarySelection,
        IEnumerable<SelectionReference> rootSelections,
        IEnumerable<PoleSnapshot> poles,
        IEnumerable<PoleSwitchAttachmentSnapshot> poleSwitches,
        IEnumerable<CableTerminationAttachmentSnapshot> cableTerminations,
        IEnumerable<RingCabinetSnapshot> ringCabinets,
        IEnumerable<OverheadLineSnapshot> overheadLines,
        IEnumerable<CableSegmentSnapshot> cableSegments)
    {
        PrimarySelection = primarySelection;
        RootSelections = Array.AsReadOnly(rootSelections.ToArray());
        Poles = Array.AsReadOnly(poles.ToArray());
        PoleSwitches = Array.AsReadOnly(poleSwitches.ToArray());
        CableTerminations = Array.AsReadOnly(cableTerminations.ToArray());
        RingCabinets = Array.AsReadOnly(ringCabinets.ToArray());
        OverheadLines = Array.AsReadOnly(overheadLines.ToArray());
        CableSegments = Array.AsReadOnly(cableSegments.ToArray());
    }

    public SelectionReference? PrimarySelection { get; }

    public IReadOnlyList<SelectionReference> RootSelections { get; }

    public IReadOnlyList<PoleSnapshot> Poles { get; }

    public IReadOnlyList<PoleSwitchAttachmentSnapshot> PoleSwitches { get; }

    public IReadOnlyList<CableTerminationAttachmentSnapshot> CableTerminations { get; }

    public IReadOnlyList<RingCabinetSnapshot> RingCabinets { get; }

    public IReadOnlyList<OverheadLineSnapshot> OverheadLines { get; }

    public IReadOnlyList<CableSegmentSnapshot> CableSegments { get; }

    public bool IsEmpty => Poles.Count == 0 && RingCabinets.Count == 0;
}

internal sealed record CopyPlanResult(
    ClipboardDrawingFragment? Fragment,
    IReadOnlyList<string> Warnings)
{
    public bool IsSuccess => Fragment is not null && !Fragment.IsEmpty;
}
