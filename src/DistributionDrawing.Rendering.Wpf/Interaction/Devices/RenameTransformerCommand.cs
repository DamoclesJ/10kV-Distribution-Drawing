using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RenameTransformerCommand : ICommand
{
    private readonly Transformer _transformer;
    private readonly string? _before;
    private readonly bool _beforeWasLegacyIncomplete;
    private readonly string _after;

    public RenameTransformerCommand(Transformer transformer, string displayName)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Transformer display name cannot be empty.",
                nameof(displayName));
        }

        _before = transformer.DisplayName;
        _beforeWasLegacyIncomplete = transformer.IsLegacyNamingIncomplete;
        _after = displayName.Trim();
    }

    public void Execute() => _transformer.Rename(_after);

    public void Undo()
    {
        if (_beforeWasLegacyIncomplete)
        {
            _transformer.RestoreLegacyNamingIncomplete();
            return;
        }

        _transformer.Rename(_before!);
    }

    public void Redo() => Execute();
}
