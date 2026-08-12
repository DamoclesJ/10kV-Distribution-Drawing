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
    private readonly PoleSymbol _poleSymbol;
    private readonly AttachmentSymbol _attachmentSymbol;
    private readonly RingCabinetSymbol _ringCabinetSymbol;
    private readonly ProfessionalSceneBuilder _professionalSceneBuilder;

    public DrawingSceneBuilder(SymbolLibrary? symbolLibrary = null)
    {
        _symbolLibrary = symbolLibrary ?? new SymbolLibrary();
        _poleSymbol = new PoleSymbol(_symbolLibrary);
        _attachmentSymbol = new AttachmentSymbol(_symbolLibrary);
        _ringCabinetSymbol = new RingCabinetSymbol(_symbolLibrary);
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
            _ringCabinetSymbol.CreateElements(cabinet, layout),
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
            layout.RingCabinetLayouts);
        DrawingScene baseScene = BuildCore(
            layout.DrawingLayout,
            document.Devices.OfType<Pole>(),
            document.PoleAttachments,
            document.Devices,
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
            connections,
            overheadLines,
            terminalAnchors: null);
    }

    private DrawingScene BuildCore(
        DrawingLayout layout,
        IEnumerable<Pole> poles,
        IEnumerable<PoleAttachment> attachments,
        IEnumerable<Device> devices,
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
        var deviceById = devices.ToDictionary(device => device.Id);
        var connectionById = connections?.ToDictionary(connection => connection.Id);

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
                    10));
        }

        foreach (Pole pole in poleById.Values)
        {
            if (!layout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for pole '{pole.Id}'.");
            }

            elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout));
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(SelectionTargetKind.Device, pole.Id),
                    new DocumentRect(
                        poleLayout.Position.XMillimeters,
                        poleLayout.Position.YMillimeters,
                        poleLayout.WidthMillimeters,
                        poleLayout.HeightMillimeters),
                    20));
        }

        foreach (PoleAttachment attachment in attachments)
        {
            if (!poleById.TryGetValue(attachment.PoleId, out Pole pole) ||
                !layout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                throw new InvalidOperationException(
                    $"No pole or pole layout exists for attachment '{attachment.AttachmentId}'.");
            }

            if (!deviceById.TryGetValue(
                    attachment.AttachedDeviceId,
                    out Device attachedDevice))
            {
                throw new InvalidOperationException(
                    $"No attached device exists for attachment '{attachment.AttachmentId}'.");
            }

            if (!layout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout attachmentLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for attachment '{attachment.AttachmentId}'.");
            }

            elements.AddRange(
                _attachmentSymbol.CreateElements(
                    attachment,
                    attachedDevice,
                    poleLayout,
                    attachmentLayout));
            hitTestEntries.Add(
                new SelectionHitTestEntry(
                    new SelectionReference(
                        SelectionTargetKind.PoleAttachment,
                        attachment.AttachmentId,
                        attachment.PoleId),
                    new DocumentRect(
                        poleLayout.Position.XMillimeters + attachmentLayout.Offset.XMillimeters,
                        poleLayout.Position.YMillimeters + attachmentLayout.Offset.YMillimeters,
                        attachmentLayout.WidthMillimeters,
                        attachmentLayout.HeightMillimeters),
                    40));
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
}
