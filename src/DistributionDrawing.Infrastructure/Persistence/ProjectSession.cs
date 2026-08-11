namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Minimal project lifecycle state used until the full editor session is implemented.
/// It intentionally contains no Domain, Layout, Rendering, Selection, or Undo state.
/// </summary>
public sealed record ProjectSession
{
    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        bool isDirty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);

        FilePath = Path.GetFullPath(filePath);
        Document = document;
        IsDirty = isDirty;
    }

    public string FilePath { get; }

    public ProjectFileDocument Document { get; }

    public ProjectFileManifest Manifest => Document.Manifest;

    public ProjectFileMetadata Metadata => Document.Metadata;

    public Guid ProjectId => Manifest.ProjectId;

    public bool IsDirty { get; init; }
}
