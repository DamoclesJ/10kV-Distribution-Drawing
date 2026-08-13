using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed class BayTemplate
{
    public BayTemplate(
        int index,
        BayFunction function,
        BayEquipmentConfiguration equipmentConfiguration)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Bay index must be positive.");
        }

        if (!Enum.IsDefined(function) || function == BayFunction.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(function),
                "A template bay requires a known bay function.");
        }

        Index = index;
        Function = function;
        EquipmentConfiguration = equipmentConfiguration ??
            throw new ArgumentNullException(nameof(equipmentConfiguration));
    }

    public int Index { get; }

    public BayFunction Function { get; }

    public BayEquipmentConfiguration EquipmentConfiguration { get; }
}
