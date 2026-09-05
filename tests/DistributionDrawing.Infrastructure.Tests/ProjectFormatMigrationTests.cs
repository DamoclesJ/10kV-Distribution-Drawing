using System.Text.Json.Nodes;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectFormatMigrationTests
{
    [Fact]
    public void CurrentVersion_IsVersion7()
    {
        Assert.Equal(ProjectFileFormat.Version7, ProjectFileFormat.CurrentVersion);
        Assert.Equal(7, ProjectFileFormat.CurrentVersion);
    }

    [Theory]
    [InlineData(ProjectFileFormat.Version1)]
    [InlineData(ProjectFileFormat.Version2)]
    public void LegacyMigration_AppliesSequentialStepsAndProducesV7Shape(int sourceVersion)
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
        Assert.Empty(Assert.IsType<JsonArray>(domain["transformers"]));
        Assert.Empty(Assert.IsType<JsonArray>(domain["customerStations"]));
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
            Assert.Empty(Assert.IsType<JsonArray>(professional["groundingAccessPoints"]));
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
    public void Version6Payload_AddsV7SlotsAndTypedIntervalContract()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        Guid terminalId = Guid.NewGuid();
        Guid switchId = Guid.NewGuid();
        JsonObject interval = GetInterval(payload);
        interval["externalTerminalId"] = terminalId;
        interval["switches"] = new JsonArray
        {
            new JsonObject { ["deviceId"] = switchId }
        };

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version6,
            Guid.NewGuid());

        JsonObject migratedDomain = Assert.IsType<JsonObject>(migrated["domain"]);
        Assert.Empty(Assert.IsType<JsonArray>(migratedDomain["transformers"]));
        Assert.Empty(Assert.IsType<JsonArray>(migratedDomain["customerStations"]));
        JsonObject migratedInterval = GetInterval(migrated);
        Assert.Equal(terminalId, migratedInterval["cableTerminalId"]!.GetValue<Guid>());
        Assert.False(migratedInterval.ContainsKey("externalTerminalId"));
        JsonObject migratedSwitch = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(migratedInterval["switches"])));
        JsonObject owner = Assert.IsType<JsonObject>(migratedSwitch["owner"]);
        Assert.Equal("RingCabinetInterval", owner["ownerKind"]!.GetValue<string>());
        Assert.Equal(
            migratedInterval["intervalId"]!.GetValue<string>(),
            owner["ownerId"]!.GetValue<string>());
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
    public void Version6Migration_PreservesExistingTopologyAndLayoutFacts()
    {
        JsonObject payload = CreatePayload("负1间隔", "load-switch-interval", 1);
        JsonObject domain = Assert.IsType<JsonObject>(payload["domain"]);
        var cableSegments = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "22222222-2222-2222-2222-222222222222",
                ["connectionId"] = "33333333-3333-3333-3333-333333333333",
                ["startTerminalId"] = "44444444-4444-4444-4444-444444444444",
                ["endTerminalId"] = "55555555-5555-5555-5555-555555555555"
            }
        };
        var overheadLines = new JsonArray
        {
            new JsonObject
            {
                ["connectionId"] = "66666666-6666-6666-6666-666666666666",
                ["supportPoleIds"] = new JsonArray(
                    "77777777-7777-7777-7777-777777777777")
            }
        };
        domain["cableSegments"] = cableSegments;
        domain["overheadLines"] = overheadLines;
        domain["switchDevices"] = new JsonArray
        {
            new JsonObject
            {
                ["deviceId"] = "88888888-8888-8888-8888-888888888888",
                ["installationType"] = "pole"
            }
        };
        payload["layout"] = new JsonObject
        {
            ["documentId"] = Guid.NewGuid(),
            ["coordinateUnit"] = "mm",
            ["ringCabinets"] = new JsonArray(),
            ["poles"] = new JsonArray
            {
                new JsonObject
                {
                    ["poleId"] = "77777777-7777-7777-7777-777777777777",
                    ["position"] = new JsonObject
                    {
                        ["xMillimeters"] = 10,
                        ["yMillimeters"] = 20
                    }
                }
            },
            ["attachments"] = new JsonArray(),
            ["overheadLines"] = new JsonArray(),
            ["cableRouteGuides"] = new JsonArray()
        };
        JsonNode expectedCables = cableSegments.DeepClone();
        JsonNode expectedOverhead = overheadLines.DeepClone();
        JsonNode expectedPoles = payload["layout"]!["poles"]!.DeepClone();

        JsonObject migrated = ProjectFormatMigration.Migrate(
            payload,
            ProjectFileFormat.Version6,
            Guid.NewGuid());

        JsonObject migratedDomain = Assert.IsType<JsonObject>(migrated["domain"]);
        Assert.True(JsonNode.DeepEquals(expectedCables, migratedDomain["cableSegments"]));
        Assert.True(JsonNode.DeepEquals(expectedOverhead, migratedDomain["overheadLines"]));
        Assert.True(JsonNode.DeepEquals(
            expectedPoles,
            Assert.IsType<JsonObject>(migrated["layout"])["poles"]));
        Assert.False(Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(migratedDomain["switchDevices"]))).ContainsKey("owner"));
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
