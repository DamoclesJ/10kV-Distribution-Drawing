using System.Text.Json.Nodes;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectFormatMigrationTests
{
    [Fact]
    public void CurrentVersion_IsVersion6()
    {
        Assert.Equal(ProjectFileFormat.Version6, ProjectFileFormat.CurrentVersion);
        Assert.Equal(6, ProjectFileFormat.CurrentVersion);
    }

    [Theory]
    [InlineData(ProjectFileFormat.Version1)]
    [InlineData(ProjectFileFormat.Version2)]
    public void LegacyMigration_AppliesSequentialStepsAndProducesV6Shape(int sourceVersion)
    {
        Guid projectId = Guid.NewGuid();
        JsonObject payload = CreatePayload(
            displayName: "负荷开关间隔",
            intervalKind: "load-switch-interval",
            sequence: 3);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            sourceVersion,
            projectId);
        JsonObject interval = GetInterval(migrated);

        Assert.Equal(3, interval["bayIndex"]!.GetValue<int>());
        Assert.False(interval.ContainsKey("function"));
        JsonObject domain = Assert.IsType<JsonObject>(migrated["domain"]);
        Assert.Empty(Assert.IsType<JsonArray>(domain["switchDevices"]));
        Assert.Equal("负荷开关间隔", interval["displayName"]!.GetValue<string>());
        Assert.Equal(
            "load-switch-interval",
            interval["intervalKind"]!.GetValue<string>());
        if (sourceVersion == ProjectFileFormat.Version1)
        {
            JsonObject professional = Assert.IsType<JsonObject>(migrated["professional"]);
            Assert.Equal(projectId, professional["documentId"]!.GetValue<Guid>());
            Assert.Empty(Assert.IsType<JsonArray>(professional["workScopes"]));
            Assert.Empty(Assert.IsType<JsonArray>(professional["groundingPoints"]));
        }
    }

    [Theory]
    [InlineData("\"unknown\"")]
    [InlineData("\"incoming\"")]
    [InlineData("\"outgoing\"")]
    [InlineData("\"tie\"")]
    [InlineData("\"pt\"")]
    [InlineData("\"metering\"")]
    [InlineData("\"reserve\"")]
    [InlineData("\"arbitrary-legacy-value\"")]
    [InlineData("123")]
    [InlineData("null")]
    public void Version3Migration_DiscardsFunctionWithoutParsing(string legacyJson)
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        JsonObject interval = GetInterval(payload);
        interval["bayIndex"] = 10;
        interval["function"] = JsonNode.Parse(legacyJson);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version3,
            Guid.NewGuid());

        JsonObject migratedInterval = GetInterval(migrated);
        Assert.Equal(10, migratedInterval["bayIndex"]!.GetValue<int>());
        Assert.False(migratedInterval.ContainsKey("function"));
    }

    [Fact]
    public void Version3Migration_AllowsMissingFunction()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        GetInterval(payload)["bayIndex"] = 8;

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version3,
            Guid.NewGuid());

        Assert.Equal(8, GetInterval(migrated)["bayIndex"]!.GetValue<int>());
        Assert.False(GetInterval(migrated).ContainsKey("function"));
    }

    [Fact]
    public void Version4Payload_IsNotGivenOrRequiredFunction()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        GetInterval(payload)["bayIndex"] = 1;

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version4,
            Guid.NewGuid());

        Assert.Equal(1, GetInterval(migrated)["bayIndex"]!.GetValue<int>());
        Assert.False(GetInterval(migrated).ContainsKey("function"));
        JsonObject domain = Assert.IsType<JsonObject>(migrated["domain"]);
        Assert.Empty(Assert.IsType<JsonArray>(domain["switchDevices"]));
    }

    [Fact]
    public void Version6Payload_DoesNotRunMigrationAgain()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        domain["switchDevices"] = new JsonArray
        {
            new JsonObject { ["deviceId"] = Guid.NewGuid() }
        };

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version6,
            Guid.NewGuid());

        Assert.Single(Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(migrated["domain"])["switchDevices"]));
    }

    [Fact]
    public void Version6Payload_PreservesPTIntervalKind()
    {
        JsonObject payload = CreatePayload("负7 PT间隔", "pt-interval", 1);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version6,
            Guid.NewGuid());

        Assert.Equal(
            "pt-interval",
            GetInterval(migrated)["intervalKind"]!.GetValue<string>());
    }

    [Fact]
    public void Version5Payload_AddsEmptyCableCollections()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version5,
            Guid.NewGuid());

        JsonObject domain = Assert.IsType<JsonObject>(migrated["domain"]);
        Assert.Empty(Assert.IsType<JsonArray>(domain["cableSegments"]));
        Assert.Empty(Assert.IsType<JsonArray>(domain["intermediateTerminals"]));
    }

    [Fact]
    public void Version4IntervalDto_DoesNotExposeFunction()
    {
        Assert.Null(typeof(ProjectRingCabinetIntervalDto).GetProperty("Function"));
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
                                ["intervalId"] =
                                    "11111111-1111-1111-1111-111111111111",
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
