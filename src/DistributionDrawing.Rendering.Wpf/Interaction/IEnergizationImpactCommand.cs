namespace DistributionDrawing.Rendering.Wpf.Interaction;

/// <summary>
/// Declares whether a command changes facts used by energization analysis.
/// Commands without this contract retain the existing conservative lifecycle.
/// The declaration applies equally to execution, undo, and redo.
/// </summary>
public interface IEnergizationImpactCommand : ICommand
{
    bool AffectsEnergization { get; }
}
