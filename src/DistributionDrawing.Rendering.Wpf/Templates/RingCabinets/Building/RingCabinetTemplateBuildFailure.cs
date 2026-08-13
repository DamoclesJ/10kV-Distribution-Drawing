using System.Collections.Frozen;
using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateBuildFailure
{
    private RingCabinetTemplateBuildFailure(
        RingCabinetTemplateBuildFailureStage stage,
        RingCabinetTemplateBuildFailureKind kind,
        string message,
        IEnumerable<TemplateCapability>? unsupportedCapabilities,
        TemplateCapability? missingCapability,
        string? unsupportedRuleId,
        Exception? cause)
    {
        Stage = stage;
        Kind = kind;
        Message = message;
        UnsupportedCapabilities = (unsupportedCapabilities ?? [])
            .ToFrozenSet();
        MissingCapability = missingCapability;
        UnsupportedRuleId = unsupportedRuleId;
        Cause = cause;
    }

    public RingCabinetTemplateBuildFailureStage Stage { get; }

    public RingCabinetTemplateBuildFailureKind Kind { get; }

    public string Message { get; }

    public IReadOnlySet<TemplateCapability> UnsupportedCapabilities { get; }

    public TemplateCapability? MissingCapability { get; }

    public string? UnsupportedRuleId { get; }

    public Exception? Cause { get; }

    public static RingCabinetTemplateBuildFailure InvalidRequest(string message)
    {
        return new RingCabinetTemplateBuildFailure(
            RingCabinetTemplateBuildFailureStage.Coordinator,
            RingCabinetTemplateBuildFailureKind.InvalidTemplate,
            message,
            null,
            null,
            null,
            null);
    }

    public static RingCabinetTemplateBuildFailure FromDomainFailure(
        RingCabinetDomainBuildFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        RingCabinetTemplateBuildFailureKind kind = failure.Kind switch
        {
            RingCabinetDomainBuildFailureKind.InvalidTemplate =>
                RingCabinetTemplateBuildFailureKind.InvalidTemplate,
            RingCabinetDomainBuildFailureKind.UnsupportedCapability =>
                RingCabinetTemplateBuildFailureKind.UnsupportedCapability,
            RingCabinetDomainBuildFailureKind.DomainCreationFailure =>
                RingCabinetTemplateBuildFailureKind.DomainCreationFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                $"Unsupported Domain build failure kind '{failure.Kind}'.")
        };

        return new RingCabinetTemplateBuildFailure(
            RingCabinetTemplateBuildFailureStage.Domain,
            kind,
            failure.Message,
            failure.UnsupportedCapabilities,
            null,
            null,
            failure.Cause);
    }

    public static RingCabinetTemplateBuildFailure FromLayoutFailure(
        RingCabinetLayoutBuildFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        RingCabinetTemplateBuildFailureKind kind = failure.Kind switch
        {
            RingCabinetLayoutBuildFailureKind.InvalidInput =>
                RingCabinetTemplateBuildFailureKind.InvalidLayoutInput,
            RingCabinetLayoutBuildFailureKind.MissingRequiredCapability =>
                RingCabinetTemplateBuildFailureKind.MissingRequiredCapability,
            RingCabinetLayoutBuildFailureKind.UnsupportedCapability =>
                RingCabinetTemplateBuildFailureKind.UnsupportedCapability,
            RingCabinetLayoutBuildFailureKind.UnsupportedLayoutRule =>
                RingCabinetTemplateBuildFailureKind.UnsupportedLayoutRule,
            RingCabinetLayoutBuildFailureKind.LayoutCreationFailure =>
                RingCabinetTemplateBuildFailureKind.LayoutCreationFailure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                $"Unsupported Layout build failure kind '{failure.Kind}'.")
        };

        return new RingCabinetTemplateBuildFailure(
            RingCabinetTemplateBuildFailureStage.Layout,
            kind,
            failure.Message,
            failure.UnsupportedCapabilities,
            failure.MissingCapability,
            failure.UnsupportedRuleId,
            failure.Cause);
    }
}
