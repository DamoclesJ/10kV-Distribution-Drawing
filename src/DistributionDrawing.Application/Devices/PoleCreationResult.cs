using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Application.Devices;

public sealed class PoleCreationResult
{
    public PoleCreationResult(Pole pole)
    {
        Pole = pole ?? throw new ArgumentNullException(nameof(pole));
    }

    public Pole Pole { get; }

    public IReadOnlyList<PoleAttachment> Attachments { get; } = [];
}
