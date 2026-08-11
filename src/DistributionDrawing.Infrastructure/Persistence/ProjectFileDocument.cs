namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectFileDocument(
    ProjectFileManifest Manifest,
    ProjectFileMetadata Metadata,
    ProjectDomainDto? Domain = null,
    ProjectLayoutDto? Layout = null,
    ProjectProfessionalDto? Professional = null)
{
    public static ProjectFileDocument CreateEmpty(
        Guid projectId,
        ProjectFileMetadata metadata,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        DateTimeOffset created = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new ProjectFileDocument(
            ProjectFileManifest.Create(projectId, created, created),
            metadata,
            ProjectDomainDto.Empty(projectId, metadata.Title),
            ProjectLayoutDto.Empty(projectId),
            ProjectProfessionalDto.Empty(projectId));
    }
}
