using System.Text.Json.Nodes;

namespace DistributionDrawing.Infrastructure.Persistence;

internal static class ProjectFormatMigration
{
    public static JsonObject Migrate(
        JsonObject payload,
        int sourceVersion,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!ProjectFileFormat.IsSupportedVersion(sourceVersion))
        {
            throw new InvalidDataException(
                $"Unsupported project format version '{sourceVersion}'.");
        }

        var migrated = (JsonObject)payload.DeepClone();
        int version = sourceVersion;

        if (version == ProjectFileFormat.Version1)
        {
            MigrateVersion1ToVersion2(migrated, projectId);
            version = ProjectFileFormat.Version2;
        }

        if (version == ProjectFileFormat.Version2)
        {
            MigrateVersion2ToVersion3(migrated);
            version = ProjectFileFormat.Version3;
        }

        if (version == ProjectFileFormat.Version3)
        {
            MigrateVersion3ToVersion4(migrated);
            version = ProjectFileFormat.Version4;
        }

        if (version == ProjectFileFormat.Version4)
        {
            MigrateVersion4ToVersion5(migrated);
            version = ProjectFileFormat.Version5;
        }

        if (version == ProjectFileFormat.Version5)
        {
            MigrateVersion5ToVersion6(migrated);
            version = ProjectFileFormat.Version6;
        }

        if (version != ProjectFileFormat.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Project format version '{sourceVersion}' could not be migrated.");
        }

        return migrated;
    }

    private static void MigrateVersion1ToVersion2(JsonObject payload, Guid projectId)
    {
        payload["professional"] = new JsonObject
        {
            ["documentId"] = projectId,
            ["workScopes"] = new JsonArray(),
            ["groundingPoints"] = new JsonArray()
        };
    }

    private static void MigrateVersion2ToVersion3(JsonObject payload)
    {
        if (payload["domain"] is null)
        {
            return;
        }

        JsonObject domain = RequireObject(payload["domain"], "domain");
        JsonArray cabinets = RequireArray(domain["ringCabinets"], "domain.ringCabinets");

        for (int cabinetIndex = 0; cabinetIndex < cabinets.Count; cabinetIndex++)
        {
            JsonObject cabinet = RequireObject(
                cabinets[cabinetIndex],
                $"domain.ringCabinets[{cabinetIndex}]");
            JsonArray intervals = RequireArray(
                cabinet["intervals"],
                $"domain.ringCabinets[{cabinetIndex}].intervals");

            for (int intervalIndex = 0; intervalIndex < intervals.Count; intervalIndex++)
            {
                string path =
                    $"domain.ringCabinets[{cabinetIndex}].intervals[{intervalIndex}]";
                JsonObject interval = RequireObject(intervals[intervalIndex], path);
                int sequence = RequireInt32(interval["sequence"], $"{path}.sequence");

                interval["bayIndex"] = sequence;
                interval["function"] = "unknown";
            }
        }
    }

    private static void MigrateVersion3ToVersion4(JsonObject payload)
    {
        if (payload["domain"] is null)
        {
            return;
        }

        JsonObject domain = RequireObject(payload["domain"], "domain");
        JsonArray cabinets = RequireArray(domain["ringCabinets"], "domain.ringCabinets");

        for (int cabinetIndex = 0; cabinetIndex < cabinets.Count; cabinetIndex++)
        {
            JsonObject cabinet = RequireObject(
                cabinets[cabinetIndex],
                $"domain.ringCabinets[{cabinetIndex}]");
            JsonArray intervals = RequireArray(
                cabinet["intervals"],
                $"domain.ringCabinets[{cabinetIndex}].intervals");

            for (int intervalIndex = 0; intervalIndex < intervals.Count; intervalIndex++)
            {
                string path =
                    $"domain.ringCabinets[{cabinetIndex}].intervals[{intervalIndex}]";
                JsonObject interval = RequireObject(intervals[intervalIndex], path);
                interval.Remove("function");
            }
        }
    }

    private static void MigrateVersion4ToVersion5(JsonObject payload)
    {
        if (payload["domain"] is not JsonObject domain)
        {
            return;
        }

        domain["switchDevices"] ??= new JsonArray();
    }

    private static void MigrateVersion5ToVersion6(JsonObject payload)
    {
        if (payload["domain"] is not JsonObject domain)
        {
            return;
        }

        domain["cableSegments"] ??= new JsonArray();
        domain["intermediateTerminals"] ??= new JsonArray();
    }

    private static JsonObject RequireObject(JsonNode? node, string path)
    {
        return node as JsonObject
            ?? throw new InvalidDataException($"Project payload field '{path}' must be an object.");
    }

    private static JsonArray RequireArray(JsonNode? node, string path)
    {
        return node as JsonArray
            ?? throw new InvalidDataException($"Project payload field '{path}' must be an array.");
    }

    private static int RequireInt32(JsonNode? node, string path)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out int value))
        {
            return value;
        }

        throw new InvalidDataException(
            $"Project payload field '{path}' must be a 32-bit integer.");
    }
}
