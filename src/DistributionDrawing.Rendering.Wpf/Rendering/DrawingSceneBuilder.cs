using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class DrawingSceneBuilder
{
    private readonly SymbolLibrary _symbolLibrary;
    private readonly MixedPoleRenderer _mixedPoleRenderer;
    private readonly RingCabinetRenderer _ringCabinetRenderer;
    private readonly CableRenderer _cableRenderer;
    private readonly JointRenderer _jointRenderer;
    private readonly ProfessionalSceneBuilder _professionalSceneBuilder;

    public DrawingSceneBuilder(SymbolLibrary? symbolLibrary = null)
    {
        _symbolLibrary = symbolLibrary ?? new SymbolLibrary();
        _ringCabinetRenderer = new RingCabinetRenderer(_symbolLibrary);
        _mixedPoleRenderer = new MixedPoleRenderer(_symbolLibrary);
        _cableRenderer = new CableRenderer(_symbolLibrary);
        _jointRenderer = new JointRenderer(_symbolLibrary);
        _professionalSceneBuilder = new ProfessionalSceneBuilder(_symbolLibrary);
    }

    public DrawingScene Build(
        RingCabinet cabinet,
        RingCabinetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);

        var hitTestEntries = new List<SelectionHitTestEntry>
        {
            new(
                new SelectionReference(SelectionTargetKind.RingCabinet, cabinet.Id),
                new DocumentRect(
                    layout.Position.XMillimeters,
                    layout.Position.YMillimeters,
                    layout.WidthMillimeters,
                    layout.HeightMillimeters),
                10)
        };

        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            if (!layout.IntervalLayouts.TryGetValue(
                    interval.IntervalId,
                    out RingCabinetIntervalLayout intervalLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for interval '{interval.IntervalId}'.");
            }

            DocumentPoint intervalOrigin = new(
                layout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
                layout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(
                        SelectionTargetKind.RingCabinetInterval,
                        interval.IntervalId,
                        cabinet.Id),
                    new DocumentRect(
                        intervalOrigin.XMillimeters,
                        intervalOrigin.YMillimeters,
                        intervalLayout.WidthMillimeters,
                        intervalLayout.HeightMillimeters),
                    20));

            foreach (RingCabinetSwitchLayout switchLayout in intervalLayout.SwitchLayouts.Values)
            {
                hitTestEntries.Add(
                    new SelectionHitTestEntry(
                        new SelectionReference(
                            SelectionTargetKind.Device,
                            switchLayout.SwitchDeviceId,
                            interval.IntervalId),
                        new DocumentRect(
                            intervalOrigin.XMillimeters + switchLayout.RelativePosition.XMillimeters,
                            intervalOrigin.YMillimeters + switchLayout.RelativePosition.YMillimeters,
                            switchLayout.WidthMillimeters,
                            switchLayout.HeightMillimeters),
                        40));
            }
        }

        return new DrawingScene(
            _ringCabinetRenderer.Render(cabinet, layout),
            new SelectionHitTestIndex(hitTestEntries));
    }

    public DrawingScene Build(
        DrawingDocument document,
        RuntimeLayoutDocument layout)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);

        TerminalAnchorIndex terminalAnchors = TerminalAnchorIndex.Build(
            document,
            layout.DrawingLayout,
            layout.RingCabinetLayouts,
            document.Connections,
            document.CableSegments);
        DrawingScene baseScene = BuildCore(
            layout.DrawingLayout,
            document.Devices.OfType<Pole>(),
            document.PoleAttachments,
            document.Devices,
            document.CableSegments,
            document.IntermediateTerminals,
            document.Connections,
            document.OverheadLines,
            terminalAnchors);

        var elements = baseScene.Elements.ToList();
        var hitTestEntries = baseScene.HitTestIndex.Entries.ToList();
        foreach (RingCabinet cabinet in document.Devices.OfType<RingCabinet>())
        {
            if (!layout.RingCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? cabinetLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for ring cabinet '{cabinet.Id}'.");
            }

            DrawingScene cabinetScene = Build(cabinet, cabinetLayout);
            elements.AddRange(cabinetScene.Elements);
            hitTestEntries.AddRange(cabinetScene.HitTestIndex.Entries);
        }

        ProfessionalSceneResult professionalScene = _professionalSceneBuilder.Build(
            document,
            layout.DrawingLayout,
            layout.RingCabinetLayouts);
        elements.AddRange(professionalScene.Elements);
        hitTestEntries.AddRange(professionalScene.HitTestEntries);

        return new DrawingScene(elements, new SelectionHitTestIndex(hitTestEntries));
    }

    public DrawingScene Build(
        DrawingLayout layout,
        IEnumerable<Pole> poles,
        IEnumerable<PoleAttachment> attachments,
        IEnumerable<Device> devices,
        IEnumerable<OverheadLine> overheadLines)
    {
        return BuildCore(
            layout,
            poles,
            attachments,
            devices,
            cableSegments: null,
            intermediateTerminals: null,
            connections: null,
            overheadLines: overheadLines,
            terminalAnchors: null);
    }

    public DrawingScene Build(
        DrawingLayout layout,
        IEnumerable<Pole> poles,
        IEnumerable<PoleAttachment> attachments,
        IEnumerable<Device> devices,
        IEnumerable<Connection> connections,
        IEnumerable<OverheadLine> overheadLines)
    {
        ArgumentNullException.ThrowIfNull(connections);

        return BuildCore(
            layout,
            poles,
            attachments,
            devices,
            cableSegments: null,
            intermediateTerminals: null,
            connections,
            overheadLines,
            terminalAnchors: null);
    }

    private DrawingScene BuildCore(
        DrawingLayout layout,
        IEnumerable<Pole> poles,
        IEnumerable<PoleAttachment> attachments,
        IEnumerable<Device> devices,
        IEnumerable<CableSegment>? cableSegments,
        IEnumerable<IntermediateTerminal>? intermediateTerminals,
        IEnumerable<Connection>? connections,
        IEnumerable<OverheadLine> overheadLines,
        TerminalAnchorIndex? terminalAnchors)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(poles);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(overheadLines);

        var elements = new List<SceneElement>();
        var hitTestEntries = new List<SelectionHitTestEntry>();
        var poleById = poles.ToDictionary(pole => pole.Id);
        PoleAttachment[] poleAttachments = attachments.ToArray();
        var deviceById = devices.ToDictionary(device => device.Id);
        CableSegment[] cableSegmentsArray = cableSegments?.ToArray() ?? [];
        IntermediateTerminal[] intermediateTerminalsArray =
            intermediateTerminals?.ToArray() ?? [];
        HashSet<Guid> cableConnectionIds = cableSegmentsArray
            .Select(cableSegment => cableSegment.ConnectionId)
            .ToHashSet();
        var connectionById = connections?.ToDictionary(connection => connection.Id);
        Dictionary<Guid, DocumentPoint> terminalPositions = terminalAnchors?.Anchors
            .ToDictionary(anchor => anchor.TerminalId, anchor => anchor.Position) ?? [];
        var jointInputs = new List<(IntermediateTerminal IntermediateTerminal, JointLayout Layout)>();

        foreach (IntermediateTerminal intermediateTerminal in intermediateTerminalsArray)
        {
            if (connectionById is null || terminalAnchors is null)
            {
                throw new InvalidOperationException(
                    "Joint rendering requires Connections and terminal anchors.");
            }

            Connection[] jointConnections = connectionById.Values
                .Where(connection => connection.Type == ConnectionType.Cable &&
                                     connection.UsesTerminal(intermediateTerminal.TerminalId))
                .ToArray();
            if (jointConnections.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Intermediate terminal '{intermediateTerminal.Id}' must connect exactly two cable connections.");
            }

            if (jointConnections.Any(connection => !cableConnectionIds.Contains(connection.Id)))
            {
                throw new InvalidOperationException(
                    $"Intermediate terminal '{intermediateTerminal.Id}' is not connected by two current cable segments.");
            }

            DocumentPoint[] outerPositions = jointConnections
                .Select(connection =>
                {
                    Guid outerTerminalId = connection.StartTerminalId == intermediateTerminal.TerminalId
                        ? connection.EndTerminalId
                        : connection.StartTerminalId;
                    if (!terminalPositions.TryGetValue(
                            outerTerminalId,
                            out DocumentPoint position))
                    {
                        throw new InvalidOperationException(
                            $"No terminal anchor exists for joint '{intermediateTerminal.Id}'.");
                    }

                    return position;
                })
                .ToArray();
            DocumentPoint jointPosition = Midpoint(outerPositions[0], outerPositions[1]);
            terminalPositions[intermediateTerminal.TerminalId] = jointPosition;
            jointInputs.Add(
                (intermediateTerminal,
                    new JointLayout(intermediateTerminal.Id, jointPosition)));
        }

        if (cableSegmentsArray.Length > 0)
        {
            if (connectionById is null || terminalAnchors is null)
            {
                throw new InvalidOperationException(
                    "Cable rendering requires Connections and terminal anchors.");
            }

            var cableInputs = new List<(CableSegment CableSegment, CableLayout Layout)>();
            foreach (CableSegment cableSegment in cableSegmentsArray)
            {
                if (!connectionById.TryGetValue(
                        cableSegment.ConnectionId,
                        out Connection? connection))
                {
                    throw new InvalidOperationException(
                        $"No connection exists for cable segment '{cableSegment.Id}'.");
                }

                if (connection.StartTerminalId != cableSegment.StartTerminalId ||
                    connection.EndTerminalId != cableSegment.EndTerminalId)
                {
                    throw new InvalidOperationException(
                        $"Cable segment '{cableSegment.Id}' does not match its connection endpoints.");
                }

                if (!terminalPositions.TryGetValue(
                        connection.StartTerminalId,
                        out DocumentPoint startPosition) ||
                    !terminalPositions.TryGetValue(
                        connection.EndTerminalId,
                        out DocumentPoint endPosition))
                {
                    throw new InvalidOperationException(
                        $"No terminal anchors exist for cable segment '{cableSegment.Id}'.");
                }

                cableInputs.Add(
                    (cableSegment,
                        new CableLayout(
                            cableSegment.Id,
                            [startPosition, endPosition])));
            }

            elements.AddRange(_cableRenderer.Render(cableInputs));
            foreach ((CableSegment cableSegment, CableLayout cableLayout) in cableInputs)
            {
                hitTestEntries.Add(
                    new SelectionHitTestEntry(
                        new SelectionReference(
                            SelectionTargetKind.CableSegment,
                            cableSegment.Id),
                        CreateBounds(cableLayout.Start, cableLayout.End, 2),
                        30,
                        cableLayout.Start,
                        cableLayout.End));
            }
        }

        if (jointInputs.Count > 0)
        {
            elements.AddRange(_jointRenderer.Render(jointInputs));
            foreach ((IntermediateTerminal intermediateTerminal, JointLayout jointLayout) in jointInputs)
            {
                hitTestEntries.Add(
                    new SelectionHitTestEntry(
                        new SelectionReference(
                            SelectionTargetKind.IntermediateTerminal,
                            intermediateTerminal.Id),
                        new DocumentRect(
                            jointLayout.Position.XMillimeters - jointLayout.SizeMillimeters / 2,
                            jointLayout.Position.YMillimeters - jointLayout.SizeMillimeters / 2,
                            jointLayout.SizeMillimeters,
                            jointLayout.SizeMillimeters),
                        50));
            }
        }

        foreach (OverheadLine overheadLine in overheadLines)
        {
            if (!layout.OverheadLines.TryGetValue(
                    overheadLine.ConnectionId,
                    out OverheadLineLayout lineLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for overhead line '{overheadLine.ConnectionId}'.");
            }

            OverheadLineLayout effectiveLayout = lineLayout;
            if (connectionById is not null)
            {
                if (!connectionById.TryGetValue(
                        overheadLine.ConnectionId,
                        out Connection connection))
                {
                    throw new InvalidOperationException(
                        $"No connection exists for overhead line '{overheadLine.ConnectionId}'.");
                }

                overheadLine.ValidateAgainst(connection);
                if (terminalAnchors is not null)
                {
                    if (!terminalAnchors.TryGet(
                            connection.StartTerminalId,
                            out TerminalAnchor startAnchor) ||
                        !terminalAnchors.TryGet(
                            connection.EndTerminalId,
                            out TerminalAnchor endAnchor))
                    {
                        throw new InvalidOperationException(
                            $"No terminal anchor exists for overhead line '{overheadLine.ConnectionId}'.");
                    }

                    effectiveLayout = new OverheadLineLayout(
                        lineLayout.ConnectionId,
                        startAnchor.Position,
                        endAnchor.Position,
                        lineLayout.IsContinued,
                        lineLayout.ContinuationOffset);
                }
            }

            elements.AddRange(
                _symbolLibrary.CreateOverheadLineSegment(
                    OverheadLineSegment.From(overheadLine, effectiveLayout)));
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(
                        SelectionTargetKind.Connection,
                        overheadLine.ConnectionId),
                    CreateBounds(effectiveLayout.Start, effectiveLayout.End, 3),
                    10,
                    effectiveLayout.Start,
                    effectiveLayout.End));
        }

        foreach (Pole pole in poleById.Values)
        {
            if (!layout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for pole '{pole.Id}'.");
            }

            var switchInputs = new List<SwitchAttachmentRenderInput>();
            var cableTerminationInputs = new List<PoleAttachmentRenderInput>();
            foreach (PoleAttachment attachment in poleAttachments.Where(
                         attachment => attachment.PoleId == pole.Id))
            {
                if (!deviceById.TryGetValue(
                        attachment.AttachedDeviceId,
                        out Device? attachedDevice))
                {
                    throw new InvalidOperationException(
                        $"No attached device exists for attachment '{attachment.AttachmentId}'.");
                }

                if (!layout.Attachments.TryGetValue(
                        attachment.AttachmentId,
                        out AttachmentLayout? attachmentLayout) ||
                    attachmentLayout is null)
                {
                    throw new InvalidOperationException(
                        $"No layout exists for attachment '{attachment.AttachmentId}'.");
                }

                switch (attachedDevice)
                {
                    case SwitchDevice switchDevice:
                        switchInputs.Add(new SwitchAttachmentRenderInput(
                            attachment,
                            switchDevice,
                            attachmentLayout));
                        break;
                    case CableTermination cableTermination:
                        cableTerminationInputs.Add(new PoleAttachmentRenderInput(
                            attachment,
                            cableTermination,
                            attachmentLayout));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Device '{attachedDevice.Id}' cannot be rendered as a pole attachment.");
                }
            }

            elements.AddRange(_mixedPoleRenderer.Render(
                pole,
                poleLayout,
                switchInputs,
                cableTerminationInputs));
            DocumentRect poleBounds = PoleProfessionalGeometry.GetPoleBounds(poleLayout);
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(SelectionTargetKind.Device, pole.Id),
                    poleBounds,
                    20));
        }

        foreach (PoleAttachment attachment in poleAttachments)
        {
            if (!poleById.TryGetValue(attachment.PoleId, out Pole pole) ||
                !layout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                throw new InvalidOperationException(
                    $"No pole or pole layout exists for attachment '{attachment.AttachmentId}'.");
            }

            if (!deviceById.ContainsKey(attachment.AttachedDeviceId))
            {
                throw new InvalidOperationException(
                    $"No attached device exists for attachment '{attachment.AttachmentId}'.");
            }

            if (!layout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout? attachmentLayout) ||
                attachmentLayout is null)
            {
                throw new InvalidOperationException(
                    $"No layout exists for attachment '{attachment.AttachmentId}'.");
            }

            Device attachedDevice = deviceById[attachment.AttachedDeviceId];
            SymbolKind symbolKind = SymbolLibrary.ResolveAttachmentKind(attachedDevice);
            PoleAttachmentGeometry geometry = PoleProfessionalGeometry.GetAttachmentGeometry(
                poleLayout,
                attachmentLayout,
                symbolKind);
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(
                        SelectionTargetKind.PoleAttachment,
                        attachment.AttachmentId,
                        attachment.PoleId),
                    geometry.LogicalBounds,
                    40));
            if (attachedDevice is SwitchDevice switchDevice)
            {
                hitTestEntries.Add(
                    new SelectionHitTestEntry(
                        new SelectionReference(
                            SelectionTargetKind.Device,
                            switchDevice.Id,
                            attachment.AttachmentId),
                        geometry.LogicalBounds,
                        45));
            }
        }

        return new DrawingScene(elements, new SelectionHitTestIndex(hitTestEntries));
    }

    private static DocumentRect CreateBounds(
        DocumentPoint first,
        DocumentPoint second,
        double paddingMillimeters)
    {
        double minX = Math.Min(first.XMillimeters, second.XMillimeters) - paddingMillimeters;
        double minY = Math.Min(first.YMillimeters, second.YMillimeters) - paddingMillimeters;
        double maxX = Math.Max(first.XMillimeters, second.XMillimeters) + paddingMillimeters;
        double maxY = Math.Max(first.YMillimeters, second.YMillimeters) + paddingMillimeters;
        return new DocumentRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static DocumentPoint Midpoint(DocumentPoint first, DocumentPoint second)
    {
        return new DocumentPoint(
            (first.XMillimeters + second.XMillimeters) / 2,
            (first.YMillimeters + second.YMillimeters) / 2);
    }
}
