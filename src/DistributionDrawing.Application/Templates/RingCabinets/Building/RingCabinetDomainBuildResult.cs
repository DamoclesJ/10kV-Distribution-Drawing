using System.Collections.Frozen;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets.Building;

public sealed class RingCabinetDomainBuildResult
{
    public RingCabinetDomainBuildResult(
        RingCabinetDefinition definition,
        RingCabinet cabinet,
        IEnumerable<TemplateCapability> requiredCapabilities)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Cabinet = cabinet ?? throw new ArgumentNullException(nameof(cabinet));
        if (Definition.CabinetId != Cabinet.Id)
        {
            throw new ArgumentException(
                "Ring cabinet definition and aggregate IDs must match.",
                nameof(cabinet));
        }

        RequiredCapabilities = (requiredCapabilities ??
                throw new ArgumentNullException(nameof(requiredCapabilities)))
            .ToFrozenSet();
    }

    public RingCabinetDefinition Definition { get; }

    public RingCabinet Cabinet { get; }

    public IReadOnlySet<TemplateCapability> RequiredCapabilities { get; }
}
