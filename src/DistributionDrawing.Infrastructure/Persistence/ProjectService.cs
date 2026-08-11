namespace DistributionDrawing.Infrastructure.Persistence;

/// <summary>
/// Coordinates the minimal project create, save, and load lifecycle.
/// Domain and Layout persistence are intentionally not part of this phase.
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

        ProjectSession candidate = new(filePath, document, isDirty: false);
        Current = candidate;
        return candidate;
    }

    public ProjectSession SaveProject()
    {
        ProjectSession current = RequireCurrent();

        _container.Save(current.FilePath, current.Document);

        // Reopen the written archive so the session observes the persisted
        // manifest timestamps and validates the complete container round trip.
        ProjectFileDocument persistedDocument = _container.Open(current.FilePath);
        ProjectSession candidate = new(
            current.FilePath,
            persistedDocument,
            isDirty: false);

        Current = candidate;
        return candidate;
    }

    public ProjectSession LoadProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Build and validate the candidate before replacing the current session.
        ProjectFileDocument document = _container.Open(filePath);
        ProjectSession candidate = new(filePath, document, isDirty: false);
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

    private ProjectSession RequireCurrent()
    {
        return Current
            ?? throw new InvalidOperationException("No project is currently open.");
    }
}
