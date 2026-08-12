namespace DistributionDrawing.Rendering.Wpf.Layout;

public sealed class DrawingLayout
{
    private readonly Dictionary<Guid, PoleLayout> _poles = [];
    private readonly Dictionary<Guid, AttachmentLayout> _attachments = [];
    private readonly Dictionary<Guid, OverheadLineLayout> _overheadLines = [];

    public IReadOnlyDictionary<Guid, PoleLayout> Poles => _poles;

    public IReadOnlyDictionary<Guid, AttachmentLayout> Attachments => _attachments;

    public IReadOnlyDictionary<Guid, OverheadLineLayout> OverheadLines => _overheadLines;

    public void Add(PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_poles.TryAdd(layout.PoleId, layout))
        {
            throw new InvalidOperationException(
                $"A layout for pole '{layout.PoleId}' already exists.");
        }
    }

    public void Add(AttachmentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_attachments.TryAdd(layout.AttachmentId, layout))
        {
            throw new InvalidOperationException(
                $"A layout for attachment '{layout.AttachmentId}' already exists.");
        }
    }

    public void Add(OverheadLineLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_overheadLines.TryAdd(layout.ConnectionId, layout))
        {
            throw new InvalidOperationException(
                $"A layout for overhead line '{layout.ConnectionId}' already exists.");
        }
    }

    public void Replace(PoleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!_poles.ContainsKey(layout.PoleId))
        {
            throw new InvalidOperationException(
                $"No layout exists for pole '{layout.PoleId}'.");
        }

        _poles[layout.PoleId] = layout;
    }

    public PoleLayout RemovePole(Guid poleId)
    {
        if (!_poles.Remove(poleId, out PoleLayout? layout))
        {
            throw new InvalidOperationException($"No layout exists for pole '{poleId}'.");
        }

        return layout;
    }
}
