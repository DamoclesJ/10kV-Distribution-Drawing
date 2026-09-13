using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Rendering.Wpf.Interaction.Devices;

public sealed class TransformerCreationFactory
{
    public TransformerCreation Create(
        TransformerKind transformerKind,
        DocumentPoint position,
        string displayName,
        TransformerOrientation? orientation = null)
    {
        Guid transformerId = Guid.NewGuid();
        Guid hvTerminalId = Guid.NewGuid();
        TransformerOrientation resolvedOrientation = orientation ?? transformerKind switch
        {
            TransformerKind.PublicPoleMounted => TransformerOrientation.Vertical,
            TransformerKind.DedicatedPoleMounted => TransformerOrientation.Vertical,
            TransformerKind.PublicIndoor => TransformerOrientation.Horizontal,
            _ => throw new ArgumentOutOfRangeException(nameof(transformerKind))
        };

        var transformer = new Transformer(
            transformerId,
            transformerKind,
            hvTerminalId,
            displayName);
        var hvTerminal = new Terminal(
            hvTerminalId,
            TopologyOwnerType.Device,
            transformerId,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: false,
            electricalNodeId: null,
            allowedConnectionTypes: [transformer.AllowedConnectionType]);
        var layout = new TransformerLayout(
            transformerId,
            position,
            resolvedOrientation,
            transformerKind);

        return new TransformerCreation(transformer, hvTerminal, layout);
    }
}
