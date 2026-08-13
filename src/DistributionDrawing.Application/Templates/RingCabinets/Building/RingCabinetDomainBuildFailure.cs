using System.Collections.Frozen;
using DistributionDrawing.Application.Templates.RingCabinets;

namespace DistributionDrawing.Application.Templates.RingCabinets.Building;

public sealed class RingCabinetDomainBuildFailure
{
    private RingCabinetDomainBuildFailure(
        RingCabinetDomainBuildFailureKind kind,
        string message,
        IEnumerable<TemplateCapability>? unsupportedCapabilities,
        Exception? cause)
    {
        Kind = kind;
        Message = message;
        UnsupportedCapabilities = (unsupportedCapabilities ?? [])
            .ToFrozenSet();
        Cause = cause;
    }

    public RingCabinetDomainBuildFailureKind Kind { get; }

    public string Message { get; }

    public IReadOnlySet<TemplateCapability> UnsupportedCapabilities { get; }

    public Exception? Cause { get; }

    public static RingCabinetDomainBuildFailure InvalidTemplate(string message)
    {
        return new RingCabinetDomainBuildFailure(
            RingCabinetDomainBuildFailureKind.InvalidTemplate,
            message,
            null,
            null);
    }

    public static RingCabinetDomainBuildFailure UnsupportedCapability(
        IEnumerable<TemplateCapability> capabilities)
    {
        TemplateCapability[] values = capabilities
            .Distinct()
            .OrderBy(capability => capability)
            .ToArray();
        return new RingCabinetDomainBuildFailure(
            RingCabinetDomainBuildFailureKind.UnsupportedCapability,
            $"The template requires unsupported capabilities: {string.Join(", ", values)}.",
            values,
            null);
    }

    public static RingCabinetDomainBuildFailure DomainCreationFailure(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return new RingCabinetDomainBuildFailure(
            RingCabinetDomainBuildFailureKind.DomainCreationFailure,
            "The template could not be converted into a valid ring cabinet.",
            null,
            cause);
    }
}
