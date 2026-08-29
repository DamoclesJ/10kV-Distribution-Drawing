using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Clipboard;

internal sealed record MaterializedPaste(
    ICommand Command,
    IReadOnlyDictionary<Guid, Guid> IdMap,
    IReadOnlyList<SelectionReference> Selection,
    SelectionReference? PrimarySelection);

internal sealed class ClipboardFragmentMaterializer
{
    public MaterializedPaste Materialize(
        ClipboardDrawingFragment fragment,
        ProjectRuntimeSession target,
        DocumentPoint offset)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(target);

        var idMap = BuildIdMap(fragment);
        Guid Map(Guid id) => idMap[id];
        Guid? MapOptional(Guid? id) => id is Guid value ? Map(value) : null;
        var commands = new List<ICommand>();
        var document = target.PersistenceSession.Domain;
        var layout = target.Layout;

        foreach (PoleSnapshot snapshot in fragment.Poles)
        {
            var pole = new Pole(
                Map(snapshot.Id),
                snapshot.PoleNumber,
                snapshot.DisplayName,
                snapshot.PoleType,
                snapshot.OverheadAnchorTerminalIds.Select(Map));
            ElectricalNode[] nodes = snapshot.Nodes.Select(item => new ElectricalNode(
                Map(item.Id),
                item.Type,
                item.OwnerType,
                Map(item.OwnerId),
                item.ElectricalState)).ToArray();
            Terminal[] terminals = snapshot.Terminals.Select(item => CreateTerminal(item, Map)).ToArray();
            commands.Add(new AddCopiedPoleCommand(
                document,
                layout,
                pole,
                nodes,
                terminals,
                new PoleLayout(
                    Map(snapshot.Id),
                    Add(snapshot.Layout.Position, offset),
                    snapshot.Layout.WidthMillimeters,
                    snapshot.Layout.HeightMillimeters,
                    snapshot.Layout.LabelOffset)));
        }

        foreach (RingCabinetSnapshot snapshot in fragment.RingCabinets)
        {
            RingCabinet cabinet = RingCabinet.Restore(Remap(snapshot.Definition, Map));
            commands.Add(new AddRingCabinetCommand(
                document,
                layout,
                cabinet,
                Remap(snapshot.Layout, Map, offset)));
        }

        foreach (PoleSwitchAttachmentSnapshot snapshot in fragment.PoleSwitches)
        {
            Guid deviceId = Map(snapshot.DeviceId);
            var device = SwitchDevice.CreateForPole(
                deviceId,
                snapshot.SwitchKind,
                Map(snapshot.FirstTerminal.Id),
                Map(snapshot.SecondTerminal.Id),
                snapshot.SwitchState,
                snapshot.DisplayName,
                snapshot.VoltageLevel,
                snapshot.DispatchNumber);
            Terminal first = CreateTerminal(snapshot.FirstTerminal, Map);
            Terminal second = CreateTerminal(snapshot.SecondTerminal, Map);
            var attachment = new PoleAttachment(
                Map(snapshot.AttachmentId),
                Map(snapshot.PoleId),
                deviceId);
            var creation = new PoleSwitchAttachmentCreation(
                device,
                first,
                second,
                attachment,
                Remap(snapshot.Layout, Map));
            commands.Add(new AddPoleSwitchAttachmentCommand(document, layout, creation));
        }

        foreach (CableTerminationAttachmentSnapshot snapshot in fragment.CableTerminations)
        {
            Guid deviceId = Map(snapshot.DeviceId);
            Guid internalNodeId = Map(snapshot.InternalNodeId);
            var device = new CableTermination(
                deviceId,
                Map(snapshot.CableSideTerminal.Id),
                Map(snapshot.OverheadSideTerminal.Id),
                internalNodeId,
                snapshot.DisplayName,
                snapshot.VoltageLevel);
            var internalNode = new ElectricalNode(
                internalNodeId,
                snapshot.InternalNode.Type,
                snapshot.InternalNode.OwnerType,
                deviceId,
                snapshot.InternalNode.ElectricalState);
            var creation = new CableTerminationAttachmentCreation(
                device,
                internalNode,
                CreateTerminal(snapshot.CableSideTerminal, Map),
                CreateTerminal(snapshot.OverheadSideTerminal, Map),
                new PoleAttachment(
                    Map(snapshot.AttachmentId),
                    Map(snapshot.PoleId),
                    deviceId),
                Remap(snapshot.Layout, Map));
            commands.Add(new AddCableTerminationAttachmentCommand(document, layout, creation));
        }

        foreach (CableSegmentSnapshot snapshot in fragment.CableSegments)
        {
            Connection connection = Remap(snapshot.Connection, Map);
            var segment = new CableSegment(
                Map(snapshot.CableSegment.Id),
                snapshot.CableSegment.Name,
                snapshot.CableSegment.CableType,
                snapshot.CableSegment.Length,
                snapshot.CableSegment.VoltageLevel,
                connection.Id,
                connection.StartTerminalId,
                connection.EndTerminalId);
            CableRouteGuide? guide = snapshot.RouteGuide is null
                ? null
                : new CableRouteGuide(
                    segment.Id,
                    snapshot.RouteGuide.HorizontalYMillimeters + offset.YMillimeters);
            commands.Add(new AddCopiedCableSegmentCommand(
                document,
                layout,
                connection,
                segment,
                guide));
        }

        foreach (OverheadLineSnapshot snapshot in fragment.OverheadLines)
        {
            Connection connection = Remap(snapshot.Connection, Map);
            var line = new OverheadLine(
                connection.Id,
                snapshot.OverheadLine.LineModel,
                snapshot.OverheadLine.SupportPoleIds.Select(Map),
                snapshot.OverheadLine.IsContinued,
                MapOptional(snapshot.OverheadLine.ContinuationTerminalId),
                snapshot.OverheadLine.ContinuationState,
                snapshot.OverheadLine.ContinuationDescription,
                snapshot.OverheadLine.LengthMeters);
            commands.Add(new AddOverheadLineCommand(
                document,
                layout,
                connection,
                line,
                new OverheadLineLayout(
                    connection.Id,
                    Add(snapshot.Layout.Start, offset),
                    Add(snapshot.Layout.End, offset),
                    snapshot.Layout.IsContinued,
                    snapshot.Layout.ContinuationOffset)));
        }

        SelectionReference[] mappedSelection = fragment.RootSelections
            .Where(item => idMap.ContainsKey(item.ObjectId))
            .Select(item => Remap(item, idMap))
            .GroupBy(item => (item.Kind, item.ObjectId))
            .Select(group => group.First())
            .ToArray();
        SelectionReference? primary = fragment.PrimarySelection is not null &&
                                      idMap.ContainsKey(fragment.PrimarySelection.ObjectId)
            ? Remap(fragment.PrimarySelection, idMap)
            : mappedSelection.LastOrDefault();
        var command = new PasteSelectionCommand(
            commands,
            target.SelectionManager,
            mappedSelection,
            primary);
        return new MaterializedPaste(command, idMap, mappedSelection, primary);
    }

    private static Dictionary<Guid, Guid> BuildIdMap(ClipboardDrawingFragment fragment)
    {
        var ids = new HashSet<Guid>();
        foreach (PoleSnapshot item in fragment.Poles)
        {
            ids.Add(item.Id);
            ids.UnionWith(item.OverheadAnchorTerminalIds);
            ids.UnionWith(item.Nodes.Select(node => node.Id));
        }
        foreach (RingCabinetSnapshot item in fragment.RingCabinets)
        {
            AddRingCabinetIds(item.Definition, ids);
        }
        foreach (PoleSwitchAttachmentSnapshot item in fragment.PoleSwitches)
        {
            ids.UnionWith(new[]
            {
                item.AttachmentId, item.PoleId, item.DeviceId,
                item.FirstTerminal.Id, item.SecondTerminal.Id
            });
            if (item.SecondTerminal.ElectricalNodeId is Guid nodeId) ids.Add(nodeId);
        }
        foreach (CableTerminationAttachmentSnapshot item in fragment.CableTerminations)
        {
            ids.UnionWith(new[]
            {
                item.AttachmentId, item.PoleId, item.DeviceId, item.InternalNodeId,
                item.CableSideTerminal.Id, item.OverheadSideTerminal.Id
            });
        }
        foreach (OverheadLineSnapshot item in fragment.OverheadLines)
        {
            ids.Add(item.Connection.Id);
            ids.Add(item.Connection.StartTerminalId);
            ids.Add(item.Connection.EndTerminalId);
            ids.UnionWith(item.OverheadLine.SupportPoleIds);
            if (item.OverheadLine.ContinuationTerminalId is Guid terminalId) ids.Add(terminalId);
        }
        foreach (CableSegmentSnapshot item in fragment.CableSegments)
        {
            ids.UnionWith(new[]
            {
                item.Connection.Id, item.Connection.StartTerminalId,
                item.Connection.EndTerminalId, item.CableSegment.Id
            });
        }

        return ids.ToDictionary(id => id, _ => Guid.NewGuid());
    }

    private static void AddRingCabinetIds(
        RingCabinetRestoreDefinition definition,
        ISet<Guid> ids)
    {
        ids.Add(definition.CabinetId);
        ids.Add(definition.MainBusNodeId);
        foreach (RingCabinetIntervalRestoreDefinition interval in definition.Intervals)
        {
            ids.UnionWith(new[]
            {
                interval.IntervalId, interval.ParentCabinetId, interval.CircuitNodeId,
                interval.EarthNodeId, interval.ExternalTerminalId, interval.SwitchAssemblyId
            });
            if (interval.IntermediateNodeId is Guid intermediateNodeId) ids.Add(intermediateNodeId);
            foreach (SwitchDeviceRestoreDefinition device in interval.Switches)
            {
                ids.UnionWith(new[]
                {
                    device.Id, device.FirstTerminalId, device.SecondTerminalId
                });
            }
        }
    }

    private static Terminal CreateTerminal(TerminalSnapshot item, Func<Guid, Guid> map) => new(
        map(item.Id),
        item.OwnerType,
        map(item.OwnerId),
        item.Role,
        item.VoltageLevel,
        item.IsExternal,
        item.AllowsMultipleConnections,
        item.ElectricalNodeId is Guid nodeId ? map(nodeId) : null,
        item.AllowedConnectionTypes);

    private static Connection Remap(Connection item, Func<Guid, Guid> map) => new(
        map(item.Id),
        item.Type,
        map(item.StartTerminalId),
        map(item.EndTerminalId),
        item.DisplayName,
        item.VoltageLevel);

    private static RingCabinetRestoreDefinition Remap(
        RingCabinetRestoreDefinition value,
        Func<Guid, Guid> map) => new(
        map(value.CabinetId),
        value.DisplayName,
        map(value.MainBusNodeId),
        value.Intervals.Select(interval => new RingCabinetIntervalRestoreDefinition(
            map(interval.IntervalId),
            map(interval.ParentCabinetId),
            interval.Sequence,
            interval.BayIndex,
            interval.DisplayName,
            interval.IntervalKind,
            interval.GroundingStructureKind,
            interval.IntermediateNodeId is Guid nodeId ? map(nodeId) : null,
            map(interval.CircuitNodeId),
            map(interval.EarthNodeId),
            map(interval.ExternalTerminalId),
            map(interval.SwitchAssemblyId),
            interval.Switches.Select(item => new SwitchDeviceRestoreDefinition(
                map(item.Id),
                item.SwitchKind,
                item.InstallationType,
                map(item.FirstTerminalId),
                map(item.SecondTerminalId),
                item.SwitchState,
                item.DisplayName,
                item.VoltageLevel,
                item.DispatchNumber)).ToArray())).ToArray(),
        value.LineName);

    private static RingCabinetLayout Remap(
        RingCabinetLayout value,
        Func<Guid, Guid> map,
        DocumentPoint offset) => new(
        map(value.CabinetId),
        Add(value.Position, offset),
        value.WidthMillimeters,
        value.HeightMillimeters,
        value.MainBusYMillimeters,
        value.IntervalLayouts.Values.Select(interval => new RingCabinetIntervalLayout(
            map(interval.IntervalId),
            interval.RelativePosition,
            interval.WidthMillimeters,
            interval.HeightMillimeters,
            interval.SequenceLabelOffset,
            interval.NameLabelOffset,
            interval.SwitchLayouts.Values.Select(item => new RingCabinetSwitchLayout(
                map(item.SwitchDeviceId),
                item.RelativePosition,
                item.WidthMillimeters,
                item.HeightMillimeters,
                item.LabelOffset)),
            interval.PTSymbolPosition)),
        value.LabelOffset);

    private static AttachmentLayout Remap(
        AttachmentLayout value,
        Func<Guid, Guid> map) => new(
        map(value.AttachmentId),
        value.Offset,
        value.WidthMillimeters,
        value.HeightMillimeters,
        value.LabelOffset,
        value.RotationQuarterTurns);

    private static SelectionReference Remap(
        SelectionReference value,
        IReadOnlyDictionary<Guid, Guid> map) => new(
        value.Kind,
        map[value.ObjectId],
        value.ParentId is Guid parentId && map.TryGetValue(parentId, out Guid mappedParent)
            ? mappedParent
            : null);

    private static DocumentPoint Add(DocumentPoint point, DocumentPoint offset) => new(
        point.XMillimeters + offset.XMillimeters,
        point.YMillimeters + offset.YMillimeters);
}
