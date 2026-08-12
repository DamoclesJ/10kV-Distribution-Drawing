using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class PropertyInspectionSource
{
    public DrawingDocument? Document { get; init; }

    public RingCabinet? RingCabinet { get; init; }

    public RingCabinetLayout? RingCabinetLayout { get; init; }

    public IReadOnlyDictionary<Guid, RingCabinetLayout> RingCabinetLayouts { get; init; } =
        new Dictionary<Guid, RingCabinetLayout>();

    public DrawingLayout? DrawingLayout { get; init; }

    public IReadOnlyList<Pole> Poles { get; init; } = [];

    public IReadOnlyList<Device> Devices { get; init; } = [];

    public IReadOnlyList<PoleAttachment> PoleAttachments { get; init; } = [];

    public IReadOnlyList<OverheadLine> OverheadLines { get; init; } = [];

    public IReadOnlyList<Connection> Connections { get; init; } = [];

    public IReadOnlyList<WorkScope> WorkScopes { get; init; } = [];

    public IReadOnlyList<GroundingPoint> GroundingPoints { get; init; } = [];

    public IReadOnlyList<Terminal> Terminals { get; init; } = [];

    public SelectionHitTestIndex? HitTestIndex { get; init; }
}
