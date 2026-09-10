using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class CustomerStationPersistenceTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void V7DomainRoundTrip_PreservesCustomerStationAggregate(
        StationKind stationKind,
        int feederCount)
    {
        DrawingDocument document = CreateDocument(
            stationKind,
            Enumerable.Range(1, feederCount).Select(index => $"进线 {index}").ToArray());
        CustomerStation expected = Assert.Single(document.CustomerStations);

        ProjectDomainDto dto = ProjectDomainMapper.ToDto(document);
        DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(dto);
        CustomerStation actual = Assert.Single(restoredDocument.CustomerStations);

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.StationKind, actual.StationKind);
        Assert.Equal(
            expected.IncomingFeeders.Select(Identity),
            actual.IncomingFeeders.Select(Identity));
        Assert.Empty(dto.SwitchDevices!);
        Assert.Equal(feederCount * 2, dto.Terminals!.Count);
        Assert.Equal(feederCount, dto.ElectricalNodes!.Count);
    }

    [Fact]
    public void V7DomainRoundTrip_PreservesDualFeederStateOwnerAndGenericElectricalState()
    {
        DrawingDocument document = CreateDocument(
            StationKind.IndoorStation,
            ["主供", "备供"]);
        CustomerStation expected = Assert.Single(document.CustomerStations);
        IncomingFeeder first = expected.IncomingFeeders[0];
        IncomingFeeder second = expected.IncomingFeeders[1];
        document.ChangeSwitchState(first.IsolationSwitch.Id, SwitchState.Closed);
        second.ElectricalNode.SetElectricalState(ElectricalState.Energized);

        ProjectDomainDto dto = ProjectDomainMapper.ToDto(document);
        DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(dto);
        CustomerStation actual = Assert.Single(restoredDocument.CustomerStations);

        Assert.Equal(SwitchState.Closed, actual.IncomingFeeders[0].IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, actual.IncomingFeeders[1].IsolationSwitch.SwitchState);
        Assert.Equal(ElectricalState.Energized, actual.IncomingFeeders[1].ElectricalNode.ElectricalState);
        Assert.All(actual.IncomingFeeders, feeder =>
        {
            Assert.Equal(
                SwitchInstallationType.CustomerStationIncomingFeeder,
                feeder.IsolationSwitch.InstallationType);
            Assert.Equal(feeder.IncomingFeederId, feeder.IsolationSwitch.ParentId);
            Assert.Equal(feeder.CableTerminalId, feeder.IsolationSwitch.FirstTerminalId);
            Assert.Equal(feeder.StationTerminalId, feeder.IsolationSwitch.SecondTerminalId);
        });
    }

    [Fact]
    public void V7CustomerStationDto_RejectsInvalidAggregateStructures()
    {
        ProjectCustomerStationIncomingFeederDto first = CreateFeederDto(1, "主供");
        ProjectCustomerStationIncomingFeederDto second = CreateFeederDto(2, "备供");

        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.BoxStation, [first, second]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation, []));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first, second with { Sequence = 1 }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with { DisplayName = "  " }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first, second, CreateFeederDto(3, "第三路")]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with { ElectricalNodeId = first.CableTerminalId }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with
            {
                IsolationSwitch = first.IsolationSwitch with { DeviceId = Guid.Empty }
            }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with
            {
                IsolationSwitch = first.IsolationSwitch with
                {
                    Owner = new ProjectSwitchOwnerReferenceDto(
                        ProjectSwitchOwnerKind.RingCabinetInterval,
                        first.IncomingFeederId)
                }
            }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with
            {
                IsolationSwitch = first.IsolationSwitch with
                {
                    Owner = new ProjectSwitchOwnerReferenceDto(
                        ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                        Guid.NewGuid())
                }
            }]));
        Assert.Throws<ArgumentException>(() => new ProjectCustomerStationDto(
            Guid.NewGuid(), ProjectStationKind.IndoorStation,
            [first with
            {
                IsolationSwitch = first.IsolationSwitch with
                {
                    FirstTerminalId = Guid.NewGuid()
                }
            }]));
    }

    [Fact]
    public void V7DomainRestore_RejectsInvalidTerminalAndNodeFacts()
    {
        DrawingDocument document = CreateDocument(StationKind.BoxStation, ["主供"]);
        CustomerStation station = Assert.Single(document.CustomerStations);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        ProjectDomainDto valid = ProjectDomainMapper.ToDto(document);
        ProjectTerminalDto stationTerminal = valid.Terminals!.Single(terminal =>
            terminal.TerminalId == feeder.StationTerminalId);
        ProjectElectricalNodeDto node = valid.ElectricalNodes!.Single(item =>
            item.NodeId == feeder.ElectricalNodeId);
        IReadOnlyList<ProjectTerminalDto> terminals = valid.Terminals!;
        IReadOnlyList<ProjectElectricalNodeDto> nodes = valid.ElectricalNodes!;

        ProjectDomainDto externalStationTerminal = valid with
        {
            Terminals = terminals.Select(terminal =>
                terminal.TerminalId == feeder.StationTerminalId
                    ? terminal with { IsExternal = true }
                    : terminal).ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            ProjectDomainMapper.ToDomain(externalStationTerminal));

        ProjectDomainDto wrongNodeOwner = valid with
        {
            ElectricalNodes = nodes.Select(item =>
                item.NodeId == feeder.ElectricalNodeId
                    ? item with { OwnerId = Guid.NewGuid() }
                    : item).ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            ProjectDomainMapper.ToDomain(wrongNodeOwner));

        ProjectDomainDto wrongNodeType = valid with
        {
            ElectricalNodes = nodes.Select(item =>
                item.NodeId == feeder.ElectricalNodeId
                    ? item with { NodeType = "bus" }
                    : item).ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            ProjectDomainMapper.ToDomain(wrongNodeType));

        ProjectDomainDto missingTerminal = valid with
        {
            Terminals = terminals.Where(terminal =>
                terminal.TerminalId != feeder.CableTerminalId).ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            ProjectDomainMapper.ToDomain(missingTerminal));

        Assert.False(stationTerminal.IsExternal);
        Assert.Equal(feeder.IncomingFeederId, node.OwnerId);
    }

    [Fact]
    public void V7DomainRestore_RejectsExtraTerminalOwnedByIncomingFeeder()
    {
        DrawingDocument document = CreateDocument(StationKind.BoxStation, ["主供"]);
        CustomerStation station = Assert.Single(document.CustomerStations);
        IncomingFeeder feeder = Assert.Single(station.IncomingFeeders);
        ProjectDomainDto valid = ProjectDomainMapper.ToDto(document);
        IReadOnlyList<ProjectTerminalDto> terminals = valid.Terminals!;
        ProjectTerminalDto officialStationTerminal = terminals.Single(terminal =>
            terminal.TerminalId == feeder.StationTerminalId);
        var extraTerminal = officialStationTerminal with
        {
            TerminalId = Guid.NewGuid(),
            OwnerType = "internal-aggregate",
            OwnerId = feeder.IncomingFeederId,
            Role = "extra-station-terminal"
        };
        ProjectDomainDto malformed = valid with
        {
            Terminals = [.. terminals, extraTerminal]
        };

        Assert.Throws<InvalidOperationException>(() =>
            ProjectDomainMapper.ToDomain(malformed));
    }

    [Fact]
    public void V7DomainRestore_TrimsIncomingFeederDisplayNameWithoutChangingItsValue()
    {
        DrawingDocument document = CreateDocument(StationKind.BoxStation, ["主供"]);
        ProjectDomainDto valid = ProjectDomainMapper.ToDto(document);
        ProjectCustomerStationDto station = Assert.Single(valid.CustomerStations!);
        ProjectCustomerStationIncomingFeederDto feeder = Assert.Single(
            station.IncomingFeeders);
        ProjectDomainDto padded = valid with
        {
            CustomerStations =
            [
                new ProjectCustomerStationDto(
                    station.CustomerStationId,
                    station.StationKind,
                    [feeder with { DisplayName = "  主供  " }])
            ]
        };

        DrawingDocument restored = ProjectDomainMapper.ToDomain(padded);

        Assert.Equal(
            "主供",
            Assert.Single(Assert.Single(restored.CustomerStations).IncomingFeeders).DisplayName);
        ProjectCustomerStationDto written = Assert.Single(
            ProjectDomainMapper.ToDto(restored).CustomerStations!);
        Assert.Equal("主供", Assert.Single(written.IncomingFeeders).DisplayName);
    }

    [Fact]
    public void V7LayoutValidation_RequiresExactFeederCoverageAndBoxVisibility()
    {
        DrawingDocument boxDocument = CreateDocument(StationKind.BoxStation, ["主供"]);
        CustomerStation box = Assert.Single(boxDocument.CustomerStations);
        IncomingFeeder boxFeeder = Assert.Single(box.IncomingFeeders);

        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(
            boxDocument,
            ProjectLayoutDto.Empty(boxDocument.Id)));

        ProjectLayoutDto missingFeeder = CreateLayoutDto(
            boxDocument,
            box,
            []);
        Assert.Throws<InvalidDataException>(() =>
            ProjectLayoutMapper.ToSnapshot(boxDocument, missingFeeder));

        ProjectCustomerStationIncomingFeederLayoutDto visible = new(
            boxFeeder.IncomingFeederId,
            true);
        ProjectLayoutDto duplicateFeeder = CreateLayoutDto(
            boxDocument,
            box,
            [visible, visible]);
        Assert.Throws<InvalidDataException>(() =>
            ProjectLayoutMapper.ToSnapshot(boxDocument, duplicateFeeder));

        ProjectLayoutDto hiddenBox = CreateLayoutDto(
            boxDocument,
            box,
            [visible with { ShowIncomingSwitch = false }]);
        Assert.Throws<InvalidDataException>(() =>
            ProjectLayoutMapper.ToSnapshot(boxDocument, hiddenBox));
    }

    [Fact]
    public void ProjectService_SaveReopen_PreservesDualCustomerStationAndLayout()
    {
        string path = NextPath();
        var service = new ProjectService();
        ProjectSession created = service.CreateProject(path, "双电源用户站");
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供", "备供"]);
        created.Domain.AddCustomerStation(station);
        created.Domain.ChangeSwitchState(
            station.IncomingFeeders[0].IsolationSwitch.Id,
            SwitchState.Closed);
        ProjectLayoutDto layoutDto = CreateLayoutDto(
            created.Domain,
            station,
            [
                new ProjectCustomerStationIncomingFeederLayoutDto(
                    station.IncomingFeeders[0].IncomingFeederId,
                    true),
                new ProjectCustomerStationIncomingFeederLayoutDto(
                    station.IncomingFeeders[1].IncomingFeederId,
                    false)
            ],
            new ProjectPointDto(120.5, 80.25));

        service.SaveProject(ProjectLayoutMapper.ToSnapshot(created.Domain, layoutDto));
        ProjectSession reopened = new ProjectService().LoadProject(path);
        CustomerStation restored = Assert.Single(reopened.Domain.CustomerStations);
        ProjectCustomerStationLayoutDto restoredLayout = Assert.Single(
            reopened.Layout.CustomerStationLayouts);

        Assert.Equal(station.Id, restored.Id);
        Assert.Equal(StationKind.IndoorStation, restored.StationKind);
        Assert.Equal(["主供", "备供"],
            restored.IncomingFeeders.Select(feeder => feeder.DisplayName).ToArray());
        Assert.Equal(SwitchState.Closed, restored.IncomingFeeders[0].IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, restored.IncomingFeeders[1].IsolationSwitch.SwitchState);
        Assert.Equal(new ProjectPointDto(120.5, 80.25), restoredLayout.Position);
        Assert.True(restoredLayout.IncomingFeeders[0].ShowIncomingSwitch);
        Assert.False(restoredLayout.IncomingFeeders[1].ShowIncomingSwitch);
        Assert.Equal(
            station.IncomingFeeders.Select(Identity),
            restored.IncomingFeeders.Select(Identity));

        var query = new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(reopened.Domain));
        Assert.True(query.IsConnected(
            restored.IncomingFeeders[0].CableTerminalId,
            restored.IncomingFeeders[0].StationTerminalId));
        Assert.False(query.IsConnected(
            restored.IncomingFeeders[1].CableTerminalId,
            restored.IncomingFeeders[1].StationTerminalId));
        Assert.False(query.IsConnected(
            restored.IncomingFeeders[0].CableTerminalId,
            restored.IncomingFeeders[1].CableTerminalId));
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
            $"customer-station-persistence-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    private static DrawingDocument CreateDocument(
        StationKind kind,
        IReadOnlyList<string> names)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "customer station persistence");
        document.AddCustomerStation(new CustomerStationCreationFactory().Create(kind, names));
        return document;
    }

    private static ProjectLayoutDto CreateLayoutDto(
        DrawingDocument document,
        CustomerStation station,
        IReadOnlyList<ProjectCustomerStationIncomingFeederLayoutDto> feeders,
        ProjectPointDto? position = null)
    {
        return ProjectLayoutDto.Empty(document.Id) with
        {
            CustomerStationLayouts =
            [
                new ProjectCustomerStationLayoutDto(
                    station.Id,
                    position ?? new ProjectPointDto(10, 20),
                    feeders)
            ]
        };
    }

    private static ProjectCustomerStationIncomingFeederDto CreateFeederDto(
        int sequence,
        string displayName)
    {
        Guid feederId = Guid.NewGuid();
        Guid cableTerminalId = Guid.NewGuid();
        Guid stationTerminalId = Guid.NewGuid();
        return new ProjectCustomerStationIncomingFeederDto(
            feederId,
            sequence,
            displayName,
            cableTerminalId,
            stationTerminalId,
            Guid.NewGuid(),
            new ProjectSwitchDeviceDto(
                Guid.NewGuid(),
                "isolation-switch",
                "customer-station-incoming-feeder",
                cableTerminalId,
                stationTerminalId,
                "open",
                "进线隔离开关",
                "10kV",
                null,
                new ProjectSwitchOwnerReferenceDto(
                    ProjectSwitchOwnerKind.CustomerStationIncomingFeeder,
                    feederId)));
    }

    private static object Identity(IncomingFeeder feeder) => new
    {
        feeder.IncomingFeederId,
        feeder.Sequence,
        feeder.DisplayName,
        feeder.CableTerminalId,
        feeder.StationTerminalId,
        feeder.ElectricalNodeId,
        SwitchId = feeder.IsolationSwitch.Id
    };
}
