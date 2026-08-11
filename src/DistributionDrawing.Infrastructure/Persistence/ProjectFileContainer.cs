using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed class ProjectFileContainer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public ProjectFileDocument Create(
        string filePath,
        Guid projectId,
        ProjectFileMetadata metadata,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(metadata);

        ProjectFileDocument document = ProjectFileDocument.CreateEmpty(
            projectId,
            metadata,
            createdAtUtc);
        Save(filePath, document);
        return Open(filePath);
    }

    public void Save(string filePath, ProjectFileDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);

        ValidateManifest(document.Manifest);
        ProjectFileDocument normalizedDocument = document with
        {
            Manifest = document.Manifest with
            {
                FormatVersion = ProjectFileFormat.CurrentVersion
            },
            Professional = document.Professional ??
                ProjectProfessionalDto.Empty(document.Manifest.ProjectId)
        };
        ValidateDocument(normalizedDocument);

        string targetPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(targetPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Project file directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        ProjectFileManifest manifest = normalizedDocument.Manifest with
        {
            SavedAtUtc = DateTimeOffset.UtcNow
        };
        ProjectFileDocument savedDocument = normalizedDocument with { Manifest = manifest };

        try
        {
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteJsonEntry(archive, ProjectFileFormat.ManifestEntryName, savedDocument.Manifest);
                WriteJsonEntry(
                    archive,
                    ProjectFileFormat.DocumentEntryName,
                    new ProjectFilePayload(
                        savedDocument.Manifest.ProjectId,
                        savedDocument.Metadata,
                        savedDocument.Domain,
                        savedDocument.Layout,
                        savedDocument.Professional));
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ProjectFileDocument Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string targetPath = Path.GetFullPath(filePath);
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Project file was not found.", targetPath);
        }

        using FileStream stream = new(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        ValidateArchiveEntries(archive);

        ProjectFileManifest manifest = ReadJsonEntry<ProjectFileManifest>(
            archive,
            ProjectFileFormat.ManifestEntryName);
        ValidateManifest(manifest);

        ProjectFilePayload payload = ReadJsonEntry<ProjectFilePayload>(
            archive,
            manifest.MainEntry);
        if (payload.ProjectId != manifest.ProjectId)
        {
            throw new InvalidDataException(
                "Project ID in document.json does not match manifest.json.");
        }

        if (payload.Metadata is null)
        {
            throw new InvalidDataException("Project metadata is required.");
        }

        if (payload.Domain is { } domain &&
            (domain.DocumentId != manifest.ProjectId ||
             !string.Equals(domain.Title, payload.Metadata.Title, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Domain document identity does not match the project manifest and metadata.");
        }

        if (payload.Layout is { } layout &&
            layout.DocumentId != manifest.ProjectId)
        {
            throw new InvalidDataException(
                "Layout document ID does not match the project manifest.");
        }

        if (manifest.FormatVersion == ProjectFileFormat.CurrentVersion &&
            payload.Professional is null)
        {
            throw new InvalidDataException(
                "The current project format requires a Professional section.");
        }

        ProjectProfessionalDto professional = manifest.FormatVersion ==
            ProjectFileFormat.PreviousVersion
            ? ProjectProfessionalDto.Empty(manifest.ProjectId)
            : payload.Professional!;
        if (professional.DocumentId != manifest.ProjectId)
        {
            throw new InvalidDataException(
                "Professional document ID does not match the project manifest.");
        }

        ProjectFileManifest effectiveManifest = manifest.FormatVersion ==
            ProjectFileFormat.PreviousVersion
            ? manifest with { FormatVersion = ProjectFileFormat.CurrentVersion }
            : manifest;

        return new ProjectFileDocument(
            effectiveManifest,
            payload.Metadata,
            payload.Domain,
            payload.Layout,
            professional);
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static T ReadJsonEntry<T>(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"Required entry '{entryName}' is missing.");
        using Stream stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Entry '{entryName}' is empty or invalid.");
    }

    private static void ValidateDocument(ProjectFileDocument document)
    {
        ValidateManifest(document.Manifest);
        if (string.IsNullOrWhiteSpace(document.Metadata.Title))
        {
            throw new InvalidDataException("Project metadata title is required.");
        }

        if (document.Domain is { } domain &&
            (domain.DocumentId != document.Manifest.ProjectId ||
             !string.Equals(domain.Title, document.Metadata.Title, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Domain document identity does not match the project manifest and metadata.");
        }

        if (document.Layout is { } layout &&
            layout.DocumentId != document.Manifest.ProjectId)
        {
            throw new InvalidDataException(
                "Layout document ID does not match the project manifest.");
        }

        if (document.Professional is not { } professional ||
            professional.DocumentId != document.Manifest.ProjectId)
        {
            throw new InvalidDataException(
                "Professional document identity does not match the project manifest.");
        }
    }

    private static void ValidateManifest(ProjectFileManifest manifest)
    {
        if (!string.Equals(manifest.FormatId, ProjectFileFormat.FormatId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported project format '{manifest.FormatId}'.");
        }

        if (manifest.FormatVersion != ProjectFileFormat.PreviousVersion &&
            manifest.FormatVersion != ProjectFileFormat.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported project format version '{manifest.FormatVersion}'.");
        }

        if (manifest.ProjectId == Guid.Empty)
        {
            throw new InvalidDataException("Project ID cannot be empty.");
        }

        if (!string.Equals(
                manifest.MainEntry,
                ProjectFileFormat.DocumentEntryName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The project main entry is not supported.");
        }
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"Duplicate archive entry '{entry.FullName}'.");
            }

            if (entry.FullName.Contains("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(entry.FullName) ||
                entry.FullName.Contains('\\'))
            {
                throw new InvalidDataException(
                    $"Archive entry path '{entry.FullName}' is not allowed.");
            }

            if (!string.Equals(entry.FullName, ProjectFileFormat.ManifestEntryName, StringComparison.Ordinal) &&
                !string.Equals(entry.FullName, ProjectFileFormat.DocumentEntryName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unsupported archive entry '{entry.FullName}'.");
            }
        }

        if (!names.Contains(ProjectFileFormat.ManifestEntryName) ||
            !names.Contains(ProjectFileFormat.DocumentEntryName))
        {
            throw new InvalidDataException("The project archive is missing required entries.");
        }
    }

    private sealed record ProjectFilePayload(
        Guid ProjectId,
        ProjectFileMetadata Metadata,
        ProjectDomainDto? Domain,
        ProjectLayoutDto? Layout,
        ProjectProfessionalDto? Professional = null);
}
