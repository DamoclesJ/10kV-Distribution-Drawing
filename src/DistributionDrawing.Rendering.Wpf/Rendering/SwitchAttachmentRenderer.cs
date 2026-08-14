using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

public sealed record SwitchAttachmentRenderInput(
    PoleAttachment Attachment,
    SwitchDevice SwitchDevice,
    AttachmentLayout Layout);

/// <summary>
/// Renders pole-installed switch attachments without creating or modifying
/// Domain objects.
/// </summary>
public sealed class SwitchAttachmentRenderer
{
    private readonly PoleSymbol _poleSymbol;
    private readonly AttachmentSymbol _attachmentSymbol;

    public SwitchAttachmentRenderer(SymbolLibrary? symbolLibrary = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _poleSymbol = new PoleSymbol(library);
        _attachmentSymbol = new AttachmentSymbol(library);
    }

    public IReadOnlyList<SceneElement> Render(
        Pole pole,
        PoleLayout poleLayout,
        IEnumerable<SwitchAttachmentRenderInput> attachments)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(attachments);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout));

        foreach (SwitchAttachmentRenderInput input in attachments)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input.Attachment.PoleId != pole.Id)
            {
                throw new InvalidOperationException(
                    $"Attachment '{input.Attachment.AttachmentId}' does not belong to pole '{pole.Id}'.");
            }

            if (input.Attachment.AttachedDeviceId != input.SwitchDevice.Id)
            {
                throw new InvalidOperationException(
                    $"Attachment '{input.Attachment.AttachmentId}' does not reference switch '{input.SwitchDevice.Id}'.");
            }

            if (input.SwitchDevice.InstallationType != SwitchInstallationType.Pole)
            {
                throw new InvalidOperationException(
                    $"Switch '{input.SwitchDevice.Id}' is not a pole-installed switch.");
            }

            if (input.SwitchDevice.SwitchKind is not SwitchKind.IsolationSwitch and
                not SwitchKind.CircuitBreaker)
            {
                throw new NotSupportedException(
                    $"Switch attachment rendering does not support '{input.SwitchDevice.SwitchKind}'.");
            }

            elements.AddRange(_attachmentSymbol.CreateElements(
                input.Attachment,
                input.SwitchDevice,
                poleLayout,
                input.Layout));
        }

        return elements;
    }
}
