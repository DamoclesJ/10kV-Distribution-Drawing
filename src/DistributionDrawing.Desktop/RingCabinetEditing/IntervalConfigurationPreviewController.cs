using System.Windows.Media;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.RingCabinetEditing;

public sealed class IntervalConfigurationPreviewController
{
    private readonly DrawingSceneBuilder _sceneBuilder;
    private readonly RingCabinetLayoutFactory _layoutFactory;
    private IReadOnlyList<SceneElement> _elements = [];

    public IntervalConfigurationPreviewController(
        DrawingSceneBuilder? sceneBuilder = null,
        RingCabinetLayoutFactory? layoutFactory = null)
    {
        _sceneBuilder = sceneBuilder ?? new DrawingSceneBuilder();
        _layoutFactory = layoutFactory ?? new RingCabinetLayoutFactory();
    }

    public Guid? TargetIntervalId { get; private set; }

    public bool IsActive => TargetIntervalId is not null;

    public RingCabinet? PreviewCabinet { get; private set; }

    public RingCabinetLayout? PreviewLayout { get; private set; }

    public IReadOnlyList<SceneElement> Elements => _elements;

    public bool Update(
        RingCabinet cabinet,
        RingCabinetLayout layout,
        Guid intervalId,
        IntervalKind targetKind,
        GroundingStructureKind? targetGroundingStructure)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(layout);
        RingCabinetInterval formal = cabinet.Intervals.Single(interval =>
            interval.IntervalId == intervalId);
        if (formal.IntervalKind == targetKind &&
            formal.GroundingStructureKind == targetGroundingStructure)
        {
            Cancel();
            return false;
        }

        RingCabinet preview = RingCabinet.Restore(cabinet.CaptureRestoreDefinition());
        Guid? migratedPTId = targetKind == IntervalKind.PTInterval
            ? preview.Intervals
                .Where(interval => interval.IntervalKind == IntervalKind.PTInterval &&
                                   interval.IntervalId != intervalId)
                .Select(interval => (Guid?)interval.IntervalId)
                .SingleOrDefault()
            : null;
        preview.ChangeIntervalType(intervalId, targetKind, targetGroundingStructure);
        RingCabinetLayout previewLayout = _layoutFactory.RebuildInterval(
            preview,
            layout,
            intervalId);
        if (migratedPTId is Guid ptId)
        {
            previewLayout = _layoutFactory.RebuildInterval(preview, previewLayout, ptId);
        }

        DrawingScene scene = _sceneBuilder.Build(preview, previewLayout);
        var mask = new SceneRectangle(
            new DocumentRect(
                layout.Position.XMillimeters,
                layout.Position.YMillimeters,
                layout.WidthMillimeters,
                layout.HeightMillimeters),
            Colors.White,
            0.1,
            Colors.White);
        _elements = [mask, .. scene.Elements.Where(element => element is not SceneLogicalBounds)];
        TargetIntervalId = intervalId;
        PreviewCabinet = preview;
        PreviewLayout = previewLayout;
        return true;
    }

    public bool Cancel()
    {
        bool changed = IsActive;
        TargetIntervalId = null;
        PreviewCabinet = null;
        PreviewLayout = null;
        _elements = [];
        return changed;
    }
}
