using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Application.Devices;

public sealed class PoleCreationResult
{
    public PoleCreationResult(
        Pole pole,
        IEnumerable<PoleAttachment>? attachments = null,
        IEnumerable<Device>? devices = null,
        IEnumerable<Terminal>? terminals = null,
        IEnumerable<ElectricalNode>? electricalNodes = null)
    {
        Pole = pole ?? throw new ArgumentNullException(nameof(pole));
        Attachments = (attachments ?? []).ToArray();
        Devices = (devices ?? []).ToArray();
        Terminals = (terminals ?? []).ToArray();
        ElectricalNodes = (electricalNodes ?? []).ToArray();
    }

    public Pole Pole { get; }

    public IReadOnlyList<PoleAttachment> Attachments { get; }

    public IReadOnlyList<Device> Devices { get; }

    public IReadOnlyList<Terminal> Terminals { get; }

    public IReadOnlyList<ElectricalNode> ElectricalNodes { get; }
}
