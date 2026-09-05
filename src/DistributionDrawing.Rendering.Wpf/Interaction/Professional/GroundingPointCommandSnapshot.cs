using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Professional;

public sealed record GroundingPointCommandSnapshot(
    Guid GroundingPointId,
    GroundingTarget Target,
    string Location,
    string? Number,
    string? Note)
{
    public static GroundingPointCommandSnapshot From(GroundingPoint groundingPoint)
    {
        ArgumentNullException.ThrowIfNull(groundingPoint);

        return new GroundingPointCommandSnapshot(
            groundingPoint.GroundingPointId,
            groundingPoint.Target,
            groundingPoint.Location,
            groundingPoint.Number,
            groundingPoint.Note);
    }
}
