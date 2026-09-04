using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Professional;

public readonly record struct GroundingPresentationAnchor(
    DocumentPoint Position,
    TerminalAnchorDirection Direction);

/// <summary>
/// Resolves transient grounding presentation geometry without changing the
/// electrical terminal anchor or persisting presentation-side state.
/// </summary>
public sealed class GroundingPresentationAnchorResolver
{
    public bool TryResolve(
        GroundingPoint groundingPoint,
        DrawingDocument document,
        DrawingLayout drawingLayout,
        TerminalAnchorIndex terminalAnchors,
        out GroundingPresentationAnchor presentationAnchor)
    {
        ArgumentNullException.ThrowIfNull(groundingPoint);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(drawingLayout);
        ArgumentNullException.ThrowIfNull(terminalAnchors);

        if (!terminalAnchors.TryGet(groundingPoint.TerminalId, out TerminalAnchor terminalAnchor))
        {
            presentationAnchor = default;
            return false;
        }

        SwitchDevice? switchDevice = document.Devices
            .OfType<SwitchDevice>()
            .SingleOrDefault(device =>
                device.InstallationType == SwitchInstallationType.Pole &&
                device.OwnsTerminal(groundingPoint.TerminalId));
        if (switchDevice is null)
        {
            presentationAnchor = new GroundingPresentationAnchor(
                terminalAnchor.Position,
                TerminalAnchorDirection.Right);
            return true;
        }

        PoleAttachment? attachment = document.PoleAttachments.SingleOrDefault(candidate =>
            candidate.AttachedDeviceId == switchDevice.Id);
        if (attachment is null ||
            !drawingLayout.Attachments.TryGetValue(
                attachment.AttachmentId,
                out AttachmentLayout? attachmentLayout) ||
            !drawingLayout.Poles.TryGetValue(attachment.PoleId, out PoleLayout? poleLayout))
        {
            presentationAnchor = default;
            return false;
        }

        PoleAttachmentGeometry geometry = PoleProfessionalGeometry.GetAttachmentGeometry(
            poleLayout,
            attachmentLayout,
            SymbolLibrary.ResolveAttachmentKind(switchDevice));
        bool isFirstTerminal = switchDevice.TerminalIds[0] == groundingPoint.TerminalId;
        DocumentPoint targetTerminal = isFirstTerminal
            ? geometry.FirstTerminal
            : geometry.SecondTerminal;
        DocumentPoint otherTerminal = isFirstTerminal
            ? geometry.SecondTerminal
            : geometry.FirstTerminal;
        TerminalAnchorDirection direction = ResolveOutwardDirection(
            otherTerminal,
            targetTerminal);
        DocumentRect compositeBounds = Union(
            PoleProfessionalGeometry.GetPoleBounds(poleLayout),
            geometry.LogicalBounds);

        presentationAnchor = new GroundingPresentationAnchor(
            MoveToOuterEdge(targetTerminal, compositeBounds, direction),
            direction);
        return true;
    }

    private static TerminalAnchorDirection ResolveOutwardDirection(
        DocumentPoint from,
        DocumentPoint to)
    {
        double deltaX = to.XMillimeters - from.XMillimeters;
        double deltaY = to.YMillimeters - from.YMillimeters;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX >= 0
                ? TerminalAnchorDirection.Right
                : TerminalAnchorDirection.Left;
        }

        return deltaY >= 0
            ? TerminalAnchorDirection.Down
            : TerminalAnchorDirection.Up;
    }

    private static DocumentPoint MoveToOuterEdge(
        DocumentPoint terminal,
        DocumentRect bounds,
        TerminalAnchorDirection direction) => direction switch
        {
            TerminalAnchorDirection.Left => new DocumentPoint(
                bounds.XMillimeters,
                terminal.YMillimeters),
            TerminalAnchorDirection.Right => new DocumentPoint(
                bounds.XMillimeters + bounds.WidthMillimeters,
                terminal.YMillimeters),
            TerminalAnchorDirection.Up => new DocumentPoint(
                terminal.XMillimeters,
                bounds.YMillimeters),
            TerminalAnchorDirection.Down => new DocumentPoint(
                terminal.XMillimeters,
                bounds.YMillimeters + bounds.HeightMillimeters),
            _ => terminal
        };

    private static DocumentRect Union(DocumentRect first, DocumentRect second)
    {
        double left = Math.Min(first.XMillimeters, second.XMillimeters);
        double top = Math.Min(first.YMillimeters, second.YMillimeters);
        double right = Math.Max(
            first.XMillimeters + first.WidthMillimeters,
            second.XMillimeters + second.WidthMillimeters);
        double bottom = Math.Max(
            first.YMillimeters + first.HeightMillimeters,
            second.YMillimeters + second.HeightMillimeters);
        return new DocumentRect(left, top, right - left, bottom - top);
    }
}
