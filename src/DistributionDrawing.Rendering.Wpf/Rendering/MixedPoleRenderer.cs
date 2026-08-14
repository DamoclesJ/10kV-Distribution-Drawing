using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Rendering;

/// <summary>
/// Renders one Pole with switch and cable-termination attachments in a
/// shared layout without creating or modifying Domain objects.
/// </summary>
public sealed class MixedPoleRenderer
{
    private readonly PoleSymbol _poleSymbol;
    private readonly AttachmentSymbol _attachmentSymbol;

    public MixedPoleRenderer(SymbolLibrary? symbolLibrary = null)
    {
        var library = symbolLibrary ?? new SymbolLibrary();
        _poleSymbol = new PoleSymbol(library);
        _attachmentSymbol = new AttachmentSymbol(library);
    }

    public IReadOnlyList<SceneElement> Render(
        Pole pole,
        PoleLayout poleLayout,
        IEnumerable<SwitchAttachmentRenderInput> switchAttachments,
        IEnumerable<PoleAttachmentRenderInput> cableTerminationAttachments)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(switchAttachments);
        ArgumentNullException.ThrowIfNull(cableTerminationAttachments);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout));

        foreach (SwitchAttachmentRenderInput input in switchAttachments)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateAttachment(input.Attachment, pole, input.SwitchDevice.Id);
            if (input.SwitchDevice.InstallationType != SwitchInstallationType.Pole)
            {
                throw new InvalidOperationException(
                    $"Switch '{input.SwitchDevice.Id}' is not a pole-installed switch.");
            }

            if (input.SwitchDevice.SwitchKind is not SwitchKind.IsolationSwitch and
                not SwitchKind.CircuitBreaker)
            {
                throw new NotSupportedException(
                    $"Mixed pole rendering does not support '{input.SwitchDevice.SwitchKind}'.");
            }

            elements.AddRange(_attachmentSymbol.CreateElements(
                input.Attachment,
                input.SwitchDevice,
                poleLayout,
                input.Layout));
        }

        foreach (PoleAttachmentRenderInput input in cableTerminationAttachments)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateAttachment(input.Attachment, pole, input.CableTermination.Id);
            elements.AddRange(_attachmentSymbol.CreateElements(
                input.Attachment,
                input.CableTermination,
                poleLayout,
                input.Layout));
        }

        return elements;
    }

    private static void ValidateAttachment(
        PoleAttachment attachment,
        Pole pole,
        Guid attachedDeviceId)
    {
        if (attachment.PoleId != pole.Id)
        {
            throw new InvalidOperationException(
                $"Attachment '{attachment.AttachmentId}' does not belong to pole '{pole.Id}'.");
        }

        if (attachment.AttachedDeviceId != attachedDeviceId)
        {
            throw new InvalidOperationException(
                $"Attachment '{attachment.AttachmentId}' does not reference device '{attachedDeviceId}'.");
        }
    }
}
