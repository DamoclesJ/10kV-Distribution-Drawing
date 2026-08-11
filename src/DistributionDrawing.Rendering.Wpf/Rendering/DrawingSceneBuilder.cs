using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed class DrawingSceneBuilder
{
    private readonly PoleSymbol _poleSymbol = new();
    private readonly AttachmentSymbol _attachmentSymbol = new();

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
            overheadLines: overheadLines);
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
            overheadLines);
    }

    private DrawingScene BuildCore(
        DrawingLayout layout,
        IEnumerable<Pole> poles,
        IEnumerable<PoleAttachment> attachments,
        IEnumerable<Device> devices,
        IEnumerable<Connection>? connections,
        IEnumerable<OverheadLine> overheadLines)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(poles);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(overheadLines);

        var elements = new List<SceneElement>();
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
            }

            elements.AddRange(
                OverheadLineSegment.From(overheadLine, lineLayout).CreateElements());
        }

        foreach (Pole pole in poleById.Values)
        {
            if (!layout.Poles.TryGetValue(pole.Id, out PoleLayout poleLayout))
            {
                throw new InvalidOperationException(
                    $"No layout exists for pole '{pole.Id}'.");
            }

            elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout));
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
        }

        return new DrawingScene(elements);
    }
}
