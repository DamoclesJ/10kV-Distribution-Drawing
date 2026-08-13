using System.Text.Json.Nodes;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectFormatMigrationTests
{
    [Fact]
    public void Version2Migration_AddsDeterministicBayMetadataWithoutInferringFunction()
    {
        JsonObject payload = CreatePayload(
            displayName: "进线联络间隔",
            intervalKind: "integrated-feeder-interval",
            sequence: 5);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version2,
            Guid.NewGuid());
        JsonObject interval = GetInterval(migrated);

        Assert.Equal(5, interval["bayIndex"]!.GetValue<int>());
        Assert.Equal("unknown", interval["function"]!.GetValue<string>());
        Assert.Equal("进线联络间隔", interval["displayName"]!.GetValue<string>());
        Assert.Equal(
            "integrated-feeder-interval",
            interval["intervalKind"]!.GetValue<string>());
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            interval["intervalId"]!.GetValue<string>());
    }

    [Fact]
    public void Version1Migration_AppliesProfessionalAndBayMetadataSteps()
    {
        Guid projectId = Guid.NewGuid();
        JsonObject payload = CreatePayload("负荷开关间隔", "load-switch-interval", 3);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version1,
            projectId);

        JsonObject professional = Assert.IsType<JsonObject>(migrated["professional"]);
        Assert.Equal(projectId, professional["documentId"]!.GetValue<Guid>());
        Assert.Empty(Assert.IsType<JsonArray>(professional["workScopes"]));
        Assert.Empty(Assert.IsType<JsonArray>(professional["groundingPoints"]));
        Assert.Equal(3, GetInterval(migrated)["bayIndex"]!.GetValue<int>());
        Assert.Equal("unknown", GetInterval(migrated)["function"]!.GetValue<string>());
    }

    [Fact]
    public void Version3Payload_IsNotGivenMissingBayMetadata()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.CurrentVersion,
            Guid.NewGuid());
        JsonObject interval = GetInterval(migrated);

        Assert.False(interval.ContainsKey("bayIndex"));
        Assert.False(interval.ContainsKey("function"));
    }

    private static JsonObject CreatePayload(
        string displayName,
        string intervalKind,
        int sequence)
    {
        return new JsonObject
        {
            ["domain"] = new JsonObject
            {
                ["ringCabinets"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["intervals"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["intervalId"] = "11111111-1111-1111-1111-111111111111",
                                ["sequence"] = sequence,
                                ["displayName"] = displayName,
                                ["intervalKind"] = intervalKind
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject GetInterval(JsonObject payload)
    {
        var domain = Assert.IsType<JsonObject>(payload["domain"]);
        var cabinets = Assert.IsType<JsonArray>(domain["ringCabinets"]);
        var cabinet = Assert.IsType<JsonObject>(Assert.Single(cabinets));
        var intervals = Assert.IsType<JsonArray>(cabinet["intervals"]);
        return Assert.IsType<JsonObject>(Assert.Single(intervals));
    }
}
