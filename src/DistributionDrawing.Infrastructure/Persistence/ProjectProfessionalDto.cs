using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using System.Text.Json.Serialization;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectProfessionalDto(
    Guid DocumentId,
    IReadOnlyList<ProjectWorkScopeDto> WorkScopes,
    IReadOnlyList<ProjectGroundingPointDto> GroundingPoints,
    [property: JsonRequired]
    IReadOnlyList<ProjectGroundingAccessPointDto>? GroundingAccessPoints = null)
{
    public static ProjectProfessionalDto Empty(Guid documentId)
    {
        return new ProjectProfessionalDto(documentId, [], [], []);
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
    [property: JsonRequired] ProjectGroundingTargetDto GroundingTarget,
    string Location,
    string? Number,
    string? Note);

[JsonConverter(typeof(StrictStringEnumConverter<ProjectGroundingTargetKind>))]
public enum ProjectGroundingTargetKind
{
    Terminal,
    GroundingAccessPoint
}

public sealed record ProjectGroundingTargetDto(
    [property: JsonRequired] ProjectGroundingTargetKind Kind,
    [property: JsonRequired] Guid TargetId);

[JsonConverter(typeof(StrictStringEnumConverter<ProjectGroundingAccessLineSide>))]
public enum ProjectGroundingAccessLineSide
{
    SmallerNumberSide,
    LargerNumberSide
}

public sealed record ProjectGroundingAccessPointDto(
    [property: JsonRequired] Guid GroundingAccessPointId,
    [property: JsonRequired] Guid ConnectionId,
    [property: JsonRequired] Guid PoleId,
    [property: JsonRequired] ProjectGroundingAccessLineSide LineSide);

/// <summary>
/// Validated, persistence-neutral Professional snapshot. It contains no
/// Domain object references and no Layout or Rendering state.
/// </summary>
public sealed record ProjectProfessionalSnapshot(ProjectProfessionalDto Data)
{
    public Guid DocumentId => Data.DocumentId;

    public IReadOnlyList<ProjectWorkScopeDto> WorkScopes => Data.WorkScopes;

    public IReadOnlyList<ProjectGroundingPointDto> GroundingPoints => Data.GroundingPoints;

    public IReadOnlyList<ProjectGroundingAccessPointDto> GroundingAccessPoints =>
        Data.GroundingAccessPoints ?? [];

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
                    new ProjectGroundingTargetDto(
                        ProjectGroundingTargetKind.Terminal,
                        groundingPoint.TerminalId),
                    groundingPoint.Location,
                    groundingPoint.Number,
                    groundingPoint.Note))
                .ToArray(),
            []);
    }

    public static ProjectProfessionalSnapshot ToSnapshot(
        DrawingDocument document,
        ProjectProfessionalDto? dto)
    {
        ArgumentNullException.ThrowIfNull(document);

        ProjectProfessionalDto professional = dto ?? ProjectProfessionalDto.Empty(document.Id);
        ValidateStructure(document, professional);

        if ((professional.GroundingAccessPoints ?? []).Count != 0)
        {
            throw new InvalidDataException(
                "GroundingAccessPoint runtime support is not implemented in WP-EM-02.");
        }

        foreach (ProjectGroundingPointDto groundingPoint in professional.GroundingPoints)
        {
            if (groundingPoint.GroundingTarget.Kind != ProjectGroundingTargetKind.Terminal)
            {
                throw new InvalidDataException(
                    $"Grounding point '{groundingPoint.GroundingPointId}' uses a target that is not supported by the WP-EM-02 runtime.");
            }

            document.CreateGroundingPoint(
                groundingPoint.GroundingPointId,
                groundingPoint.GroundingTarget.TargetId,
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
        IReadOnlyList<ProjectGroundingAccessPointDto> groundingAccessPoints =
            professional.GroundingAccessPoints ?? throw new InvalidDataException(
                "Professional grounding access points are required.");

        EnsureUnique(
            workScopes.Select(workScope => workScope.WorkScopeId),
            "work scope");
        EnsureUnique(
            groundingPoints.Select(groundingPoint => groundingPoint.GroundingPointId),
            "grounding point");
        EnsureUnique(
            groundingAccessPoints.Select(point => point.GroundingAccessPointId),
            "grounding access point");

        HashSet<Guid> groundingPointIds = groundingPoints
            .Select(groundingPoint => groundingPoint.GroundingPointId)
            .ToHashSet();
        HashSet<(ProjectGroundingTargetKind Kind, Guid TargetId)> groundingTargets = [];
        foreach (ProjectGroundingPointDto groundingPoint in groundingPoints)
        {
            if (groundingPoint.GroundingTarget is null ||
                groundingPoint.GroundingTarget.TargetId == Guid.Empty)
            {
                throw new InvalidDataException(
                    "A grounding point target ID cannot be empty.");
            }

            if (!groundingTargets.Add((
                    groundingPoint.GroundingTarget.Kind,
                    groundingPoint.GroundingTarget.TargetId)))
            {
                throw new InvalidDataException(
                    $"Grounding target '{groundingPoint.GroundingTarget.TargetId}' has duplicate grounding points.");
            }

            if (string.IsNullOrWhiteSpace(groundingPoint.Location))
            {
                throw new InvalidDataException("A grounding point location is required.");
            }
        }

        foreach (ProjectGroundingAccessPointDto point in groundingAccessPoints)
        {
            if (point.ConnectionId == Guid.Empty || point.PoleId == Guid.Empty)
            {
                throw new InvalidDataException(
                    $"Grounding access point '{point.GroundingAccessPointId}' has an empty reference.");
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
