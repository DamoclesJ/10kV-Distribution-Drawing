using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PoleLabel
{
    public LabelRequest CreatePoleRequest(Pole pole, PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        if (pole.Id != layout.PoleId)
        {
            throw new InvalidOperationException("Pole and layout IDs must match.");
        }

        return new LabelRequest(
            LabelTargetKind.Pole,
            pole.Id,
            pole.PoleNumber,
            layout.Position,
            layout.LabelOffset,
            preferredAlignment: LabelAlignment.Left,
            priority: 100,
            fontSizeMillimeters: 4);
    }

    public LabelRequest CreateAttachmentRequest(
        PoleAttachment attachment,
        Device attachedDevice,
        PoleLayout poleLayout,
        AttachmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(attachedDevice);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(layout);

        if (attachment.PoleId != poleLayout.PoleId ||
            attachment.AttachmentId != layout.AttachmentId ||
            attachment.AttachedDeviceId != attachedDevice.Id)
        {
            throw new InvalidOperationException(
                "Attachment, device, pole, and layout IDs must match.");
        }

        DocumentPoint anchor = new(
            poleLayout.Position.XMillimeters + layout.Offset.XMillimeters,
            poleLayout.Position.YMillimeters + layout.Offset.YMillimeters);

        return new LabelRequest(
            attachedDevice is SwitchDevice
                ? LabelTargetKind.SwitchDevice
                : LabelTargetKind.PoleAttachment,
            attachedDevice is SwitchDevice ? attachedDevice.Id : attachment.AttachmentId,
            SymbolLibrary.ResolveAttachmentLabel(attachedDevice),
            anchor,
            layout.LabelOffset,
            preferredAlignment: LabelAlignment.Left,
            priority: attachedDevice is SwitchDevice ? 80 : 70,
            fontSizeMillimeters: 3.5);
    }

    public SceneText CreateElement(LabelLayoutResult layoutResult)
    {
        ArgumentNullException.ThrowIfNull(layoutResult);

        return new SceneText(
            layoutResult.Position,
            layoutResult.Text,
            Colors.Black,
            layoutResult.Request.FontSizeMillimeters);
    }
}
