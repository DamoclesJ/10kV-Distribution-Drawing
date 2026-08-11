namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectFileManifest(
    string FormatId,
    int FormatVersion,
    Guid ProjectId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset SavedAtUtc,
    string MainEntry)
{
    public static ProjectFileManifest Create(
        Guid projectId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset savedAtUtc)
    {
        return new ProjectFileManifest(
            ProjectFileFormat.FormatId,
            ProjectFileFormat.CurrentVersion,
            projectId,
            createdAtUtc.ToUniversalTime(),
            savedAtUtc.ToUniversalTime(),
            ProjectFileFormat.DocumentEntryName);
    }
}
