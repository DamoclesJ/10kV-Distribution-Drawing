using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed class EditPropertyCommand : ICommand
{
    public const string RingCabinetNameProperty = "RingCabinet.Name";
    public const string RingCabinetDisplayNameProperty = "RingCabinet.DisplayName";
    public const string RingCabinetLineNameProperty = "RingCabinet.LineName";
    public const string PoleNumberProperty = "Pole.PoleNumber";
    public const string PoleDescriptionProperty = "Pole.Description";
    public const string CableTypeProperty = "CableSegment.CableType";
    public const string CableLengthProperty = "CableSegment.Length";
    public const string SwitchDisplayNameProperty = "SwitchDevice.DisplayName";

    private readonly object _target;

    public EditPropertyCommand(
        object target,
        string propertyKey,
        object oldValue,
        object newValue)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            throw new ArgumentException("Property key is required.", nameof(propertyKey));
        }

        PropertyKey = propertyKey.Trim();
        OldValue = oldValue ?? throw new ArgumentNullException(nameof(oldValue));
        NewValue = newValue ?? throw new ArgumentNullException(nameof(newValue));
        ValidateProperty(_target, PropertyKey, OldValue);
        ValidateProperty(_target, PropertyKey, NewValue);
    }

    public string PropertyKey { get; }

    public object OldValue { get; }

    public object NewValue { get; }

    public void Execute() => Apply(NewValue);

    public void Undo() => Apply(OldValue);

    public void Redo() => Execute();

    private void Apply(object value)
    {
        switch (_target, PropertyKey)
        {
            case (RingCabinet cabinet, RingCabinetNameProperty or RingCabinetDisplayNameProperty):
                cabinet.Rename((string)value);
                break;
            case (RingCabinet cabinet, RingCabinetLineNameProperty):
                cabinet.RenameLineName((string)value);
                break;
            case (Pole pole, PoleNumberProperty):
                pole.RenamePoleNumber((string)value);
                break;
            case (Pole pole, PoleDescriptionProperty):
                pole.Rename((string)value);
                break;
            case (CableSegment cable, CableTypeProperty):
                cable.ChangeCableType((string)value);
                break;
            case (CableSegment cable, CableLengthProperty):
                cable.ChangeLength((double)value);
                break;
            case (SwitchDevice switchDevice, SwitchDisplayNameProperty):
                switchDevice.Rename((string)value);
                break;
            default:
                throw new InvalidOperationException(
                    $"Property '{PropertyKey}' cannot be edited on '{_target.GetType().Name}'.");
        }
    }

    private static void ValidateProperty(object target, string propertyKey, object value)
    {
        switch (target, propertyKey, value)
        {
            case (RingCabinet, RingCabinetNameProperty or RingCabinetDisplayNameProperty, string):
            case (RingCabinet, RingCabinetLineNameProperty, string):
            case (Pole, PoleNumberProperty or PoleDescriptionProperty, string):
            case (SwitchDevice, SwitchDisplayNameProperty, string):
            case (CableSegment, CableTypeProperty, string):
            case (CableSegment, CableLengthProperty, double):
                return;
            default:
                throw new ArgumentException(
                    $"Property '{propertyKey}' or value type is not supported.",
                    nameof(value));
        }
    }
}
