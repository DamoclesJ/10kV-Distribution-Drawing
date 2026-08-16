# Phase D-2-A Cable Connection Creation

## Interaction state

The Desktop cable tool has four states: idle, picking the start terminal,
picking the end terminal, and waiting for cable parameters. Starting the tool
clears any unfinished connection state and replaces other drawing modes.

## Terminal picking

Picking uses the existing `TerminalAnchorIndex` and document coordinates. Only
external terminals that allow `ConnectionType.Cable` and still satisfy the
current connection-occupancy policy are candidates. The selected terminal IDs,
not screen coordinates, become the cable topology facts.

CableTermination exposes two anchors, but only its CableSide terminal is a
candidate for a cable. Its OverheadSide terminal remains reserved for
OverheadLine connections. Existing two-sided IntermediateTerminal positions can
be resolved from their two current cable connections; Joint creation remains
outside this phase.

## Command chain

After both terminals are picked, a small Chinese parameter dialog collects
CableType and Length. The Desktop adapter calls the existing
`CableSegmentCreationFactory`, then executes an `AddCableSegmentCommand`
through the existing `CommandStack`. `DrawingDocument.AddCableSegment` performs
the single authoritative registration of both the `CableSegment` and its
`Connection`; no second connection registration is performed.

## Scene and selection

Successful execution rebuilds the scene and selects the new CableSegment by its
stable ID. Cable geometry and labels continue to be produced by the existing
`DrawingSceneBuilder` and `CableRenderer` path. Undo removes the segment and its
connection; redo restores the same command-owned IDs.

## Persistence boundary

This phase does not add a CableLayout persistence format. Cable topology facts,
terminal IDs, CableType, and Length remain Domain/Persistence data. Current
scene geometry is reconstructed from the current terminal anchors. Any future
editable cable path persistence is a separate concern.

## Windows validation checklist

- Create a RingCabinet, Pole, and CableTermination.
- Start “绘制电缆”.
- Pick the RingCabinet cable terminal and CableTermination CableSide.
- Enter type and length; confirm CableSegment and label appear.
- Verify CableSegment selection and Inspector data.
- Undo and redo; save and reopen; verify terminal IDs and connection topology.
- Confirm the CableTermination OverheadSide cannot be selected as a cable end.
