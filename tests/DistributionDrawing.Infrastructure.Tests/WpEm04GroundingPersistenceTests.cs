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
        Connection before = Assert.Single(fixture.Document.Connections);
        Terminal replacement = fixture.Start.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        fixture.Document.AddTerminal(replacement);
        fixture.Document.ReplaceOverheadConnection(before,
            new Connection(before.Id, before.Type, replacement.Id, before.EndTerminalId, before.DisplayName, before.VoltageLevel),
            Assert.Single(fixture.Document.OverheadLines));
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
        Assert.Null(dto.AdjacentPoleId);
        Assert.Equal(ProjectGroundingAdjacentEndpointKind.Pole, dto.AdjacentEndpoint!.Kind);
        Assert.Equal(fixture.End.Id, dto.AdjacentEndpoint.TargetId);

        DrawingDocument restored = ProjectDomainMapper.ToDomain(opened.Domain!);
        Assert.Equal(replacement.Id, Assert.Single(restored.Connections).StartTerminalId);
        ProjectProfessionalMapper.ToSnapshot(restored, opened.Professional);
        GroundingAccessPoint gap = Assert.Single(restored.GroundingAccessPoints);
        Assert.Equal(fixture.Gap.AdjacentPoleId, gap.AdjacentPoleId);
        Assert.Contains(restored.GroundingPoints, point =>
            point.Target == GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId) &&
            point.Number == "L01");
        Assert.Contains(restored.GroundingPoints, point =>
            point.Target == GroundingTarget.ForTerminal(fixture.StartTerminalId) &&
            point.Number == "S02");
        GroundingPoint restoredCustom = restored.GetGroundingPoint(
            fixture.CustomGroundingPoint.GroundingPointId);
        Assert.Equal(fixture.CustomGroundingPoint.Target, restoredCustom.Target);
        Assert.Equal("CustomGround", restoredCustom.Number);
        GroundingPoint restoredLegacy = restored.GetGroundingPoint(
            fixture.LegacyMismatchedGroundingPoint.GroundingPointId);
        Assert.Equal(fixture.LegacyMismatchedGroundingPoint.Target, restoredLegacy.Target);
        Assert.Equal("L77", restoredLegacy.Number);
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
    public void Load_NormalizesLegacyPoleForm_AndRejectsBothOrNeitherRepresentations()
    {
        Fixture fixture = CreateFixture();
        ProjectProfessionalDto valid = ProjectProfessionalMapper.ToDto(fixture.Document);
        ProjectGroundingAccessPointDto typed = Assert.Single(valid.GroundingAccessPoints!);
        ProjectGroundingAccessPointDto legacy = typed with
        {
            AdjacentPoleId = fixture.End.Id,
            AdjacentEndpoint = null
        };

        DrawingDocument restored = ProjectDomainMapper.ToDomain(
            ProjectDomainMapper.ToDto(fixture.DomainOnlyDocument));
        ProjectProfessionalMapper.ToSnapshot(
            restored,
            valid with { GroundingAccessPoints = [legacy] });
        GroundingAccessPoint point = Assert.Single(restored.GroundingAccessPoints);
        Assert.Equal(legacy.GroundingAccessPointId, point.GroundingAccessPointId);
        Assert.Equal(GroundingAdjacentEndpoint.ForPole(fixture.End.Id), point.AdjacentEndpoint);

        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [typed with { AdjacentPoleId = fixture.End.Id }]
        });
        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [typed with { AdjacentPoleId = Guid.Empty }]
        });
        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints = [typed with { AdjacentPoleId = null, AdjacentEndpoint = null }]
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

    [Fact]
    public void V7RoundTrip_PreservesTerminalEndpointGapAndTransformerTerminalGrounding()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "WP-EM-07A persistence");
        Pole pole = AddPole(document, "P-20");
        Terminal poleTerminal = pole.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        document.AddTerminal(poleTerminal);
        Guid transformerId = Guid.NewGuid();
        Guid hvTerminalId = Guid.NewGuid();
        var transformer = new Transformer(
            transformerId,
            TransformerKind.PublicPoleMounted,
            hvTerminalId);
        var hvTerminal = new Terminal(
            hvTerminalId,
            TopologyOwnerType.Device,
            transformerId,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            true,
            false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        document.AddTransformer(transformer, hvTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            poleTerminal.Id, hvTerminal.Id, "短架空线", Transformer.TenKilovolts);
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(connection.Id, "JKLYJ", [pole.Id]));
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(), connection.Id, pole.Id,
            GroundingAdjacentEndpoint.ForTerminal(hvTerminalId),
            GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint grounding = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(hvTerminalId),
            "变压器高压侧", "S01", "保留备注");

        string path = NextPath();
        var container = new ProjectFileContainer();
        container.Save(path, new ProjectFileDocument(
            ProjectFileManifest.Create(document.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new ProjectFileMetadata(document.Title),
            ProjectDomainMapper.ToDto(document),
            ProjectLayoutDto.Empty(document.Id),
            ProjectProfessionalMapper.ToDto(document)));
        ProjectFileDocument opened = container.Open(path);
        DrawingDocument restored = ProjectDomainMapper.ToDomain(opened.Domain!);
        ProjectProfessionalMapper.ToSnapshot(restored, opened.Professional);

        GroundingAccessPoint restoredGap = Assert.Single(restored.GroundingAccessPoints);
        Assert.Equal(gap.GroundingAccessPointId, restoredGap.GroundingAccessPointId);
        Assert.Equal(GroundingAdjacentEndpoint.ForTerminal(hvTerminalId), restoredGap.AdjacentEndpoint);
        GroundingPoint restoredGrounding = Assert.Single(restored.GroundingPoints);
        Assert.Equal(grounding.GroundingPointId, restoredGrounding.GroundingPointId);
        Assert.Equal(GroundingTarget.ForTerminal(hvTerminalId), restoredGrounding.Target);
        Assert.Equal("变压器高压侧", restoredGrounding.Location);
        Assert.Equal("S01", restoredGrounding.Number);
        Assert.Equal("保留备注", restoredGrounding.Note);
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
        Terminal customTerminal = unrelated.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        Terminal legacyMismatchedTerminal = end.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        document.AddTerminal(customTerminal);
        document.AddTerminal(legacyMismatchedTerminal);
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
            "legacy terminal", "S02");
        GroundingPoint customGroundingPoint = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(customTerminal.Id),
            "custom terminal", "CustomGround");
        GroundingPoint legacyMismatchedGroundingPoint = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(legacyMismatchedTerminal.Id),
            "legacy mismatched prefix", "L77");
        return new Fixture(
            document,
            domainOnly,
            start,
            middle,
            end,
            unrelated,
            gap,
            startTerminal.Id,
            customGroundingPoint,
            legacyMismatchedGroundingPoint);
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
        Guid StartTerminalId,
        GroundingPoint CustomGroundingPoint,
        GroundingPoint LegacyMismatchedGroundingPoint);
}
