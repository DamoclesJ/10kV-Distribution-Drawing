using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;

namespace DistributionDrawing.Rendering.Wpf.Symbols;

public sealed class AttachmentSymbol
{
    private readonly SymbolLibrary _library;

    public AttachmentSymbol(SymbolLibrary? library = null)
    {
        _library = library ?? new SymbolLibrary();
    }

    public IReadOnlyList<SceneElement> CreateElements(
        PoleAttachment attachment,
        Device attachedDevice,
        PoleLayout poleLayout,
        AttachmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(attachedDevice);
        ArgumentNullException.ThrowIfNull(poleLayout);
        ArgumentNullException.ThrowIfNull(layout);

        return _library.CreateAttachment(
            attachment,
            attachedDevice,
            poleLayout,
            layout);
    }
}
