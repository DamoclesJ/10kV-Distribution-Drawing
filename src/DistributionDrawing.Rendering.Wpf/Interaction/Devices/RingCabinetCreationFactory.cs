using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RingCabinetCreationFactory
{
    private readonly RingCabinetTemplateDomainBuilder _domainBuilder;

    public RingCabinetCreationFactory(
        RingCabinetTemplateDomainBuilder? domainBuilder = null)
    {
        _domainBuilder = domainBuilder ?? new RingCabinetTemplateDomainBuilder();
    }

    public RingCabinet Create(RingCabinetCreationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        RingCabinetDomainBuildOutcome outcome = _domainBuilder.Build(
            configuration.Template,
            configuration.DisplayName);
        if (outcome.Result is { } result)
        {
            return result.Cabinet;
        }

        RingCabinetDomainBuildFailure failure = outcome.Failure ??
            throw new InvalidOperationException("The template build failed without an error.");
        throw new ArgumentException(
            failure.Cause?.Message ?? failure.Message,
            nameof(configuration),
            failure.Cause);
    }
}
