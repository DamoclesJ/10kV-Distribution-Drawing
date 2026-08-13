namespace DistributionDrawing.Infrastructure.Persistence;

public static class ProjectFileFormat
{
    public const string FormatId = "distribution-drawing-project";

    public const int Version1 = 1;

    public const int Version2 = 2;

    public const int CurrentVersion = 3;

    public const string ManifestEntryName = "manifest.json";

    public const string DocumentEntryName = "document.json";

    public static bool IsSupportedVersion(int version)
    {
        return version is >= Version1 and <= CurrentVersion;
    }
}
