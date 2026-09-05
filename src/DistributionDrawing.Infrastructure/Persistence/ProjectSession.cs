using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Minimal project lifecycle state used until the full editor session is implemented.
/// It contains restored Domain, Professional, and persistence-neutral Layout state, but no
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
            ProjectProfessionalSnapshot.Empty(domain.Id),
            isDirty,
            ProjectFileFormat.CurrentVersion)
    {
    }

    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        DrawingDocument domain,
        ProjectLayoutSnapshot layout,
        bool isDirty)
        : this(
            filePath,
            document,
            domain,
            layout,
            ProjectProfessionalSnapshot.Empty(domain.Id),
            isDirty,
            ProjectFileFormat.CurrentVersion)
    {
    }

    public ProjectSession(
        string filePath,
        ProjectFileDocument document,
        DrawingDocument domain,
        ProjectLayoutSnapshot layout,
        ProjectProfessionalSnapshot professional,
        bool isDirty,
        int openedFormatVersion = ProjectFileFormat.CurrentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(professional);
        if (!ProjectFileFormat.IsSupportedVersion(openedFormatVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(openedFormatVersion));
        }

        FilePath = Path.GetFullPath(filePath);
        Document = document;
        Domain = domain;
        Layout = layout;
        Professional = professional;
        IsDirty = isDirty;
        OpenedFormatVersion = openedFormatVersion;
    }

    public string FilePath { get; }

    public ProjectFileDocument Document { get; }

    public DrawingDocument Domain { get; }

    public ProjectLayoutSnapshot Layout { get; init; }

    public ProjectProfessionalSnapshot Professional { get; init; }

    public ProjectFileManifest Manifest => Document.Manifest;

    public ProjectFileMetadata Metadata => Document.Metadata;

    public Guid ProjectId => Manifest.ProjectId;

    public bool IsDirty { get; init; }

    public int OpenedFormatVersion { get; }

    public bool RequiresUpgradeSaveAs =>
        OpenedFormatVersion < ProjectFileFormat.CurrentVersion;
}
