using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
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
    private readonly PoleLabel _poleLabel;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public MixedPoleRenderer(
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
        PoleLayout poleLayout,
        IEnumerable<SwitchAttachmentRenderInput> switchAttachments,
        IEnumerable<PoleAttachmentRenderInput> cableTerminationAttachments)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(switchAttachments);
        ArgumentNullException.ThrowIfNull(cableTerminationAttachments);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout, includeLabel: false));
        var labelRequests = new List<LabelRequest>
        {
            _poleLabel.CreatePoleRequest(pole, poleLayout)
        };

        foreach (SwitchAttachmentRenderInput input in switchAttachments)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateAttachment(input.Attachment, pole, input.SwitchDevice.Id);
            if (input.SwitchDevice.InstallationType != SwitchInstallationType.Pole)
            {
                throw new InvalidOperationException(
                    $"Switch '{input.SwitchDevice.Id}' is not a pole-installed switch.");
            }

            if (!IsSupportedPoleSwitch(input.SwitchDevice.SwitchKind))
            {
                throw new NotSupportedException(
                    $"Mixed pole rendering does not support '{input.SwitchDevice.SwitchKind}'.");
            }

            elements.AddRange(_attachmentSymbol.CreateElements(
                input.Attachment,
                input.SwitchDevice,
                poleLayout,
                input.Layout,
                includeLabel: false));
            labelRequests.Add(_poleLabel.CreateAttachmentRequest(
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
                input.Layout,
                includeLabel: false));
            labelRequests.Add(_poleLabel.CreateAttachmentRequest(
                input.Attachment,
                input.CableTermination,
                poleLayout,
                input.Layout));
        }

        elements.AddRange(_labelLayoutEngine.Layout(labelRequests)
            .Select(_poleLabel.CreateElement));

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

    private static bool IsSupportedPoleSwitch(SwitchKind kind) =>
        kind is SwitchKind.CircuitBreaker or
            SwitchKind.LoadSwitch or
            SwitchKind.IsolationSwitch or
            SwitchKind.DropoutFuse;
}
