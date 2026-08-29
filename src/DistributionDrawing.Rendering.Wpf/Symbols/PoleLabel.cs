using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using System.Windows.Media;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class PoleLabel
{
    private readonly DrawingMetrics _metrics;

    public PoleLabel(DrawingMetrics? metrics = null)
    {
        _metrics = metrics ?? DrawingMetrics.Default;
    }

    public LabelRequest CreatePoleRequest(Pole pole, PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        if (pole.Id != layout.PoleId)
        {
            throw new InvalidOperationException("Pole and layout IDs must match.");
        }

        DocumentPoint center = PoleProfessionalGeometry.GetPoleCenter(layout, _metrics);
        return new LabelRequest(
            LabelTargetKind.Pole,
            pole.Id,
            pole.PoleNumber,
            center,
            new DocumentPoint(
                0,
                _metrics.Pole.PoleRadius +
                _metrics.Typography.PoleNumberFontSize + 2),
            preferredAlignment: LabelAlignment.Center,
            priority: 100,
            fontSizeMillimeters: _metrics.Typography.PoleNumberFontSize);
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

        DocumentPoint anchor;
        DocumentPoint offset = layout.LabelOffset;
        if (attachedDevice is SwitchDevice)
        {
            AttachmentLayout baseLayout = layout.RotateBy(-layout.RotationQuarterTurns);
            PoleAttachmentGeometry baseGeometry = PoleProfessionalGeometry.GetAttachmentGeometry(
                poleLayout,
                baseLayout,
                SymbolLibrary.ResolveAttachmentKind(attachedDevice),
                _metrics);
            DocumentPoint baseAnchor = new(
                baseGeometry.LogicalBounds.XMillimeters,
                baseGeometry.LogicalBounds.YMillimeters);
            DocumentPoint baseLabelPosition = new(
                baseAnchor.XMillimeters + layout.LabelOffset.XMillimeters,
                baseAnchor.YMillimeters + layout.LabelOffset.YMillimeters);
            anchor = PoleProfessionalGeometry.RotateAroundPole(
                poleLayout,
                baseAnchor,
                layout.RotationQuarterTurns,
                _metrics);
            DocumentPoint labelPosition = PoleProfessionalGeometry.RotateAroundPole(
                poleLayout,
                baseLabelPosition,
                layout.RotationQuarterTurns,
                _metrics);
            offset = new DocumentPoint(
                labelPosition.XMillimeters - anchor.XMillimeters,
                labelPosition.YMillimeters - anchor.YMillimeters);
        }
        else
        {
            anchor = new DocumentPoint(
                poleLayout.Position.XMillimeters + layout.Offset.XMillimeters,
                poleLayout.Position.YMillimeters + layout.Offset.YMillimeters);
        }

        return new LabelRequest(
            attachedDevice is SwitchDevice
                ? LabelTargetKind.SwitchDevice
                : LabelTargetKind.PoleAttachment,
            attachedDevice is SwitchDevice ? attachedDevice.Id : attachment.AttachmentId,
            SymbolLibrary.ResolveAttachmentLabel(attachedDevice),
            anchor,
            offset,
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
