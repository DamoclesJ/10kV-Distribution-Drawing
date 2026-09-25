using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Application.WorkTickets;

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

        DrawingDocument domain = RestoreDomain(
            document,
            TransformerNamingContractMode.Current);
        ProjectProfessionalSnapshot professional = RestoreProfessional(document, domain);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(
            filePath,
            document,
            domain,
            layout,
            professional,
            isDirty: false,
            openedFormatVersion: ProjectFileFormat.CurrentVersion,
            transformerNamingMode: TransformerNamingContractMode.Current,
            workTickets: RestoreWorkTickets(document, domain));
        Current = candidate;
        return candidate;
    }

    public ProjectSession SaveProject()
    {
        ProjectSession current = RequireCurrent();
        return SaveProject(current.FilePath, current.Layout);
    }

    public ProjectSession SaveProject(ProjectLayoutSnapshot layout)
    {
        ProjectSession current = RequireCurrent();
        return SaveProject(current.FilePath, layout);
    }

    public ProjectSession SaveProjectAs(string filePath, ProjectLayoutSnapshot layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ProjectSession current = RequireCurrent();
        return SaveProject(Path.GetFullPath(filePath), layout);
    }

    private ProjectSession SaveProject(string filePath, ProjectLayoutSnapshot layout)
    {
        ProjectSession current = RequireCurrent();
        ArgumentNullException.ThrowIfNull(layout);
        ValidateTransformerNamingForSave(current.Domain);

        ProjectFileDocument snapshot = current.Document with
        {
            Metadata = new ProjectFileMetadata(
                current.Domain.Title,
                current.Metadata.Description),
            Domain = ProjectDomainMapper.ToDto(current.Domain),
            Layout = ProjectLayoutMapper.ToDto(current.Domain, layout),
            Professional = ProjectProfessionalMapper.ToDto(current.Domain),
            WorkTicketData = ProjectWorkTicketMapper.ToDto(current.WorkTickets, current.Domain)
        };
        _container.Save(filePath, snapshot);

        // Reopen the written archive so the session observes the persisted
        // manifest timestamps and validates the complete container round trip.
        ProjectFileOpenResult persisted = _container.OpenWithSource(filePath);
        ProjectFileDocument persistedDocument = persisted.Document;
        DrawingDocument validationDomain = RestoreDomain(
            persistedDocument,
            persisted.TransformerNamingMode);
        _ = RestoreProfessional(persistedDocument, validationDomain);
        _ = RestoreWorkTickets(persistedDocument, validationDomain);
        _ = RestoreLayout(persistedDocument, validationDomain);
        ProjectSession candidate = new(
            filePath,
            persistedDocument,
            current.Domain,
            layout,
            new ProjectProfessionalSnapshot(ProjectProfessionalMapper.ToDto(current.Domain)),
            isDirty: false,
            openedFormatVersion: persisted.OpenedFormatVersion,
            transformerNamingMode: persisted.TransformerNamingMode,
            workTickets: current.WorkTickets);

        Current = candidate;
        return candidate;
    }

    public ProjectSession LoadProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Build and validate the candidate before replacing the current session.
        ProjectFileOpenResult opened = _container.OpenWithSource(filePath);
        ProjectFileDocument document = opened.Document;
        DrawingDocument domain = RestoreDomain(document, opened.TransformerNamingMode);
        ProjectProfessionalSnapshot professional = RestoreProfessional(document, domain);
        ProjectLayoutSnapshot layout = RestoreLayout(document, domain);
        ProjectSession candidate = new(
            filePath,
            document,
            domain,
            layout,
            professional,
            isDirty: false,
            openedFormatVersion: opened.OpenedFormatVersion,
            transformerNamingMode: opened.TransformerNamingMode,
            workTickets: RestoreWorkTickets(document, domain));
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

    private static DrawingDocument RestoreDomain(
        ProjectFileDocument document,
        TransformerNamingContractMode transformerNamingMode)
    {
        ProjectDomainDto domain = document.Domain ?? ProjectDomainDto.Empty(
            document.Manifest.ProjectId,
            document.Metadata.Title);
        return ProjectDomainMapper.ToDomain(domain, transformerNamingMode);
    }

    private static void ValidateTransformerNamingForSave(DrawingDocument document)
    {
        int incompleteCount = document.Transformers.Count(transformer =>
            transformer.IsLegacyNamingIncomplete ||
            string.IsNullOrWhiteSpace(transformer.DisplayName));
        if (incompleteCount > 0)
        {
            throw new InvalidOperationException(
                $"工程中仍有 {incompleteCount} 台历史变压器未补录“变压器名称”，请全部补录后再保存。");
        }
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

    private static WorkTicketDataRoot RestoreWorkTickets(ProjectFileDocument document, DrawingDocument domain) =>
        ProjectWorkTicketMapper.ToRoot(document.WorkTicketData, domain);
}
