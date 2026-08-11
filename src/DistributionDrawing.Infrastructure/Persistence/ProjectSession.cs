using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Minimal project lifecycle state used until the full editor session is implemented.
/// It contains the restored Domain model, but no Layout, Rendering, Selection, or Undo state.
/// </summary>
public sealed record ProjectSession
{
    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        DrawingDocument domain,
        bool isDirty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(domain);

        FilePath = Path.GetFullPath(filePath);
        Document = document;
        Domain = domain;
        IsDirty = isDirty;
    }

    public string FilePath { get; }

    public ProjectFileDocument Document { get; }

    public DrawingDocument Domain { get; }

    public ProjectFileManifest Manifest => Document.Manifest;

    public ProjectFileMetadata Metadata => Document.Metadata;

    public Guid ProjectId => Manifest.ProjectId;

    public bool IsDirty { get; init; }
}
