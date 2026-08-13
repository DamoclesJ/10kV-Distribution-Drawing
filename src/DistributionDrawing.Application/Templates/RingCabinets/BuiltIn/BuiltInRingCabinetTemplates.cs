using DistributionDrawing.Application.Templates.RingCabinets.Library;

namespace DistributionDrawing.Application.Templates.RingCabinets.BuiltIn;

public static class BuiltInRingCabinetTemplates
{
    public static RingCabinetTemplateLibrary CreateLibrary()
    {
        return new RingCabinetTemplateLibrary(
            [Conventional10kVRingCabinetTemplate.Create()]);
    }
}
