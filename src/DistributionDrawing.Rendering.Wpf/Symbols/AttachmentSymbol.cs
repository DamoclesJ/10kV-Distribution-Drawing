using System.Windows.Media;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class AttachmentSymbol
{
    public IReadOnlyList<SceneElement> CreateElements(
        PoleAttachment attachment,
        Device attachedDevice,
        PoleLayout poleLayout,
        AttachmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(attachedDevice);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(layout);

        if (attachment.AttachmentId != layout.AttachmentId)
        {
            throw new InvalidOperationException(
                "Pole attachment and attachment layout IDs must match.");
        }

        if (attachment.PoleId != poleLayout.PoleId)
        {
            throw new InvalidOperationException(
                "Pole attachment and pole layout IDs must match.");
        }

        if (attachment.AttachedDeviceId != attachedDevice.Id)
        {
            throw new InvalidOperationException(
                "Pole attachment and attached device IDs must match.");
        }

        double x = poleLayout.Position.XMillimeters + layout.Offset.XMillimeters;
        double y = poleLayout.Position.YMillimeters + layout.Offset.YMillimeters;
        double poleCenterX = poleLayout.Position.XMillimeters + poleLayout.WidthMillimeters / 2;
        double deviceCenterY = y + layout.HeightMillimeters / 2;

        string label = GetLabel(attachedDevice);
        Color bodyColor = attachedDevice.SwitchState == SwitchState.Closed
            ? Colors.DarkBlue
            : Colors.Black;

        var elements = new List<SceneElement>
        {
            new SceneLine(
                new DocumentPoint(poleCenterX, deviceCenterY),
                new DocumentPoint(x, deviceCenterY),
                Colors.Black,
                0.7),
            new SceneRectangle(
                new DocumentRect(
                    x,
                    y,
                    layout.WidthMillimeters,
                    layout.HeightMillimeters),
                bodyColor,
                0.8,
                Colors.White),
            new SceneText(
                new DocumentPoint(
                    x + layout.LabelOffset.XMillimeters,
                    y + layout.LabelOffset.YMillimeters),
                label,
                Colors.Black,
                3.5)
        };

        if (attachedDevice.SwitchState == SwitchState.Open)
        {
            elements.Add(
                new SceneLine(
                    new DocumentPoint(x + 3, y + layout.HeightMillimeters - 3),
                    new DocumentPoint(
                        x + layout.WidthMillimeters - 3,
                        y + 3),
                    Colors.Black,
                    0.6));
        }

        return elements;
    }

    private static string GetLabel(Device device)
    {
        if (device is SwitchDevice switchDevice)
        {
            return switchDevice.SwitchKind switch
            {
                SwitchKind.CircuitBreaker => "柱上断路器",
                SwitchKind.LoadSwitch => "柱上负荷开关",
                SwitchKind.IsolationSwitch => "柱上隔离刀闸",
                SwitchKind.DropoutFuse => "跌落式熔断器",
                _ => device.DisplayName ?? "柱上开关"
            };
        }

        if (device is CableTermination)
        {
            return "电缆终端";
        }

        return device.DisplayName ?? "柱上设备";
    }
}
