using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Layout;

namespace DistributionDrawing.Rendering.Wpf.PropertyInspector;

public sealed class ResolvedSelection
{
    public required SelectionReference Reference { get; init; }

    public DrawingDocument? Document { get; init; }

    public RingCabinet? RingCabinet { get; init; }

    public RingCabinetInterval? RingCabinetInterval { get; init; }

    public SwitchDevice? SwitchDevice { get; init; }

    public Pole? Pole { get; init; }

    public PoleAttachment? PoleAttachment { get; init; }

    public Device? AttachedDevice { get; init; }

    public CableTermination? CableTermination { get; init; }

    public Connection? Connection { get; init; }

    public OverheadLine? OverheadLine { get; init; }

    public WorkScope? WorkScope { get; init; }

    public GroundingPoint? GroundingPoint { get; init; }

    public Terminal? Terminal { get; init; }

    public RingCabinetLayout? RingCabinetLayout { get; init; }

    public RingCabinetIntervalLayout? RingCabinetIntervalLayout { get; init; }

    public PoleLayout? PoleLayout { get; init; }

    public AttachmentLayout? AttachmentLayout { get; init; }

    public OverheadLineLayout? OverheadLineLayout { get; init; }

    public SelectionHitTestEntry? HitTestEntry { get; init; }
}
