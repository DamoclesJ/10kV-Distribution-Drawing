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
        Assert.Equal(ProjectGroundingAccessPlacementSide.PoleSide, dto.PlacementSide);

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
            AdjacentEndpoint = null,
            PlacementSide = null
        };

        foreach (ProjectGroundingAccessLineSide side in new[]
                 {
                     ProjectGroundingAccessLineSide.SmallerNumberSide,
                     ProjectGroundingAccessLineSide.LargerNumberSide
                 })
        {
            DrawingDocument restored = ProjectDomainMapper.ToDomain(
                ProjectDomainMapper.ToDto(fixture.DomainOnlyDocument));
            ProjectProfessionalMapper.ToSnapshot(
                restored,
                valid with { GroundingAccessPoints = [legacy with { LineSide = side }] });
            GroundingAccessPoint point = Assert.Single(restored.GroundingAccessPoints);
            Assert.Equal(legacy.GroundingAccessPointId, point.GroundingAccessPointId);
            Assert.Equal(GroundingAdjacentEndpoint.ForPole(fixture.End.Id), point.AdjacentEndpoint);
            Assert.Equal(GroundingAccessPlacementSide.PoleSide, point.PlacementSide);

            DrawingDocument newShapeRestored = ProjectDomainMapper.ToDomain(
                ProjectDomainMapper.ToDto(fixture.DomainOnlyDocument));
            ProjectProfessionalMapper.ToSnapshot(
                newShapeRestored,
                valid with { GroundingAccessPoints = [typed with { LineSide = side }] });
            Assert.Equal(
                GroundingAccessPlacementSide.PoleSide,
                Assert.Single(newShapeRestored.GroundingAccessPoints).PlacementSide);
        }

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
    public void Load_RejectsNewInvalidPoleLineAndPlacementCombinations()
    {
        Fixture fixture = CreateFixture();
        ProjectProfessionalDto valid = ProjectProfessionalMapper.ToDto(fixture.Document);
        ProjectGroundingAccessPointDto gap = Assert.Single(valid.GroundingAccessPoints!);

        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints =
            [gap with { LineSide = ProjectGroundingAccessLineSide.TransformerSide }]
        });
        AssertInvalid(fixture, valid with
        {
            GroundingAccessPoints =
            [gap with { PlacementSide = ProjectGroundingAccessPlacementSide.AdjacentEndpointSide }]
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
            hvTerminalId,
            "杆上变压器");
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
            GroundingAccessLineSide.TransformerSide,
            GroundingAccessPlacementSide.AdjacentEndpointSide);
        GroundingAccessPoint poleSideGap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(), connection.Id, pole.Id,
            GroundingAdjacentEndpoint.ForTerminal(hvTerminalId),
            GroundingAccessLineSide.TransformerSide,
            GroundingAccessPlacementSide.PoleSide);
        GroundingPoint grounding = document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(hvTerminalId),
            "变压器高压侧", "S01", "保留备注");
        GroundingPoint conductorGrounding = document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(poleSideGap.GroundingAccessPointId),
            $"{pole.PoleNumber}杆变压器侧",
            "L01",
            "导线侧备注");

        string path = NextPath();
        var container = new ProjectFileContainer();
        container.Save(path, new ProjectFileDocument(
            ProjectFileManifest.Create(document.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new ProjectFileMetadata(document.Title),
            ProjectDomainMapper.ToDto(document),
            ProjectLayoutDto.Empty(document.Id),
            ProjectProfessionalMapper.ToDto(document)));
        ProjectFileDocument opened = container.Open(path);
        ProjectGroundingAccessPointDto writtenTransformerSide =
            opened.Professional!.GroundingAccessPoints!.Single(point =>
                point.GroundingAccessPointId == gap.GroundingAccessPointId);
        ProjectGroundingAccessPointDto writtenPoleSide =
            opened.Professional.GroundingAccessPoints!.Single(point =>
                point.GroundingAccessPointId == poleSideGap.GroundingAccessPointId);
        Assert.Null(writtenTransformerSide.AdjacentPoleId);
        Assert.Equal(ProjectGroundingAccessLineSide.TransformerSide, writtenTransformerSide.LineSide);
        Assert.Equal(
            ProjectGroundingAccessPlacementSide.AdjacentEndpointSide,
            writtenTransformerSide.PlacementSide);
        Assert.Null(writtenPoleSide.AdjacentPoleId);
        Assert.Equal(ProjectGroundingAccessLineSide.TransformerSide, writtenPoleSide.LineSide);
        Assert.Equal(ProjectGroundingAccessPlacementSide.PoleSide, writtenPoleSide.PlacementSide);
        DrawingDocument restored = ProjectDomainMapper.ToDomain(opened.Domain!);
        ProjectProfessionalMapper.ToSnapshot(restored, opened.Professional);

        GroundingAccessPoint restoredGap = Assert.Single(restored.GroundingAccessPoints,
            point => point.GroundingAccessPointId == gap.GroundingAccessPointId);
        Assert.Equal(gap.GroundingAccessPointId, restoredGap.GroundingAccessPointId);
        Assert.Equal(GroundingAdjacentEndpoint.ForTerminal(hvTerminalId), restoredGap.AdjacentEndpoint);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, restoredGap.LineSide);
        Assert.Equal(GroundingAccessPlacementSide.AdjacentEndpointSide, restoredGap.PlacementSide);
        GroundingAccessPoint restoredPoleSideGap = Assert.Single(restored.GroundingAccessPoints,
            point => point.GroundingAccessPointId == poleSideGap.GroundingAccessPointId);
        Assert.Equal(GroundingAccessPlacementSide.PoleSide, restoredPoleSideGap.PlacementSide);
        GroundingPoint restoredGrounding = Assert.Single(restored.GroundingPoints,
            point => point.GroundingPointId == grounding.GroundingPointId);
        Assert.Equal(grounding.GroundingPointId, restoredGrounding.GroundingPointId);
        Assert.Equal(GroundingTarget.ForTerminal(hvTerminalId), restoredGrounding.Target);
        Assert.Equal("变压器高压侧", restoredGrounding.Location);
        Assert.Equal("S01", restoredGrounding.Number);
        Assert.Equal("保留备注", restoredGrounding.Note);
        GroundingPoint restoredConductorGrounding = Assert.Single(restored.GroundingPoints,
            point => point.GroundingPointId == conductorGrounding.GroundingPointId);
        Assert.Equal(conductorGrounding.Target, restoredConductorGrounding.Target);
        Assert.Equal(conductorGrounding.Location, restoredConductorGrounding.Location);
        Assert.Equal("L01", restoredConductorGrounding.Number);
        Assert.Equal("导线侧备注", restoredConductorGrounding.Note);

        ProjectGroundingPointDto directGrounding = opened.Professional.GroundingPoints.Single(point =>
            point.GroundingPointId == grounding.GroundingPointId);
        ProjectGroundingPointDto conductorGroundingDto = opened.Professional.GroundingPoints.Single(point =>
            point.GroundingPointId == conductorGrounding.GroundingPointId);
        foreach (ProjectGroundingAccessLineSide legacySide in new[]
                 {
                     ProjectGroundingAccessLineSide.SmallerNumberSide,
                     ProjectGroundingAccessLineSide.LargerNumberSide
                 })
        {
            ProjectGroundingAccessPointDto oldTypedTerminal = writtenPoleSide with
            {
                LineSide = legacySide,
                PlacementSide = null
            };
            DrawingDocument legacyRestored = ProjectDomainMapper.ToDomain(opened.Domain!);
            ProjectProfessionalMapper.ToSnapshot(
                legacyRestored,
                opened.Professional with
                {
                    GroundingAccessPoints = [oldTypedTerminal],
                    GroundingPoints = [directGrounding, conductorGroundingDto]
                });
            GroundingAccessPoint normalized = Assert.Single(legacyRestored.GroundingAccessPoints);
            Assert.Equal(poleSideGap.GroundingAccessPointId, normalized.GroundingAccessPointId);
            Assert.Equal(connection.Id, normalized.ConnectionId);
            Assert.Equal(pole.Id, normalized.PoleId);
            Assert.Equal(GroundingAdjacentEndpoint.ForTerminal(hvTerminalId), normalized.AdjacentEndpoint);
            Assert.Equal(GroundingAccessLineSide.TransformerSide, normalized.LineSide);
            Assert.Equal(GroundingAccessPlacementSide.PoleSide, normalized.PlacementSide);
            GroundingPoint normalizedConductorGrounding = legacyRestored.GetGroundingPoint(
                conductorGrounding.GroundingPointId);
            Assert.Equal(conductorGrounding.Target, normalizedConductorGrounding.Target);
            Assert.Equal(conductorGrounding.Location, normalizedConductorGrounding.Location);
            Assert.Equal(conductorGrounding.Number, normalizedConductorGrounding.Number);
            Assert.Equal(conductorGrounding.Note, normalizedConductorGrounding.Note);
        }

        foreach (ProjectGroundingAccessLineSide invalidSide in new[]
                 {
                     ProjectGroundingAccessLineSide.SmallerNumberSide,
                     ProjectGroundingAccessLineSide.LargerNumberSide
                 })
        foreach (ProjectGroundingAccessPlacementSide explicitPlacement in new[]
                 {
                     ProjectGroundingAccessPlacementSide.PoleSide,
                     ProjectGroundingAccessPlacementSide.AdjacentEndpointSide
                 })
        {
            DrawingDocument malformedDomain = ProjectDomainMapper.ToDomain(opened.Domain!);
            Assert.Throws<InvalidDataException>(() => ProjectProfessionalMapper.ToSnapshot(
                malformedDomain,
                opened.Professional with
                {
                    GroundingAccessPoints =
                    [writtenTransformerSide with
                    {
                        LineSide = invalidSide,
                        PlacementSide = explicitPlacement
                    }],
                    GroundingPoints = [directGrounding]
                }));
        }

        DrawingDocument missingNewPlacementDomain = ProjectDomainMapper.ToDomain(opened.Domain!);
        Assert.Throws<InvalidDataException>(() => ProjectProfessionalMapper.ToSnapshot(
            missingNewPlacementDomain,
            opened.Professional with
            {
                GroundingAccessPoints =
                [writtenTransformerSide with { PlacementSide = null }],
                GroundingPoints = [directGrounding]
            }));
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
