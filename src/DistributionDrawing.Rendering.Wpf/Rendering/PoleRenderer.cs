using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
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

    public PoleRenderer(SymbolLibrary? symbolLibrary = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _poleSymbol = new PoleSymbol(library);
        _attachmentSymbol = new AttachmentSymbol(library);
    }

    public IReadOnlyList<SceneElement> Render(
        Pole pole,
        PoleLayout layout,
        IEnumerable<PoleAttachmentRenderInput>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(layout);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, layout));

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
                input.Layout));
        }

        return elements;
    }
}
