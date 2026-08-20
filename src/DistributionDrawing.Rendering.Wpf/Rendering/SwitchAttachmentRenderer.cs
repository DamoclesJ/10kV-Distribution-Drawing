using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
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
    private readonly PoleLabel _poleLabel;
    private readonly LabelLayoutEngine _labelLayoutEngine;

    public SwitchAttachmentRenderer(
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
        IEnumerable<SwitchAttachmentRenderInput> attachments)
    {
        ArgumentNullException.ThrowIfNull(pole);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(attachments);

        var elements = new List<SceneElement>();
        elements.AddRange(_poleSymbol.CreateElements(pole, poleLayout, includeLabel: false));
        var labelRequests = new List<LabelRequest>
        {
            _poleLabel.CreatePoleRequest(pole, poleLayout)
        };

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

            if (!IsSupportedPoleSwitch(input.SwitchDevice.SwitchKind))
            {
                throw new NotSupportedException(
                    $"Switch attachment rendering does not support '{input.SwitchDevice.SwitchKind}'.");
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

        elements.AddRange(_labelLayoutEngine.Layout(labelRequests)
            .Select(_poleLabel.CreateElement));

        return elements;
    }

    private static bool IsSupportedPoleSwitch(SwitchKind kind) =>
        kind is SwitchKind.CircuitBreaker or
            SwitchKind.LoadSwitch or
            SwitchKind.IsolationSwitch or
            SwitchKind.DropoutFuse;
}
