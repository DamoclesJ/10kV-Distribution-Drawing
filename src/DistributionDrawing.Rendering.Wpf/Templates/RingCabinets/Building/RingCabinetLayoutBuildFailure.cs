using System.Collections.Frozen;
using DistributionDrawing.Application.Templates.RingCabinets;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetLayoutBuildFailure
{
    private RingCabinetLayoutBuildFailure(
        RingCabinetLayoutBuildFailureKind kind,
        string message,
        string? unsupportedRuleId,
        TemplateCapability? missingCapability,
        IEnumerable<TemplateCapability>? unsupportedCapabilities,
        Exception? cause)
    {
        Kind = kind;
        Message = message;
        UnsupportedRuleId = unsupportedRuleId;
        MissingCapability = missingCapability;
        UnsupportedCapabilities = (unsupportedCapabilities ?? [])
            .ToFrozenSet();
        Cause = cause;
    }

    public RingCabinetLayoutBuildFailureKind Kind { get; }

    public string Message { get; }

    public string? UnsupportedRuleId { get; }

    public TemplateCapability? MissingCapability { get; }

    public IReadOnlySet<TemplateCapability> UnsupportedCapabilities { get; }

    public Exception? Cause { get; }

    public static RingCabinetLayoutBuildFailure InvalidInput(string message)
    {
        return new RingCabinetLayoutBuildFailure(
            RingCabinetLayoutBuildFailureKind.InvalidInput,
            message,
            null,
            null,
            null,
            null);
    }

    public static RingCabinetLayoutBuildFailure MissingRequiredCapability(
        TemplateCapability capability)
    {
        return new RingCabinetLayoutBuildFailure(
            RingCabinetLayoutBuildFailureKind.MissingRequiredCapability,
            $"The domain build result does not declare the required '{capability}' capability.",
            null,
            capability,
            null,
            null);
    }

    public static RingCabinetLayoutBuildFailure UnsupportedCapability(
        IEnumerable<TemplateCapability> capabilities)
    {
        TemplateCapability[] values = capabilities
            .Distinct()
            .OrderBy(capability => capability)
            .ToArray();
        return new RingCabinetLayoutBuildFailure(
            RingCabinetLayoutBuildFailureKind.UnsupportedCapability,
            $"The runtime layout builder does not support capabilities: {string.Join(", ", values)}.",
            null,
            null,
            values,
            null);
    }

    public static RingCabinetLayoutBuildFailure UnsupportedLayoutRule(string ruleId)
    {
        return new RingCabinetLayoutBuildFailure(
            RingCabinetLayoutBuildFailureKind.UnsupportedLayoutRule,
            $"The ring cabinet layout rule '{ruleId}' is not supported.",
            ruleId,
            null,
            null,
            null);
    }

    public static RingCabinetLayoutBuildFailure LayoutCreationFailure(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return new RingCabinetLayoutBuildFailure(
            RingCabinetLayoutBuildFailureKind.LayoutCreationFailure,
            "The ring cabinet runtime layout could not be created.",
            null,
            null,
            null,
            cause);
    }
}
