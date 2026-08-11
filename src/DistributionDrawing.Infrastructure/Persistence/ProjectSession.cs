using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Minimal project lifecycle state used until the full editor session is implemented.
/// It contains restored Domain and persistence-neutral Layout state, but no
/// Rendering, Selection, or Undo state.
/// </summary>
public sealed record ProjectSession
{
    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        DrawingDocument domain,
        bool isDirty)
        : this(
            filePath,
            document,
            domain,
            ProjectLayoutSnapshot.Empty(domain.Id),
            isDirty)
    {
    }

    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        DrawingDocument domain,
        ProjectLayoutSnapshot layout,
        bool isDirty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(layout);

        FilePath = Path.GetFullPath(filePath);
        Document = document;
        Domain = domain;
        Layout = layout;
        IsDirty = isDirty;
    }

    public string FilePath { get; }

    public ProjectFileDocument Document { get; }

    public DrawingDocument Domain { get; }

    public ProjectLayoutSnapshot Layout { get; init; }

    public ProjectFileManifest Manifest => Document.Manifest;

    public ProjectFileMetadata Metadata => Document.Metadata;

    public Guid ProjectId => Manifest.ProjectId;

    public bool IsDirty { get; init; }
}
