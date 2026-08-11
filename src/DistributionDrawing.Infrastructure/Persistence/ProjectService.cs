using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Coordinates the project create, save, and load lifecycle.
/// This phase persists Domain and external topology data only; Layout and editor state
/// remain out of scope.
/// </summary>
public sealed class ProjectService
{
    private readonly ProjectFileContainer _container;

    public ProjectService(ProjectFileContainer? container = null)
    {
        _container = container ?? new ProjectFileContainer();
    }

    public ProjectSession? Current { get; private set; }

    public ProjectSession CreateProject(
        string filePath,
        string title,
        string? description = null,
        Guid? projectId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ProjectFileMetadata metadata = new(title, description);
        ProjectFileDocument document = _container.Create(
            filePath,
            projectId ?? Guid.NewGuid(),
            metadata,
            createdAtUtc);

        DrawingDocument domain = RestoreDomain(document);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(filePath, document, domain, layout, isDirty: false);
        Current = candidate;
        return candidate;
    }

    public ProjectSession SaveProject()
    {
        ProjectSession current = RequireCurrent();

        ProjectFileDocument snapshot = current.Document with
        {
            Metadata = new ProjectFileMetadata(
                current.Domain.Title,
                current.Metadata.Description),
            Domain = ProjectDomainMapper.ToDto(current.Domain),
            Layout = ProjectLayoutMapper.ToDto(current.Domain, current.Layout)
        };
        _container.Save(current.FilePath, snapshot);

        // Reopen the written archive so the session observes the persisted
        // manifest timestamps and validates the complete container round trip.
        ProjectFileDocument persistedDocument = _container.Open(current.FilePath);
        DrawingDocument domain = RestoreDomain(persistedDocument);
        ProjectLayoutSnapshot layout = RestoreLayout(persistedDocument, domain);
        ProjectSession candidate = new(
            current.FilePath,
            persistedDocument,
            domain,
            layout,
            isDirty: false);

        Current = candidate;
        return candidate;
    }

    public ProjectSession LoadProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Build and validate the candidate before replacing the current session.
        ProjectFileDocument document = _container.Open(filePath);
        DrawingDocument domain = RestoreDomain(document);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(filePath, document, domain, layout, isDirty: false);
        Current = candidate;
        return candidate;
    }

    /// <summary>
    /// Marks the lifecycle state dirty for future editor mutations without
    /// changing any persisted Domain or Layout data.
    /// </summary>
    public ProjectSession MarkDirty()
    {
        ProjectSession current = RequireCurrent();
        ProjectSession candidate = current with { IsDirty = true };
        Current = candidate;
        return candidate;
    }

    public ProjectSession SetLayout(ProjectLayoutSnapshot layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        ProjectSession current = RequireCurrent();
        ProjectLayoutMapper.ToDto(current.Domain, layout);
        ProjectSession candidate = current with
        {
            Layout = layout,
            IsDirty = true
        };
        Current = candidate;
        return candidate;
    }

    private ProjectSession RequireCurrent()
    {
        return Current
            ?? throw new InvalidOperationException("No project is currently open.");
    }

    private static DrawingDocument RestoreDomain(ProjectFileDocument document)
    {
        ProjectDomainDto domain = document.Domain ?? ProjectDomainDto.Empty(
            document.Manifest.ProjectId,
            document.Metadata.Title);
        return ProjectDomainMapper.ToDomain(domain);
    }

    private static ProjectLayoutSnapshot RestoreLayout(
        ProjectFileDocument document,
        DrawingDocument domain)
    {
        return ProjectLayoutMapper.ToSnapshot(domain, document.Layout);
    }
}
