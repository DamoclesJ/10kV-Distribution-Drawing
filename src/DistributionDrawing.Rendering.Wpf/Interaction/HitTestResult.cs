using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction;

public sealed record HitTestResult(
    DistributionDrawing.Application.Interaction.SelectionTarget Target,
    DocumentPoint HitPosition);
