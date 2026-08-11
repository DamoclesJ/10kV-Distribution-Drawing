using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectProfessionalDto(
    Guid DocumentId,
    IReadOnlyList<ProjectWorkScopeDto> WorkScopes,
    IReadOnlyList<ProjectGroundingPointDto> GroundingPoints)
{
    public static ProjectProfessionalDto Empty(Guid documentId)
    {
        return new ProjectProfessionalDto(documentId, [], []);
    }
}

public sealed record ProjectWorkScopeDto(
    Guid WorkScopeId,
    ProjectBoundaryPointDto StartBoundary,
    ProjectBoundaryPointDto EndBoundary,
    string Description,
    IReadOnlyList<Guid> GroundingPointIds);

public sealed record ProjectBoundaryPointDto(
    Guid DeviceId,
    Guid TerminalId,
    string Side);

public sealed record ProjectGroundingPointDto(
    Guid GroundingPointId,
    Guid TerminalId,
    string Location,
    string? Number,
    string? Note);

/// <summary>
/// Validated, persistence-neutral Professional snapshot. It contains no
/// Domain object references and no Layout or Rendering state.
/// </summary>
public sealed record ProjectProfessionalSnapshot(ProjectProfessionalDto Data)
{
    public Guid DocumentId => Data.DocumentId;

    public IReadOnlyList<ProjectWorkScopeDto> WorkScopes => Data.WorkScopes;

    public IReadOnlyList<ProjectGroundingPointDto> GroundingPoints => Data.GroundingPoints;

    public static ProjectProfessionalSnapshot Empty(Guid documentId)
    {
        return new ProjectProfessionalSnapshot(ProjectProfessionalDto.Empty(documentId));
    }
}

internal static class ProjectProfessionalMapper
{
    public static ProjectProfessionalDto ToDto(DrawingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ProjectProfessionalDto(
            document.Id,
            document.WorkScopes
                .Select(workScope => new ProjectWorkScopeDto(
                    workScope.WorkScopeId,
                    ToDto(workScope.StartBoundary),
                    ToDto(workScope.EndBoundary),
                    workScope.Description,
                    workScope.GroundingPointIds.ToArray()))
                .ToArray(),
            document.GroundingPoints
                .Select(groundingPoint => new ProjectGroundingPointDto(
                    groundingPoint.GroundingPointId,
                    groundingPoint.TerminalId,
                    groundingPoint.Location,
                    groundingPoint.Number,
                    groundingPoint.Note))
                .ToArray());
    }

    public static ProjectProfessionalSnapshot ToSnapshot(
        DrawingDocument document,
        ProjectProfessionalDto? dto)
    {
        ArgumentNullException.ThrowIfNull(document);

        ProjectProfessionalDto professional = dto ?? ProjectProfessionalDto.Empty(document.Id);
        ValidateStructure(document, professional);

        foreach (ProjectGroundingPointDto groundingPoint in professional.GroundingPoints)
        {
            document.CreateGroundingPoint(
                groundingPoint.GroundingPointId,
                groundingPoint.TerminalId,
                groundingPoint.Location,
                groundingPoint.Number,
                groundingPoint.Note);
        }

        foreach (ProjectWorkScopeDto workScope in professional.WorkScopes)
        {
            document.CreateWorkScope(
                workScope.WorkScopeId,
                ToDomain(workScope.StartBoundary),
                ToDomain(workScope.EndBoundary),
                workScope.Description,
                workScope.GroundingPointIds);
        }

        return new ProjectProfessionalSnapshot(professional);
    }

    public static ProjectProfessionalDto ToDto(
        DrawingDocument document,
        ProjectProfessionalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(snapshot);

        ValidateStructure(document, snapshot.Data);
        return snapshot.Data;
    }

    private static void ValidateStructure(
        DrawingDocument document,
        ProjectProfessionalDto professional)
    {
        if (professional.DocumentId != document.Id)
        {
            throw new InvalidDataException(
                "Professional document ID does not match the Domain document.");
        }

        IReadOnlyList<ProjectWorkScopeDto> workScopes = professional.WorkScopes
            ?? throw new InvalidDataException("Professional work scopes are required.");
        IReadOnlyList<ProjectGroundingPointDto> groundingPoints = professional.GroundingPoints
            ?? throw new InvalidDataException("Professional grounding points are required.");

        EnsureUnique(
            workScopes.Select(workScope => workScope.WorkScopeId),
            "work scope");
        EnsureUnique(
            groundingPoints.Select(groundingPoint => groundingPoint.GroundingPointId),
            "grounding point");

        HashSet<Guid> groundingPointIds = groundingPoints
            .Select(groundingPoint => groundingPoint.GroundingPointId)
            .ToHashSet();
        HashSet<Guid> groundingTerminalIds = [];
        foreach (ProjectGroundingPointDto groundingPoint in groundingPoints)
        {
            if (groundingPoint.TerminalId == Guid.Empty)
            {
                throw new InvalidDataException(
                    "A grounding point terminal ID cannot be empty.");
            }

            if (!groundingTerminalIds.Add(groundingPoint.TerminalId))
            {
                throw new InvalidDataException(
                    $"Terminal '{groundingPoint.TerminalId}' has duplicate grounding points.");
            }

            if (string.IsNullOrWhiteSpace(groundingPoint.Location))
            {
                throw new InvalidDataException("A grounding point location is required.");
            }
        }

        foreach (ProjectWorkScopeDto workScope in workScopes)
        {
            if (workScope.StartBoundary is null || workScope.EndBoundary is null)
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' requires two boundaries.");
            }

            if (workScope.GroundingPointIds is null)
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' grounding point IDs are required.");
            }

            if (workScope.GroundingPointIds.Distinct().Count() !=
                workScope.GroundingPointIds.Count)
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' has duplicate grounding point IDs.");
            }

            if (workScope.GroundingPointIds.Any(id => !groundingPointIds.Contains(id)))
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' references a missing grounding point.");
            }

            ValidateBoundaryStructure(workScope.StartBoundary, workScope.WorkScopeId);
            ValidateBoundaryStructure(workScope.EndBoundary, workScope.WorkScopeId);
            if (workScope.StartBoundary.TerminalId == workScope.EndBoundary.TerminalId)
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' has duplicate boundary terminals.");
            }

            if (string.IsNullOrWhiteSpace(workScope.Description))
            {
                throw new InvalidDataException(
                    $"Work scope '{workScope.WorkScopeId}' description is required.");
            }
        }

        // Domain-level creation performs the authoritative Device/Terminal
        // ownership and global ID checks when the candidate is restored.
        _ = document;
    }

    private static void ValidateBoundaryStructure(
        ProjectBoundaryPointDto boundary,
        Guid workScopeId)
    {
        if (boundary.DeviceId == Guid.Empty || boundary.TerminalId == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Work scope '{workScopeId}' contains an empty boundary reference.");
        }

        if (string.IsNullOrWhiteSpace(boundary.Side))
        {
            throw new InvalidDataException(
                $"Work scope '{workScopeId}' contains an empty boundary side.");
        }
    }

    private static BoundaryPoint ToDomain(ProjectBoundaryPointDto boundary)
    {
        return new BoundaryPoint(boundary.DeviceId, boundary.TerminalId, boundary.Side);
    }

    private static ProjectBoundaryPointDto ToDto(BoundaryPoint boundary)
    {
        return new ProjectBoundaryPointDto(
            boundary.DeviceId,
            boundary.TerminalId,
            boundary.Side);
    }

    private static void EnsureUnique(IEnumerable<Guid> ids, string objectName)
    {
        Guid[] values = ids.ToArray();
        if (values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length)
        {
            throw new InvalidDataException($"Professional {objectName} IDs must be unique and non-empty.");
        }
    }
}
