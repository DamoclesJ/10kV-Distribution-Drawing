using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Professional;

/// <summary>
/// A transient, value-only position for a Domain terminal in the current
/// document layout. It intentionally contains no Domain object reference.
/// </summary>
public readonly record struct TerminalAnchor(
    Guid TerminalId,
    DocumentPoint Position,
    TerminalAnchorDirection Direction = TerminalAnchorDirection.Auto,
    double MinimumStubLength = 0);

public enum TerminalAnchorDirection
{
    Auto,
    Left,
    Right,
    Up,
    Down
}
