using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Rendering.Wpf.Scene;

public sealed record SceneBuildDiagnostic(
    string Code,
    string Message,
    SelectionTargetKind TargetKind,
    Guid TargetId);
