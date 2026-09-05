using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Coordinates the project create, save, and load lifecycle, including the
/// persistence-neutral Professional snapshot.
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
        ProjectProfessionalSnapshot professional = RestoreProfessional(document, domain);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(
            filePath,
            document,
            domain,
            layout,
            professional,
            isDirty: false,
            openedFormatVersion: ProjectFileFormat.CurrentVersion);
        Current = candidate;
        return candidate;
    }

    public ProjectSession SaveProject()
    {
        ProjectSession current = RequireCurrent();
        EnsureCanSaveInPlace(current);
        return SaveProject(current.FilePath, current.Layout);
    }

    public ProjectSession SaveProject(ProjectLayoutSnapshot layout)
    {
        ProjectSession current = RequireCurrent();
        EnsureCanSaveInPlace(current);
        return SaveProject(current.FilePath, layout);
    }

    public ProjectSession SaveProjectAs(string filePath, ProjectLayoutSnapshot layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ProjectSession current = RequireCurrent();
        string targetPath = Path.GetFullPath(filePath);
        if (current.RequiresUpgradeSaveAs &&
            StringComparer.OrdinalIgnoreCase.Equals(targetPath, current.FilePath))
        {
            throw new InvalidOperationException(
                "从旧格式打开的工程必须另存为新的 V7 文件，不能覆盖原文件。");
        }

        return SaveProject(targetPath, layout);
    }

    private ProjectSession SaveProject(string filePath, ProjectLayoutSnapshot layout)
    {
        ProjectSession current = RequireCurrent();
        ArgumentNullException.ThrowIfNull(layout);

        ProjectFileDocument snapshot = current.Document with
        {
            Metadata = new ProjectFileMetadata(
                current.Domain.Title,
                current.Metadata.Description),
            Domain = ProjectDomainMapper.ToDto(current.Domain),
            Layout = ProjectLayoutMapper.ToDto(current.Domain, layout),
            Professional = ProjectProfessionalMapper.ToDto(current.Domain)
        };
        _container.Save(filePath, snapshot);

        // Reopen the written archive so the session observes the persisted
        // manifest timestamps and validates the complete container round trip.
        ProjectFileOpenResult persisted = _container.OpenWithSource(filePath);
        ProjectFileDocument persistedDocument = persisted.Document;
        DrawingDocument validationDomain = RestoreDomain(persistedDocument);
        _ = RestoreProfessional(persistedDocument, validationDomain);
        _ = RestoreLayout(persistedDocument, validationDomain);
        ProjectSession candidate = new(
            filePath,
            persistedDocument,
            current.Domain,
            layout,
            new ProjectProfessionalSnapshot(ProjectProfessionalMapper.ToDto(current.Domain)),
            isDirty: false,
            openedFormatVersion: persisted.OpenedFormatVersion);

        Current = candidate;
        return candidate;
    }

    public ProjectSession LoadProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Build and validate the candidate before replacing the current session.
        ProjectFileOpenResult opened = _container.OpenWithSource(filePath);
        ProjectFileDocument document = opened.Document;
        DrawingDocument domain = RestoreDomain(document);
        ProjectProfessionalSnapshot professional = RestoreProfessional(document, domain);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(
            filePath,
            document,
            domain,
            layout,
            professional,
            isDirty: false,
            openedFormatVersion: opened.OpenedFormatVersion);
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

    private static void EnsureCanSaveInPlace(ProjectSession session)
    {
        if (session.RequiresUpgradeSaveAs)
        {
            throw new InvalidOperationException(
                $"该工程从 V{session.OpenedFormatVersion} 打开，首次保存必须使用“另存为”创建新的 V7 文件。");
        }
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

    private static ProjectProfessionalSnapshot RestoreProfessional(
        ProjectFileDocument document,
        DrawingDocument domain)
    {
        return ProjectProfessionalMapper.ToSnapshot(domain, document.Professional);
    }
}
