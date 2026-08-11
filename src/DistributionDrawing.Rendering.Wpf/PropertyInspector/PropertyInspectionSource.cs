using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class PropertyInspectionSource
{
    public RingCabinet? RingCabinet { get; init; }

    public RingCabinetLayout? RingCabinetLayout { get; init; }

    public DrawingLayout? DrawingLayout { get; init; }

    public IReadOnlyList<Pole> Poles { get; init; } = [];

    public IReadOnlyList<PoleAttachment> PoleAttachments { get; init; } = [];

    public IReadOnlyList<OverheadLine> OverheadLines { get; init; } = [];

    public IReadOnlyList<Connection> Connections { get; init; } = [];

    public SelectionHitTestIndex? HitTestIndex { get; init; }
}
