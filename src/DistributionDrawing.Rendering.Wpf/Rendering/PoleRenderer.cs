using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record PoleAttachmentRenderInput(
    PoleAttachment Attachment,
    CableTermination CableTermination,
    AttachmentLayout Layout);

/// <summary>
/// Renders a Pole and its supported cable-termination attachments without
/// creating or modifying Domain objects.
/// </summary>
public sealed class PoleRenderer
{
    private readonly PoleSymbol _poleSymbol;
    private readonly AttachmentSymbol _attachmentSymbol;
    private readonly PoleLabel _poleLabel;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public PoleRenderer(
        SymbolLibrary? symbolLibrary = null,
        LabelLayoutEngine? labelLayoutEngine = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _poleSymbol = new PoleSymbol(library);
        _attachmentSymbol = new AttachmentSymbol(library);
        _poleLabel = new PoleLabel();
        _labelLayoutEngine = labelLayoutEngine ?? new LabelLayoutEngine();
    }

    public IReadOnlyList<SceneElement> Render(
        Pole pole,
        PoleLayout layout,
        IEnumerable<PoleAttachmentRenderInput>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, layout, includeLabel: false));
        var labelRequests = new List<LabelRequest>
        {
            _poleLabel.CreatePoleRequest(pole, layout)
        };

        foreach (PoleAttachmentRenderInput input in attachments ?? [])
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input.Attachment.PoleId != pole.Id)
            {
                throw new InvalidOperationException(
                    $"Attachment '{input.Attachment.AttachmentId}' does not belong to pole '{pole.Id}'.");
            }

            if (input.Attachment.AttachedDeviceId != input.CableTermination.Id)
            {
                throw new InvalidOperationException(
                    $"Attachment '{input.Attachment.AttachmentId}' does not reference cable termination '{input.CableTermination.Id}'.");
            }

            elements.AddRange(_attachmentSymbol.CreateElements(
                input.Attachment,
                input.CableTermination,
                layout,
                input.Layout,
                includeLabel: false));
            labelRequests.Add(_poleLabel.CreateAttachmentRequest(
                input.Attachment,
                input.CableTermination,
                layout,
                input.Layout));
        }

        elements.AddRange(_labelLayoutEngine.Layout(labelRequests)
            .Select(_poleLabel.CreateElement));

        return elements;
    }
}
