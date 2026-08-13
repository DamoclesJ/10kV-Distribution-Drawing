using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed class IntegratedFeederConfiguration : BayEquipmentConfiguration
{
    public IntegratedFeederConfiguration(GroundingStructureKind groundingStructureKind)
    {
        if (!Enum.IsDefined(groundingStructureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(groundingStructureKind));
        }

        GroundingStructureKind = groundingStructureKind;
    }

    public GroundingStructureKind GroundingStructureKind { get; }
}
