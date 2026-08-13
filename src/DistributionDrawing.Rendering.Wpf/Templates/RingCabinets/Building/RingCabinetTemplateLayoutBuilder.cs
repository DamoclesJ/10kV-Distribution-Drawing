using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateLayoutBuilder
{
    private static readonly IReadOnlySet<TemplateCapability> UnsupportedCapabilities =
        new HashSet<TemplateCapability>
        {
            TemplateCapability.DtuSecondary
        };

    private readonly RingCabinetLayoutFactory _layoutFactory;

    public RingCabinetTemplateLayoutBuilder(
        RingCabinetLayoutFactory? layoutFactory = null)
    {
        _layoutFactory = layoutFactory ?? new RingCabinetLayoutFactory();
    }

    public RingCabinetLayoutBuildOutcome Build(
        RingCabinetDomainBuildResult? domainBuildResult,
        RingCabinetLayoutRule? layoutRule,
        DocumentPoint position)
    {
        if (domainBuildResult is null)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.InvalidInput(
                    "A ring cabinet domain build result is required."));
        }

        if (layoutRule is null)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.InvalidInput(
                    "A ring cabinet layout rule is required."));
        }

        if (!domainBuildResult.RequiredCapabilities.Contains(
                TemplateCapability.RingCabinetLayout))
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.MissingRequiredCapability(
                    TemplateCapability.RingCabinetLayout));
        }

        TemplateCapability[] unsupportedCapabilities = domainBuildResult.RequiredCapabilities
            .Where(UnsupportedCapabilities.Contains)
            .ToArray();
        if (unsupportedCapabilities.Length > 0)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.UnsupportedCapability(
                    unsupportedCapabilities));
        }

        if (!string.Equals(
                layoutRule.RuleId,
                RingCabinetLayoutRule.DefaultRuleId,
                StringComparison.Ordinal))
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.UnsupportedLayoutRule(
                    layoutRule.RuleId));
        }

        if (!double.IsFinite(position.XMillimeters) ||
            !double.IsFinite(position.YMillimeters))
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.InvalidInput(
                    "The ring cabinet position must contain finite coordinates."));
        }

        try
        {
            RingCabinetLayout layout = _layoutFactory.Create(
                domainBuildResult.Cabinet,
                position);
            return RingCabinetLayoutBuildOutcome.Success(
                new RingCabinetLayoutBuildResult(layout));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.LayoutCreationFailure(exception));
        }
        catch (InvalidOperationException exception)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.LayoutCreationFailure(exception));
        }
        catch (NotSupportedException exception)
        {
            return RingCabinetLayoutBuildOutcome.Failed(
                RingCabinetLayoutBuildFailure.LayoutCreationFailure(exception));
        }
    }
}
