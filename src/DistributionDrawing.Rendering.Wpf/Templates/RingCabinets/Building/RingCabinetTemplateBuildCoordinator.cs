using DistributionDrawing.Application.Templates.RingCabinets.Building;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateBuildCoordinator
{
    private readonly RingCabinetTemplateDomainBuilder _domainBuilder;
    private readonly RingCabinetTemplateLayoutBuilder _layoutBuilder;

    public RingCabinetTemplateBuildCoordinator(
        RingCabinetTemplateDomainBuilder? domainBuilder = null,
        RingCabinetTemplateLayoutBuilder? layoutBuilder = null)
    {
        _domainBuilder = domainBuilder ?? new RingCabinetTemplateDomainBuilder();
        _layoutBuilder = layoutBuilder ?? new RingCabinetTemplateLayoutBuilder();
    }

    public RingCabinetTemplateBuildOutcome Build(
        RingCabinetTemplateBuildRequest? request)
    {
        if (request is null)
        {
            return RingCabinetTemplateBuildOutcome.Failed(
                RingCabinetTemplateBuildFailure.InvalidRequest(
                    "A ring cabinet template build request is required."));
        }

        RingCabinetDomainBuildOutcome domainOutcome = _domainBuilder.Build(
            request.Template,
            request.DisplayName);
        if (!domainOutcome.IsSuccess)
        {
            RingCabinetDomainBuildFailure failure = domainOutcome.Failure ??
                throw new InvalidOperationException(
                    "A failed Domain build outcome must contain a failure.");
            return RingCabinetTemplateBuildOutcome.Failed(
                RingCabinetTemplateBuildFailure.FromDomainFailure(failure));
        }

        RingCabinetDomainBuildResult domainResult = domainOutcome.Result ??
            throw new InvalidOperationException(
                "A successful Domain build outcome must contain a result.");
        RingCabinetLayoutBuildOutcome layoutOutcome = _layoutBuilder.Build(
            domainResult,
            request.Template!.LayoutRule,
            request.Position);
        if (!layoutOutcome.IsSuccess)
        {
            RingCabinetLayoutBuildFailure failure = layoutOutcome.Failure ??
                throw new InvalidOperationException(
                    "A failed Layout build outcome must contain a failure.");
            return RingCabinetTemplateBuildOutcome.Failed(
                RingCabinetTemplateBuildFailure.FromLayoutFailure(failure));
        }

        RingCabinetLayoutBuildResult layoutResult = layoutOutcome.Result ??
            throw new InvalidOperationException(
                "A successful Layout build outcome must contain a result.");
        return RingCabinetTemplateBuildOutcome.Success(
            new RingCabinetTemplateBuildResult(domainResult, layoutResult));
    }
}
