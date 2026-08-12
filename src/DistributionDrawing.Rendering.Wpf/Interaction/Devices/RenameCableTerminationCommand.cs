using DistributionDrawing.Domain.Devices;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class RenameCableTerminationCommand : ICommand
{
    private readonly CableTermination _cableTermination;

    public RenameCableTerminationCommand(
        CableTermination cableTermination,
        string? beforeDisplayName,
        string? afterDisplayName)
    {
        _cableTermination = cableTermination ??
            throw new ArgumentNullException(nameof(cableTermination));
        BeforeDisplayName = beforeDisplayName;
        AfterDisplayName = NormalizeOptional(afterDisplayName);
    }

    public string? BeforeDisplayName { get; }

    public string? AfterDisplayName { get; }

    public void Execute()
    {
        _cableTermination.Rename(AfterDisplayName);
    }

    public void Undo()
    {
        _cableTermination.Rename(BeforeDisplayName);
    }

    public void Redo()
    {
        Execute();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
