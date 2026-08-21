using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Professional;

/// <summary>
/// Builds a transient terminal-to-document-coordinate index from the Domain
/// and current millimeter layout. No Domain objects are retained.
/// </summary>
public sealed class TerminalAnchorIndex
{
    private readonly IReadOnlyDictionary<Guid, TerminalAnchor> _anchors;

    private TerminalAnchorIndex(IReadOnlyDictionary<Guid, TerminalAnchor> anchors)
    {
        _anchors = anchors;
    }

    public IReadOnlyCollection<TerminalAnchor> Anchors => _anchors.Values.ToArray();

    public bool TryGet(Guid terminalId, out TerminalAnchor anchor)
    {
        return _anchors.TryGetValue(terminalId, out anchor);
    }

    public static TerminalAnchorIndex Build(
        DrawingDocument document,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout> ringCabinetLayouts,
        IEnumerable<Connection>? connections = null,
        IEnumerable<CableSegment>? cableSegments = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(ringCabinetLayouts);

        var anchors = new Dictionary<Guid, TerminalAnchor>();

        foreach (Pole pole in document.Devices.OfType<Pole>())
        {
            if (!drawingLayout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                continue;
            }

            DocumentPoint poleAnchor = PoleProfessionalGeometry.GetPoleCenter(poleLayout);
            foreach (Guid terminalId in pole.OverheadAnchorTerminalIds)
            {
                Set(anchors, terminalId, poleAnchor, TerminalAnchorDirection.Auto);
            }
        }

        foreach (PoleAttachment attachment in document.PoleAttachments)
        {
            if (!drawingLayout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout attachmentLayout) ||
                !drawingLayout.Poles.TryGetValue(attachment.PoleId, out PoleLayout poleLayout))
            {
                continue;
            }

            Device? attachedDevice = document.Devices
                .SingleOrDefault(device => device.Id == attachment.AttachedDeviceId);
            if (attachedDevice is null)
            {
                continue;
            }

            SymbolKind symbolKind = SymbolLibrary.ResolveAttachmentKind(attachedDevice);
            PoleAttachmentGeometry geometry = PoleProfessionalGeometry.GetAttachmentGeometry(
                poleLayout,
                attachmentLayout,
                symbolKind);

            if (attachedDevice is CableTermination cableTermination)
            {
                Set(
                    anchors,
                    cableTermination.CableSideTerminalId,
                    geometry.FirstTerminal,
                    TerminalAnchorDirection.Up);
                Set(
                    anchors,
                    cableTermination.OverheadSideTerminalId,
                    geometry.SecondTerminal,
                    TerminalAnchorDirection.Down);
            }
            else if (attachedDevice is SwitchDevice switchDevice)
            {
                bool vertical = symbolKind == SymbolKind.DropoutFuse;
                Set(
                    anchors,
                    switchDevice.TerminalIds[0],
                    geometry.FirstTerminal,
                    vertical ? TerminalAnchorDirection.Up : TerminalAnchorDirection.Left);
                Set(
                    anchors,
                    switchDevice.TerminalIds[1],
                    geometry.SecondTerminal,
                    vertical ? TerminalAnchorDirection.Down : TerminalAnchorDirection.Right);
            }
        }

        foreach (RingCabinet cabinet in document.Devices.OfType<RingCabinet>())
        {
            if (!ringCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? cabinetLayout))
            {
                continue;
            }

            foreach (RingCabinetInterval interval in cabinet.Intervals)
            {
                if (!cabinetLayout.IntervalLayouts.TryGetValue(
                        interval.IntervalId,
                        out RingCabinetIntervalLayout intervalLayout))
                {
                    continue;
                }

                DocumentPoint origin = new(
                    cabinetLayout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
                    cabinetLayout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
                double terminalX = interval.IntervalKind == IntervalKind.PTInterval &&
                                   intervalLayout.PTSymbolPosition is DocumentPoint ptPosition
                    ? origin.XMillimeters + ptPosition.XMillimeters +
                      DrawingMetrics.Default.PT.CoilRadius
                    : origin.XMillimeters + intervalLayout.WidthMillimeters / 2;
                DocumentPoint terminalPosition = new(
                    terminalX,
                    origin.YMillimeters + intervalLayout.HeightMillimeters);
                Set(
                    anchors,
                    interval.ExternalTerminalId,
                    terminalPosition,
                    TerminalAnchorDirection.Down,
                    DrawingMetrics.Default.CableTermination
                        .CableTerminalExitMinimumStubLength);
            }
        }

        if (connections is not null && cableSegments is not null)
        {
            Dictionary<Guid, Connection> connectionById = connections.ToDictionary(
                connection => connection.Id);
            HashSet<Guid> cableConnectionIds = cableSegments
                .Select(cable => cable.ConnectionId)
                .ToHashSet();
            foreach (IntermediateTerminal intermediateTerminal in document.IntermediateTerminals)
            {
                Connection[] jointConnections = connectionById.Values
                    .Where(connection => connection.Type == ConnectionType.Cable &&
                        cableConnectionIds.Contains(connection.Id) &&
                        connection.UsesTerminal(intermediateTerminal.TerminalId))
                    .ToArray();
                if (jointConnections.Length != 2)
                {
                    continue;
                }

                Guid[] outerTerminalIds = jointConnections.Select(connection =>
                    connection.StartTerminalId == intermediateTerminal.TerminalId
                        ? connection.EndTerminalId
                        : connection.StartTerminalId).ToArray();
                if (anchors.TryGetValue(outerTerminalIds[0], out TerminalAnchor first) &&
                    anchors.TryGetValue(outerTerminalIds[1], out TerminalAnchor second))
                {
                    Set(
                        anchors,
                        intermediateTerminal.TerminalId,
                        new DocumentPoint(
                            (first.Position.XMillimeters + second.Position.XMillimeters) / 2,
                            (first.Position.YMillimeters + second.Position.YMillimeters) / 2),
                        TerminalAnchorDirection.Auto);
                }
            }
        }

        return new TerminalAnchorIndex(
            new Dictionary<Guid, TerminalAnchor>(anchors));
    }

    private static void Set(
        IDictionary<Guid, TerminalAnchor> anchors,
        Guid terminalId,
        DocumentPoint position,
        TerminalAnchorDirection direction,
        double minimumStubLength = 0)
    {
        if (terminalId != Guid.Empty)
        {
            anchors[terminalId] = new TerminalAnchor(
                terminalId,
                position,
                direction,
                minimumStubLength);
        }
    }
}
