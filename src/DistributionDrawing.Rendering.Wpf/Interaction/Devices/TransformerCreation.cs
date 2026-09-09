using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class TransformerCreation
{
    public TransformerCreation(
        Transformer transformer,
        Terminal hvTerminal,
        TransformerLayout layout)
    {
        Transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        HvTerminal = hvTerminal ?? throw new ArgumentNullException(nameof(hvTerminal));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));

        if (Transformer.Id != Layout.TransformerId ||
            Transformer.HvTerminalId != HvTerminal.Id)
        {
            throw new ArgumentException(
                "Transformer, HV terminal, and layout IDs must match.");
        }

        Layout.ValidateFor(Transformer.TransformerKind);
    }

    public Transformer Transformer { get; }

    public Terminal HvTerminal { get; }

    public TransformerLayout Layout { get; }
}
