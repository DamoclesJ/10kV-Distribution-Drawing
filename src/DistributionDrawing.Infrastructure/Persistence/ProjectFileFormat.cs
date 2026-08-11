namespace DistributionDrawing.Infrastructure.Persistence;

public static class ProjectFileFormat
{
    public const string FormatId = "distribution-drawing-project";

    public const int PreviousVersion = 1;

    public const int CurrentVersion = 2;

    public const string ManifestEntryName = "manifest.json";

    public const string DocumentEntryName = "document.json";
}
