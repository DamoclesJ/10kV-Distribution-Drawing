using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Layout;

/// <summary>
/// Manual presentation override for one GroundingPoint. The offset is relative
/// to the currently derived default symbol position and carries no topology.
/// </summary>
public sealed record GroundingPointLayout
{
    public GroundingPointLayout(Guid groundingPointId, DocumentPoint symbolOffset)
    {
        if (groundingPointId == Guid.Empty)
        {
            throw new ArgumentException("Grounding point ID cannot be empty.", nameof(groundingPointId));
        }

        if (!double.IsFinite(symbolOffset.XMillimeters) ||
            !double.IsFinite(symbolOffset.YMillimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(symbolOffset));
        }

        GroundingPointId = groundingPointId;
        SymbolOffset = symbolOffset;
    }

    public Guid GroundingPointId { get; }

    public DocumentPoint SymbolOffset { get; }

    public GroundingPointLayout MoveTo(DocumentPoint symbolOffset) =>
        new(GroundingPointId, symbolOffset);
}
