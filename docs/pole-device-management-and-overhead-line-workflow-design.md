# Pole Device Management and Overhead Line Workflow Design

## 1. Purpose

This document defines the implementation contract for managing devices installed on a pole and for creating overhead-line topology around empty poles, pole switches, cable terminations, straight-through lines, T junctions, and cross junctions.

The design addresses these confirmed workflow requirements:

- a pole may contain multiple installed devices;
- installed devices must be visible and removable from the Pole Inspector;
- pole switch devices support four 90-degree orientations;
- cable termination keeps its existing free rotation around the pole circumference;
- overhead lines can be drawn continuously by repeated left clicks;
- an empty pole supports through connections, T junctions, and cross junctions;
- a switch may be inserted into an existing unambiguous straight-through pole;
- after a switch is inserted, a new branch may be drawn from a chosen side of that switch;
- an existing T/cross topology must never be rewired by guessing which two lines belong on the switch sides.

This is a solution design only. It does not describe completed production behavior.

## 2. Existing Facts and Boundaries

The existing architecture remains authoritative:

- `PoleAttachment` expresses physical installation/ownership only; it does not imply electrical continuity.
- `Terminal` is the selectable electrical endpoint.
- `Connection + OverheadLine` is the overhead-line topology fact.
- `TerminalAnchorIndex` maps existing Domain terminals to transient drawing coordinates.
- `CommandStack` is the only Execute/Undo/Redo path.
- routes remain transient derived state and are not persisted.
- Domain IDs, Connection IDs, device IDs, terminal IDs, and attachment IDs remain stable.
- Rendering must not infer or create electrical relationships from visual proximity.

No second topology model, pole-local wiring model, or renderer-owned connectivity graph is introduced.

## 3. Pole Inspector: Installed Device Management

When a Pole is selected, the Inspector adds an `Installed Devices` section. Each row represents one `PoleAttachment` and resolves its attached Device.

Each row displays:

- device type;
- device/dispatch number when available;
- switch state when the attached device is a `SwitchDevice`;
- orientation;
- number of external connections using each device terminal;
- whether the device is currently connected or unconnected.

Each row provides explicit actions:

- select device;
- rotate left 90 degrees;
- rotate right 90 degrees;
- delete device.

Selecting a row must select the existing `PoleAttachment` or attached Device through the current Selection system. The Inspector must not create a parallel selection model.

### 3.1 Delete policy

Deletion is a structural command, not a property edit.

| Installed device state | Delete behavior |
|---|---|
| Unconnected pole switch | Remove SwitchDevice, its two Terminals, PoleAttachment, and AttachmentLayout atomically |
| Pole switch with one or two connected overhead lines | Offer an explicit Chinese confirmation to restore those line endpoints to the Pole junction terminal, then remove the switch |
| Pole switch with more than two connected overhead lines | Refuse automatic deletion; require the user to remove/reconnect branches first |
| Unconnected CableTermination | Use the existing CableTermination removal command |
| CableTermination referenced by Cable/OverheadLine | Refuse removal until the referenced line is removed or reconnected |

For a permitted connected-switch deletion, the operation must preserve every existing `ConnectionId` and `OverheadLine.ConnectionId`. It must not implement deletion as new line creation.

Execute, Undo, and Redo must restore the same device, terminal, attachment, connection, and layout IDs.

## 4. Attachment Orientation

### 4.1 Pole switches

Pole switch attachments use a discrete orientation:

- 0 degrees;
- 90 degrees;
- 180 degrees;
- 270 degrees.

The orientation applies consistently to:

- professional symbol geometry;
- first and second terminal positions;
- terminal directions;
- logical bounds;
- HitTest geometry;
- Selection overlay;
- routing obstacles;
- label anchors where the current renderer derives them from attachment geometry.

Rotation changes Layout only. It must not change Device ID, PoleAttachment ID, Terminal IDs, switch state, or electrical connections. Connected overhead lines are rebuilt from the rotated terminal anchors.

### 4.2 Cable termination

CableTermination keeps the current continuous orbit behavior around the pole circumference. Its triangle remains tangent to the pole and points outward. Its cable-side terminal remains at the outer apex.

CableTermination does not use the four-direction switch orientation contract.

### 4.3 Layout and persistence

`AttachmentLayout` should gain a quarter-turn orientation value for switch attachments. Persistence V6 should save this as an optional backward-compatible layout field with a default of zero for older files.

This does not require a Domain property or a format-version upgrade. Route data remains unpersisted.

## 5. Connection Ports Exposed by a Pole

A pole can expose two different categories of overhead-line endpoint:

### 5.1 Pole junction terminal

The existing Pole-owned overhead terminal is a multi-connection junction point. It supports:

- empty-pole through connection;
- T junction;
- cross junction;
- an independent branch that intentionally does not pass through an installed switch.

The Pole junction terminal remains available even when a switch is installed. It must not be hidden merely because the Pole has a SwitchDevice attachment.

### 5.2 Switch terminals

Every pole-installed SwitchDevice exposes its actual first and second terminal. These terminals represent the two electrical sides of the switch.

Pole switch terminals must support multiple overhead-line connections on the same side so that a branch can be intentionally connected upstream or downstream of the switch. This is still one real Terminal ID with multiple `Connection` references, not a new graphical junction object.

The Domain remains the final authority for terminal capacity and allowed connection type. Existing V6 projects must restore pole-switch terminal policy consistently with newly created pole switches.

### 5.3 Cable termination terminals

CableTermination keeps its existing split contract:

- CableSide accepts Cable only;
- OverheadSide accepts OverheadLine only.

Neither side is interchangeable with the Pole junction terminal.

## 6. Terminal Picking UX

During overhead-line creation, all legal nearby ports are temporarily highlighted:

- Pole junction terminal;
- each pole switch first/second terminal;
- CableTermination overhead-side terminal.

The user chooses the electrical fact by clicking the intended port. Screen coordinates are used only to pick a Terminal ID.

Selection priority must not allow an occupied or incompatible nearby terminal to hide another valid terminal. Candidate processing is:

1. collect anchors within tolerance;
2. resolve each Domain Terminal;
3. remove terminals that are incompatible or unavailable;
4. rank remaining candidates by distance and explicit endpoint priority;
5. if equally ranked anchors overlap, request explicit disambiguation instead of guessing.

The UI should display a small temporary endpoint marker only while a connection tool is active. Normal drawing output remains unchanged.

## 7. Continuous Overhead-Line Drawing

The overhead-line tool is a persistent click sequence:

```text
Activate tool
  -> click A
  -> click B: create A-B
  -> click C: create B-C
  -> click D: create C-D
  -> Esc/right-click cancel: finish
```

Each completed segment is one CommandStack command so each segment is independently undoable.

Continuation rules:

- empty Pole junction reached: continue from the same multi-connection Pole terminal;
- pole switch terminal reached: continue from the opposite switch terminal for a normal through sequence;
- the user may explicitly restart or choose a used switch-side terminal to create a branch on that side;
- CableTermination overhead terminal reached: finish unless another valid continuation has been explicitly defined;
- invalid click: keep the current start and preview; do not add command history.

The status area displays Chinese guidance and makes Esc cancellation discoverable.

## 8. Adding a Switch Before or After Lines

### 8.1 Device first, lines second

The user adds a switch to a Pole, then connects overhead lines directly to the switch's first and second Terminal IDs. The Pole junction remains available for an intentional bypass or independent branch.

### 8.2 Empty pole with zero or one line, then switch

The switch may be installed. With one existing line, the UI may offer an explicit insertion preview showing which switch side receives the existing line. No topology changes occur until the user confirms.

### 8.3 Straight-through pole with exactly two lines, then switch

This is the deterministic automatic-insertion case:

```text
A -- Pole junction -- B

becomes

A -- Switch terminal 1 [Switch] Switch terminal 2 -- B
```

The Desktop/Application command may use current geometry only to decide which existing line is nearer to terminal 1 or terminal 2. Geometry does not create topology; it only orders two already-known connection facts. The user should see the proposed mapping before confirmation when orientation or geometry makes the assignment ambiguous.

The insertion command must preserve:

- both Connection IDs;
- both OverheadLine IDs;
- existing line attributes;
- Pole ID;
- newly created SwitchDevice, Terminal, and PoleAttachment IDs across Undo/Redo.

The operation is atomic. Scene rebuild/routing failure rolls back both switch installation and endpoint migration.

### 8.4 Existing T or cross topology, then switch

When three or more lines already use the Pole junction, automatic switch insertion is forbidden because the software cannot know which lines are upstream, downstream, or bypass branches.

The user may still install the device physically as an unconnected attachment, but the software must not silently migrate existing line endpoints. The Inspector marks it as unconnected. The user can then explicitly reconnect lines to the intended switch terminals.

This distinguishes:

- `install device only` — allowed;
- `automatically insert device into existing T/cross topology` — rejected.

## 9. Branching From an Installed Switch

After a switch has been inserted into a straight-through line, the user may start a new overhead line from either switch side.

Example:

```text
A -- terminal 1 [Switch] terminal 2 -- B
                              |
                              +-- C
```

The branch uses the exact selected switch Terminal ID. It is therefore explicitly upstream or downstream according to the user's click and remains affected by the existing switch-state topology semantics.

The UI must not automatically move the branch to the Pole junction or the opposite switch side.

## 10. Multiple Installed Devices

Multiple `PoleAttachment` objects may coexist on one Pole, for example:

- one pole switch plus one CableTermination;
- multiple independently installed switch devices;
- other supported attachment types.

Installation order and layout do not imply electrical order. Each device remains electrically independent until its actual Terminals are used by Connections.

The Inspector must clearly show connected/unconnected state. The system must not automatically chain multiple switch devices based on visual order.

If a future workflow needs two switches electrically in series on one Pole, that relationship must be created by explicit terminal connection commands, not inferred from their offsets or rotations.

## 11. Commands

The minimum command set is:

- `AddPoleSwitchAttachmentCommand`
  - optionally includes a confirmed zero/one/two-line endpoint migration snapshot;
- `RemovePoleSwitchAttachmentCommand`
  - optionally includes a confirmed endpoint restoration snapshot;
- `RotatePoleAttachmentCommand`
  - stores before/after quarter-turn orientation;
- existing `AddOverheadLineCommand` and `RemoveOverheadLineCommand`;
- a focused overhead endpoint reconnect operation if explicit correction is required.

Composite commands store complete before/after endpoint snapshots. Undo must not infer the old topology by running a reverse heuristic.

Failed Execute must not:

- change Domain state;
- change Layout;
- rebuild a half-valid Scene;
- alter Selection;
- add CommandStack history.

## 12. Selection and Scene Behavior

After a successful operation:

- adding a device selects the new PoleAttachment;
- rotating keeps the same PoleAttachment selected;
- deleting selects the parent Pole;
- creating an overhead segment selects the new Connection while the drawing tool remains active;
- Scene is rebuilt from current Domain and Layout;
- connected lines reroute from current terminal anchors.

After failure, the previous Selection and Scene remain unchanged.

## 13. Persistence Boundary

Persistence continues to save facts, not derived routes:

- Pole, SwitchDevice, CableTermination;
- PoleAttachment relationships;
- Terminal IDs and terminal policy;
- Connection endpoint Terminal IDs;
- OverheadLine detail;
- Attachment offset and switch quarter-turn orientation.

Persistence does not save:

- orthogonal route points;
- preview state;
- active drawing tool;
- highlighted terminal handles;
- Inspector selection.

CurrentVersion should remain V6 if the optional orientation field can be restored with a zero default.

## 14. Required Tests

### 14.1 Pole Inspector

- lists every PoleAttachment exactly once;
- identifies attached device type and connection count;
- selects the correct Stable ID;
- removes an unconnected switch;
- connected removal confirmation/cancellation;
- removal Undo/Redo restores original IDs;
- connected CableTermination removal remains rejected.

### 14.2 Rotation

- 0/90/180/270 geometry;
- TerminalAnchor positions and directions rotate consistently;
- HitTest and Selection remain on the same IDs;
- connected line reroutes after rotation;
- Undo/Redo;
- Save/Open restores orientation;
- older V6 file defaults to zero orientation.

### 14.3 Continuous drawing

- A-B-C-D created without reactivating the tool;
- every segment is independently undoable;
- empty Pole supports T and cross connections;
- occupied/incompatible nearby terminal does not mask a valid terminal;
- invalid click preserves current start and history;
- route failure rolls back the segment.

### 14.4 Switch topology

- switch-first then connect both sides;
- one-line insertion with explicit side;
- two-line automatic insertion preserves Connection/OverheadLine IDs;
- insertion Undo/Redo restores exact before/after endpoints;
- T/cross automatic insertion is rejected atomically;
- T/cross allows physical installation without automatic rewiring;
- branch from switch terminal 1;
- branch from switch terminal 2;
- Pole junction remains separately selectable;
- multiple devices do not become electrically connected by installation order.

### 14.5 Persistence

- Save/Open preserves migrated endpoints and Stable IDs;
- Save/Open preserves branch endpoint side;
- Save/Open preserves multiple attachments and orientation;
- no route points are persisted.

## 15. Recommended Implementation Order

1. **Reconcile terminal exposure and capacity**
   - keep Pole junction anchor available;
   - make pole-switch side terminals support intentional branching;
   - add connection-mode endpoint highlighting and deterministic picking.
2. **Complete continuous overhead drawing**
   - persistent click chain;
   - transactional scene validation;
   - empty-pole T/cross tests.
3. **Implement straight-line switch insertion**
   - zero/one/two-line cases;
   - before/after endpoint snapshots;
   - Undo/Redo and failure atomicity.
4. **Add Pole Inspector installed-device management**
   - list, select, connection status, delete.
5. **Add four-direction switch rotation**
   - geometry, anchors, obstacles, HitTest, persistence.
6. **Windows validation**
   - continuous drawing;
   - exact endpoint selection;
   - switch insertion and branching;
   - multi-device management;
   - Save/Open and Undo/Redo.

## 16. Explicit Non-goals

This work does not include:

- automatic inference of upstream/downstream lines in an existing T/cross topology;
- topology creation based on visual proximity;
- automatic chaining of multiple devices on one Pole;
- a second routing system;
- route persistence;
- energized/red-state projection;
- DTU;
- global Desktop redesign.

