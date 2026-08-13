using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Application.Devices;

public sealed class PoleCreationFactory
{
    public PoleCreationResult Create(
        string poleNumber,
        PoleType poleType = PoleType.Cement,
        string? displayName = null)
    {
        var pole = new Pole(
            Guid.NewGuid(),
            poleNumber,
            displayName,
            poleType);

        return new PoleCreationResult(pole);
    }
}
