using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateDomainBuilder
{
    private static readonly IReadOnlySet<TemplateCapability> SupportedCapabilities =
        new HashSet<TemplateCapability>
        {
            TemplateCapability.BasicRingCabinet,
            TemplateCapability.LoadSwitchBay,
            TemplateCapability.IntegratedFeederBay,
            // Layout is handled by the later RuntimeLayout builder stage.
            TemplateCapability.RingCabinetLayout
        };

    public RingCabinetDomainBuildOutcome Build(
        RingCabinetTemplate? template,
        string? displayName)
    {
        if (template is null)
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.InvalidTemplate(
                    "A ring cabinet template is required."));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.InvalidTemplate(
                    "A ring cabinet display name is required."));
        }

        TemplateCapability[] unsupportedCapabilities = template.RequiredCapabilities
            .Where(capability => !SupportedCapabilities.Contains(capability))
            .ToArray();
        if (unsupportedCapabilities.Length > 0)
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.UnsupportedCapability(
                    unsupportedCapabilities));
        }

        RingCabinetIntervalDefinition[] intervalDefinitions;
        try
        {
            intervalDefinitions = template.Bays
                .Select(CreateIntervalDefinition)
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.DomainCreationFailure(exception));
        }

        try
        {
            RingCabinetDefinition definition = RingCabinetDefinition.Create(
                Guid.NewGuid(),
                displayName.Trim(),
                intervalDefinitions);
            RingCabinet cabinet = RingCabinet.Create(definition);
            return RingCabinetDomainBuildOutcome.Success(
                new RingCabinetDomainBuildResult(
                    definition,
                    cabinet,
                    template.RequiredCapabilities));
        }
        catch (ArgumentException exception)
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.DomainCreationFailure(exception));
        }
        catch (InvalidOperationException exception)
        {
            return RingCabinetDomainBuildOutcome.Failed(
                RingCabinetDomainBuildFailure.DomainCreationFailure(exception));
        }
    }

    private static RingCabinetIntervalDefinition CreateIntervalDefinition(BayTemplate bay)
    {
        return bay.EquipmentConfiguration switch
        {
            LoadSwitchConfiguration => RingCabinetIntervalDefinition.CreateLoadSwitch(
                bay.Index,
                SwitchState.Open,
                SwitchState.Open),
            IntegratedFeederConfiguration integratedFeeder =>
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    bay.Index,
                    integratedFeeder.GroundingStructureKind,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open),
            _ => throw new ArgumentException(
                $"Unsupported equipment configuration '{bay.EquipmentConfiguration.GetType().Name}'.",
                nameof(bay))
        };
    }
}
