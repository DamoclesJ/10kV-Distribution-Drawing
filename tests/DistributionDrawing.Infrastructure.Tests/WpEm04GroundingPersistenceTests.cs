using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class WpEm04GroundingPersistenceTests : IDisposable
{
    private readonly List<string> _paths = [];

    [Fact]
    public void V7RoundTrip_PreservesGapAdjacencyAndBothTypedGroundingTargets()
    {
        Fixture fixture = CreateFixture();
        string path = NextPath();
        var container = new ProjectFileContainer();
        var file = new ProjectFileDocument(
            ProjectFileManifest.Create(
                fixture.Document.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new ProjectFileMetadata(fixture.Document.Title),
            ProjectDomainMapper.ToDto(fixture.Document),
            ProjectLayoutDto.Empty(fixture.Document.Id),
            ProjectProfessionalMapper.ToDto(fixture.Document));

        container.Save(path, file);
        ProjectFileDocument opened = container.Open(path);
        ProjectGroundingAccessPointDto dto = Assert.Single(
            opened.Professional!.GroundingAccessPoints!);
        Assert.Equal(fixture.Gap.GroundingAccessPointId, dto.GroundingAccessPointId);
        Assert.Equal(fixture.Middle.Id, dto.PoleId);
        Assert.Equal(fixture.End.Id, dto.AdjacentPoleId);

        DrawingDocument restored = ProjectDomainMapper.ToDomain(opened.Domain!);
        ProjectProfessionalMapper.ToSnapshot(restored, opened.Professional);
        GroundingAccessPoint gap = Assert.Single(restored.GroundingAccessPoints);
        Assert.Equal(fixture.Gap.AdjacentPoleId, gap.AdjacentPoleId);
        Assert.Contains(restored.GroundingPoints, point =>
            point.Target == GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId));
        Assert.Contains(restored.GroundingPoints, point =>
            point.Target == GroundingTarget.ForTerminal(fixture.StartTerminalId));
    }

    [Fact]
    public void Load_RejectsEmptyOrNonAdjacentAdjacentPole()
    {
        Fixture fixture = CreateFixture();
        ProjectProfessionalDto valid = ProjectProfessionalMapper.ToDto(fixture.Document);
        ProjectGroundingAccessPointDto gap = Assert.Single(valid.GroundingAccessPoints!);

        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [gap with { AdjacentPoleId = Guid.Empty }]
        });
        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [gap with { AdjacentPoleId = fixture.Unrelated.Id }]
        });
    }

    [Fact]
    public void Load_RejectsDuplicatePhysicalGapTuple()
    {
        Fixture fixture = CreateFixture();
        ProjectProfessionalDto valid = ProjectProfessionalMapper.ToDto(fixture.Document);
        ProjectGroundingAccessPointDto gap = Assert.Single(valid.GroundingAccessPoints!);

        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [gap, gap with { GroundingAccessPointId = Guid.NewGuid() }]
        });
    }

    [Fact]
    public void Load_RejectsDanglingGapTargetAndDuplicateTypedTargetOccupancy()
    {
        Fixture fixture = CreateFixture();
        ProjectProfessionalDto valid = ProjectProfessionalMapper.ToDto(fixture.Document);
        ProjectGroundingPointDto gapGrounding = valid.GroundingPoints.Single(point =>
            point.GroundingTarget.Kind == ProjectGroundingTargetKind.GroundingAccessPoint);

        AssertInvalid(fixture, valid with
        {
            GroundingPoints =
            [
                gapGrounding with
                {
                    GroundingTarget = gapGrounding.GroundingTarget with { TargetId = Guid.NewGuid() }
                }
            ]
        });
        AssertInvalid(fixture, valid with
        {
            GroundingPoints =
            [
                gapGrounding,
                gapGrounding with { GroundingPointId = Guid.NewGuid(), Number = "L99" }
            ]
        });
    }

    private static void AssertInvalid(Fixture fixture, ProjectProfessionalDto professional)
    {
        DrawingDocument domain = ProjectDomainMapper.ToDomain(
            ProjectDomainMapper.ToDto(fixture.DomainOnlyDocument));
        Assert.Throws<InvalidDataException>(() =>
            ProjectProfessionalMapper.ToSnapshot(domain, professional));
    }

    private static Fixture CreateFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "WP-EM-04 persistence");
        Pole start = AddPole(document, "P-10");
        Pole middle = AddPole(document, "P-11");
        Pole end = AddPole(document, "P-12");
        Pole unrelated = AddPole(document, "P-99");
        Terminal startTerminal = start.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        Terminal endTerminal = end.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        document.AddTerminal(startTerminal);
        document.AddTerminal(endTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            startTerminal.Id, endTerminal.Id, "测试架空线", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(
            connection.Id, "JKLYJ", [start.Id, middle.Id, end.Id]));
        DrawingDocument domainOnly = ProjectDomainMapper.ToDomain(ProjectDomainMapper.ToDto(document));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(), connection.Id, middle.Id, end.Id,
            GroundingAccessLineSide.LargerNumberSide);
        document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "大号侧", "L01");
        document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(startTerminal.Id),
            "legacy terminal", "L02");
        return new Fixture(document, domainOnly, start, middle, end, unrelated, gap, startTerminal.Id);
    }

    private static Pole AddPole(DrawingDocument document, string number)
    {
        var pole = new Pole(Guid.NewGuid(), number);
        document.AddDevice(pole);
        return pole;
    }

    private string NextPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"wp-em-04-{Guid.NewGuid():N}.kvdrawing");
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed record Fixture(
        DrawingDocument Document,
        DrawingDocument DomainOnlyDocument,
        Pole Start,
        Pole Middle,
        Pole End,
        Pole Unrelated,
        GroundingAccessPoint Gap,
        Guid StartTerminalId);
}
