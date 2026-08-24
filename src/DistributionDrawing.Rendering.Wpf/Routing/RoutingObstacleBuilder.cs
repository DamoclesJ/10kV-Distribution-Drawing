using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Routing;

public sealed class RoutingObstacleBuilder
{
    private readonly DrawingMetrics _metrics;

    public RoutingObstacleBuilder(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public IReadOnlyList<RoutingObstacle> Build(
        IEnumerable<Device> devices,
        IEnumerable<PoleAttachment> attachments,
        DrawingLayout drawingLayout,
        IReadOnlyDictionary<Guid, RingCabinetLayout>? ringCabinetLayouts = null,
        IEnumerable<JointLayout>? jointLayouts = null)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(drawingLayout);

        Device[] deviceArray = devices.ToArray();
        Dictionary<Guid, Device> devicesById = deviceArray.ToDictionary(device => device.Id);
        var obstacles = new List<RoutingObstacle>();

        foreach (RingCabinet cabinet in deviceArray.OfType<RingCabinet>().OrderBy(cabinet => cabinet.Id))
        {
            if (ringCabinetLayouts is not null &&
                ringCabinetLayouts.TryGetValue(cabinet.Id, out RingCabinetLayout? layout))
            {
                obstacles.Add(new RoutingObstacle(
                    cabinet.Id,
                    RoutingObstacleKind.RingCabinet,
                    new DocumentRect(
                        layout.Position.XMillimeters,
                        layout.Position.YMillimeters,
                        layout.WidthMillimeters,
                        layout.HeightMillimeters)));

                foreach (RingCabinetInterval interval in cabinet.Intervals
                             .Where(interval => interval.IntervalKind == IntervalKind.PTInterval))
                {
                    if (!layout.IntervalLayouts.TryGetValue(
                            interval.IntervalId,
                            out RingCabinetIntervalLayout? intervalLayout) ||
                        intervalLayout.PTSymbolPosition is not DocumentPoint ptPosition)
                    {
                        continue;
                    }

                    double diameter = _metrics.PT.CoilRadius * 2;
                    double coilTop = layout.Position.YMillimeters +
                                     intervalLayout.RelativePosition.YMillimeters +
                                     ptPosition.YMillimeters;
                    double terminalY = layout.Position.YMillimeters +
                                       intervalLayout.RelativePosition.YMillimeters +
                                       intervalLayout.HeightMillimeters;
                    double coilBottom = coilTop +
                                        diameter * 2 - _metrics.PT.CoilSpacing;
                    obstacles.Add(new RoutingObstacle(
                        interval.IntervalId,
                        RoutingObstacleKind.RingCabinet,
                        new DocumentRect(
                            layout.Position.XMillimeters +
                            intervalLayout.RelativePosition.XMillimeters +
                            ptPosition.XMillimeters,
                            Math.Min(terminalY, coilTop),
                            diameter,
                            coilBottom - Math.Min(terminalY, coilTop))));
                }
            }
        }

        foreach (Pole pole in deviceArray.OfType<Pole>().OrderBy(pole => pole.Id))
        {
            if (drawingLayout.Poles.TryGetValue(pole.Id, out PoleLayout? layout))
            {
                obstacles.Add(new RoutingObstacle(
                    pole.Id,
                    RoutingObstacleKind.Pole,
                    PoleProfessionalGeometry.GetPoleBounds(layout)));
            }
        }

        foreach (PoleAttachment attachment in attachments.OrderBy(attachment => attachment.AttachmentId))
        {
            if (!drawingLayout.Attachments.TryGetValue(
                    attachment.AttachmentId,
                    out AttachmentLayout? attachmentLayout) ||
                !drawingLayout.Poles.TryGetValue(attachment.PoleId, out PoleLayout? poleLayout) ||
                !devicesById.TryGetValue(attachment.AttachedDeviceId, out Device? device))
            {
                continue;
            }

            SymbolKind kind = SymbolLibrary.ResolveAttachmentKind(device);
            obstacles.Add(new RoutingObstacle(
                attachment.AttachmentId,
                RoutingObstacleKind.PoleAttachment,
                PoleProfessionalGeometry.GetAttachmentGeometry(
                    poleLayout,
                    attachmentLayout,
                    kind).LogicalBounds));
        }

        foreach (JointLayout joint in (jointLayouts ?? []).OrderBy(
                     joint => joint.IntermediateTerminalId))
        {
            obstacles.Add(new RoutingObstacle(
                joint.IntermediateTerminalId,
                RoutingObstacleKind.IntermediateTerminal,
                new DocumentRect(
                    joint.Position.XMillimeters - joint.SizeMillimeters / 2,
                    joint.Position.YMillimeters - joint.SizeMillimeters / 2,
                    joint.SizeMillimeters,
                    joint.SizeMillimeters)));
        }

        return obstacles;
    }
}
