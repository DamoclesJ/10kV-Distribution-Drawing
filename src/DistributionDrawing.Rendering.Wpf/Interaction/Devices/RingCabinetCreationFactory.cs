using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RingCabinetCreationFactory
{
    public RingCabinet Create(RingCabinetCreationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.DisplayName))
        {
            throw new ArgumentException("Ring cabinet display name is required.", nameof(configuration));
        }

        if (configuration.Intervals.Count == 0)
        {
            throw new ArgumentException(
                "A ring cabinet requires at least one interval.",
                nameof(configuration));
        }

        RingCabinetIntervalDefinition[] definitions = configuration.Intervals
            .Select(CreateIntervalDefinition)
            .ToArray();

        return RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                configuration.DisplayName.Trim(),
                definitions));
    }

    private static RingCabinetIntervalDefinition CreateIntervalDefinition(
        RingCabinetIntervalCreationConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.DisplayName))
        {
            throw new ArgumentException("Interval display name is required.", nameof(configuration));
        }

        string displayName = configuration.DisplayName.Trim();
        // Open is only the legal technical initialization required by the
        // current Domain factories. It is not a user-confirmed operating state.
        return configuration.IntervalKind switch
        {
            IntervalKind.LoadSwitchInterval when configuration.GroundingStructureKind is null =>
                RingCabinetIntervalDefinition.CreateLoadSwitch(
                    SwitchState.Open,
                    SwitchState.Open,
                    displayName),
            IntervalKind.LoadSwitchInterval => throw new ArgumentException(
                "A load-switch interval cannot specify a grounding structure.",
                nameof(configuration)),
            IntervalKind.IntegratedFeederInterval when
                configuration.GroundingStructureKind is GroundingStructureKind structure =>
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    structure,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open,
                    displayName),
            IntervalKind.IntegratedFeederInterval => throw new ArgumentException(
                "An integrated-feeder interval requires a grounding structure.",
                nameof(configuration)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(configuration),
                $"Unsupported interval kind '{configuration.IntervalKind}'.")
        };
    }
}
