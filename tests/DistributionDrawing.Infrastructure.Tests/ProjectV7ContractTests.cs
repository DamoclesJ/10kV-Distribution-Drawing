using System.Text.Json;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class ProjectV7ContractTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void NewProject_WritesVersion7WithEmptyFoundationCollections()
    {
        string path = NextPath();
        var service = new ProjectService();

        ProjectSession session = service.CreateProject(path, "V7 空工程");

        Assert.Equal(ProjectFileFormat.Version7, session.Manifest.FormatVersion);
        Assert.Equal(ProjectFileFormat.Version7, session.OpenedFormatVersion);
        Assert.False(session.RequiresUpgradeSaveAs);
        Assert.Empty(session.Document.Domain!.Transformers!);
        Assert.Empty(session.Document.Domain.CustomerStations!);
        Assert.Empty(session.Professional.GroundingAccessPoints);
        Assert.Empty(session.Layout.TransformerLayouts);
        Assert.Empty(session.Layout.CustomerStationLayouts);
        Assert.Empty(session.Layout.GroundingPointLayouts);
    }

    [Fact]
    public void V7TypedFoundationContracts_RoundTripWithoutLosingData()
    {
        Guid projectId = Guid.NewGuid();
        Guid intervalId = Guid.NewGuid();
        Guid intervalTerminalId = Guid.NewGuid();
        Guid switchId = Guid.NewGuid();
        Guid transformerId = Guid.NewGuid();
        Guid transformerTerminalId = Guid.NewGuid();
        Guid stationId = Guid.NewGuid();
        Guid feederId = Guid.NewGuid();
        Guid groundingAccessPointId = Guid.NewGuid();
        Guid terminalGroundingPointId = Guid.NewGuid();
        Guid gapGroundingPointId = Guid.NewGuid();
        var cabinetSwitch = new ProjectSwitchDeviceDto(
            switchId,
            "load-switch",
            "cabinet-interval",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "open",
            "负荷开关",
            "10kV",
            null,
            new ProjectSwitchOwnerReferenceDto(
                ProjectSwitchOwnerKind.RingCabinetInterval,
                intervalId));
        var feederSwitch = new ProjectSwitchDeviceDto(
            Guid.NewGuid(),
            "isolation-switch",
            "customer-station-incoming-feeder",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "closed",
            "进线隔离开关",
            "10kV",
            "QS1",
            new ProjectSwitchOwnerReferenceDto(
                ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                feederId));
        ProjectDomainDto domain = ProjectDomainDto.Empty(projectId, "V7 合同") with
        {
            RingCabinets =
            [
                new ProjectRingCabinetDto(
                    Guid.NewGuid(),
                    "环网柜",
                    Guid.NewGuid(),
                    [new ProjectRingCabinetIntervalDto(
                        intervalId,
                        Guid.NewGuid(),
                        1,
                        1,
                        "负1间隔",
                        "load-switch-interval",
                        null,
                        null,
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        null,
                        Guid.NewGuid(),
                        [cabinetSwitch])],
                    [],
                    [])
            ],
            Transformers =
            [
                new ProjectTransformerDto(
                    transformerId,
                    ProjectTransformerKind.PublicIndoor,
                    transformerTerminalId)
            ],
            CustomerStations =
            [
                new ProjectCustomerStationDto(
                    stationId,
                    ProjectStationKind.IndoorStation,
                    [new ProjectCustomerStationIncomingFeederDto(
                        feederId,
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        feederSwitch)])
            ]
        };
        var professional = new ProjectProfessionalDto(
            projectId,
            [],
            [
                new ProjectGroundingPointDto(
                    terminalGroundingPointId,
                    new ProjectGroundingTargetDto(
                        ProjectGroundingTargetKind.Terminal,
                        intervalTerminalId),
                    "电缆侧",
                    "L01",
                    "terminal target"),
                new ProjectGroundingPointDto(
                    gapGroundingPointId,
                    new ProjectGroundingTargetDto(
                        ProjectGroundingTargetKind.GroundingAccessPoint,
                        groundingAccessPointId),
                    "小号侧",
                    "L02",
                    "gap target")
            ],
            [
                new ProjectGroundingAccessPointDto(
                    groundingAccessPointId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ProjectGroundingAccessLineSide.SmallerNumberSide)
            ]);
        var layout = new ProjectLayoutDto(
            projectId,
            "mm",
            [],
            [],
            [],
            [],
            [],
            [new ProjectTransformerLayoutDto(
                transformerId,
                new ProjectPointDto(100, 200),
                ProjectTransformerOrientation.Vertical)],
            [new ProjectCustomerStationLayoutDto(
                stationId,
                new ProjectPointDto(300, 400))],
            [new ProjectGroundingPointLayoutDto(
                gapGroundingPointId,
                new ProjectPointDto(12.5, -8.25))]);
        var document = new ProjectFileDocument(
            ProjectFileManifest.Create(projectId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new ProjectFileMetadata("V7 合同"),
            domain,
            layout,
            professional);
        string path = NextPath();
        var container = new ProjectFileContainer();

        container.Save(path, document);
        ProjectFileDocument opened = container.Open(path);

        ProjectRingCabinetIntervalDto interval = Assert.Single(
            Assert.Single(opened.Domain!.RingCabinets).Intervals);
        Assert.Null(interval.CableTerminalId);
        Assert.Equal(
            new ProjectSwitchOwnerReferenceDto(
                ProjectSwitchOwnerKind.RingCabinetInterval,
                intervalId),
            Assert.Single(interval.Switches).Owner);
        Assert.Equal(
            new ProjectTransformerDto(
                transformerId,
                ProjectTransformerKind.PublicIndoor,
                transformerTerminalId),
            Assert.Single(opened.Domain.Transformers!));
        ProjectCustomerStationDto station = Assert.Single(opened.Domain.CustomerStations!);
        Assert.Equal(ProjectStationKind.IndoorStation, station.StationKind);
        Assert.Equal(
            ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
            Assert.Single(station.IncomingFeeders).IsolationSwitch.Owner!.OwnerKind);
        Assert.Equal(
            ProjectGroundingTargetKind.Terminal,
            opened.Professional!.GroundingPoints[0].GroundingTarget.Kind);
        Assert.Equal(
            ProjectGroundingTargetKind.GroundingAccessPoint,
            opened.Professional.GroundingPoints[1].GroundingTarget.Kind);
        Assert.Equal(
            ProjectGroundingAccessLineSide.SmallerNumberSide,
            Assert.Single(opened.Professional.GroundingAccessPoints!).LineSide);
        Assert.Equal(
            ProjectTransformerOrientation.Vertical,
            Assert.Single(opened.Layout!.TransformerLayouts!).Orientation);
        Assert.Equal(
            new ProjectPointDto(12.5, -8.25),
            Assert.Single(opened.Layout.GroundingPointLayouts!).SymbolOffset);
    }

    [Fact]
    public void EmptyGroundingPointLayouts_RepresentsNoManualOverride()
    {
        ProjectLayoutDto layout = ProjectLayoutDto.Empty(Guid.NewGuid());

        Assert.Empty(layout.GroundingPointLayouts!);
    }

    [Theory]
    [InlineData(ProjectGroundingAccessLineSide.SmallerNumberSide)]
    [InlineData(ProjectGroundingAccessLineSide.LargerNumberSide)]
    public void GroundingAccessPointLineSide_RoundTripsAsTypedEnum(
        ProjectGroundingAccessLineSide lineSide)
    {
        var original = new ProjectGroundingAccessPointDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            lineSide);

        string json = JsonSerializer.Serialize(original);
        ProjectGroundingAccessPointDto restored =
            JsonSerializer.Deserialize<ProjectGroundingAccessPointDto>(json)!;

        Assert.Equal(original, restored);
        Assert.Equal(original.AdjacentPoleId, restored.AdjacentPoleId);
    }

    [Fact]
    public void RequiredTypedEnum_RejectsUnknownString()
    {
        const string json = """
            {"kind":"FutureTarget","targetId":"11111111-1111-1111-1111-111111111111"}
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ProjectGroundingTargetDto>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    [Fact]
    public void RequiredTypedEnum_RejectsNumericFallback()
    {
        const string json = """
            {"Kind":99,"TargetId":"11111111-1111-1111-1111-111111111111"}
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ProjectGroundingTargetDto>(json));
    }

    [Theory]
    [InlineData(ProjectStationKind.BoxStation)]
    [InlineData(ProjectStationKind.IndoorStation)]
    public void CustomerStationContract_RejectsZeroIncomingFeeders(
        ProjectStationKind stationKind)
    {
        Assert.Throws<ArgumentException>(() =>
            new ProjectCustomerStationDto(Guid.NewGuid(), stationKind, []));
    }

    [Fact]
    public void CustomerStationContract_AllowsTwoFeedersOnlyForIndoorStation()
    {
        ProjectCustomerStationIncomingFeederDto[] feeders =
            [CreateFeeder(), CreateFeeder()];

        var indoor = new ProjectCustomerStationDto(
            Guid.NewGuid(),
            ProjectStationKind.IndoorStation,
            feeders);

        Assert.Equal(2, indoor.IncomingFeeders.Count);
        Assert.Throws<ArgumentException>(() =>
            new ProjectCustomerStationDto(
                Guid.NewGuid(),
                ProjectStationKind.BoxStation,
                feeders));
    }

    public void Dispose()
    {
        foreach (string path in _paths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-v7-contract-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    private static ProjectCustomerStationIncomingFeederDto CreateFeeder()
    {
        Guid feederId = Guid.NewGuid();
        return new ProjectCustomerStationIncomingFeederDto(
            feederId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectSwitchDeviceDto(
                Guid.NewGuid(),
                "isolation-switch",
                "customer-station-incoming-feeder",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "open",
                "进线隔离开关",
                "10kV",
                null,
                new ProjectSwitchOwnerReferenceDto(
                    ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                    feederId)));
    }
}
