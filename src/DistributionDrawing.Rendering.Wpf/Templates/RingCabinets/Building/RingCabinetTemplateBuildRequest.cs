using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Templates.RingCabinets.Building;

public sealed class RingCabinetTemplateBuildRequest
{
    public RingCabinetTemplateBuildRequest(
        RingCabinetTemplate? template,
        string? displayName,
        DocumentPoint position)
    {
        Template = template;
        DisplayName = displayName;
        Position = position;
    }

    public RingCabinetTemplate? Template { get; }

    public string? DisplayName { get; }

    public DocumentPoint Position { get; }
}
