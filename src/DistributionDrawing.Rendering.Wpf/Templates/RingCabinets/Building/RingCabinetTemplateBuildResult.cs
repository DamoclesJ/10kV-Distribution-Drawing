using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateBuildResult
{
    public RingCabinetTemplateBuildResult(
        RingCabinetDomainBuildResult domainResult,
        RingCabinetLayoutBuildResult layoutResult)
    {
        DomainResult = domainResult ??
            throw new ArgumentNullException(nameof(domainResult));
        LayoutResult = layoutResult ??
            throw new ArgumentNullException(nameof(layoutResult));
        if (DomainResult.Cabinet.Id != LayoutResult.Layout.CabinetId)
        {
            throw new ArgumentException(
                "Ring cabinet and runtime layout IDs must match.",
                nameof(layoutResult));
        }
    }

    public RingCabinetDomainBuildResult DomainResult { get; }

    public RingCabinetLayoutBuildResult LayoutResult { get; }

    public RingCabinetDefinition Definition => DomainResult.Definition;

    public RingCabinet Cabinet => DomainResult.Cabinet;

    public RingCabinetLayout Layout => LayoutResult.Layout;

    public IReadOnlySet<TemplateCapability> RequiredCapabilities =>
        DomainResult.RequiredCapabilities;
}
