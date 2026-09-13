using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class TransformerPersistenceTests
{
    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    [InlineData(TransformerKind.PublicIndoor)]
    public void DomainMapper_RoundTripsTransformerAggregateWithStableIds(TransformerKind kind)
    {
        (DrawingDocument document, Transformer transformer, Terminal terminal) = CreateDocument(kind);

        ProjectDomainDto dto = ProjectDomainMapper.ToDto(document);
        DrawingDocument restored = ProjectDomainMapper.ToDomain(dto);

        ProjectTransformerDto persisted = Assert.Single(dto.Transformers!);
        Assert.Empty(dto.Devices);
        Transformer actual = Assert.IsType<Transformer>(Assert.Single(restored.Devices));
        Terminal actualTerminal = Assert.Single(restored.Terminals);
        Assert.Equal(transformer.Id, persisted.TransformerId);
        Assert.Equal(transformer.HvTerminalId, persisted.HvTerminalId);
        Assert.Equal(transformer.DisplayName, persisted.DisplayName);
        Assert.Equal(transformer.Id, actual.Id);
        Assert.Equal(transformer.DisplayName, actual.DisplayName);
        Assert.Equal(kind, actual.TransformerKind);
        Assert.Equal(terminal.Id, actualTerminal.Id);
        Assert.Equal(terminal.OwnerId, actualTerminal.OwnerId);
        Assert.Equal(terminal.AllowedConnectionTypes, actualTerminal.AllowedConnectionTypes);
        Assert.Empty(restored.ElectricalNodes);
    }

    [Fact]
    public void DomainMapper_RejectsTransformerWithMissingHvTerminal()
    {
        Guid transformerId = Guid.NewGuid();
        ProjectDomainDto dto = ProjectDomainDto.Empty(Guid.NewGuid(), "missing") with
        {
            Transformers = [new ProjectTransformerDto(
                transformerId,
                ProjectTransformerKind.PublicIndoor,
                Guid.NewGuid(),
                "T1")]
        };

        Assert.Throws<InvalidDataException>(() => ProjectDomainMapper.ToDomain(dto));
    }

    [Theory]
    [InlineData("wrong-owner")]
    [InlineData("wrong-role")]
    [InlineData("wrong-voltage")]
    [InlineData("not-external")]
    [InlineData("multiple")]
    [InlineData("node")]
    [InlineData("wrong-policy")]
    public void DomainMapper_RejectsMalformedTransformerTerminal(string mutation)
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectTerminalDto terminal = ValidTerminal(transformerId, terminalId);
        terminal = mutation switch
        {
            "wrong-owner" => terminal with { OwnerId = Guid.NewGuid() },
            "wrong-role" => terminal with { Role = "Other" },
            "wrong-voltage" => terminal with { VoltageLevel = "0.4kV" },
            "not-external" => terminal with { IsExternal = false },
            "multiple" => terminal with { AllowsMultipleConnections = true },
            "node" => terminal with { ElectricalNodeId = Guid.NewGuid() },
            "wrong-policy" => terminal with { AllowedConnectionTypes = ["overhead-line"] },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        ProjectDomainDto dto = ProjectDomainDto.Empty(Guid.NewGuid(), mutation) with
        {
            Transformers = [new ProjectTransformerDto(
                transformerId,
                ProjectTransformerKind.PublicIndoor,
                terminalId,
                "T1")],
            Terminals = [terminal]
        };

        Assert.Throws<InvalidDataException>(() => ProjectDomainMapper.ToDomain(dto));
    }

    [Fact]
    public void DomainMapper_RejectsExtraTransformerOwnedTerminal()
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectDomainDto dto = ProjectDomainDto.Empty(Guid.NewGuid(), "extra") with
        {
            Transformers = [new ProjectTransformerDto(
                transformerId,
                ProjectTransformerKind.PublicIndoor,
                terminalId,
                "T1")],
            Terminals =
            [
                ValidTerminal(transformerId, terminalId),
                ValidTerminal(transformerId, Guid.NewGuid())
            ]
        };

        Assert.Throws<InvalidOperationException>(() => ProjectDomainMapper.ToDomain(dto));
    }

    [Fact]
    public void DomainMapper_RejectsTransformerOwnedElectricalNode()
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectDomainDto dto = ProjectDomainDto.Empty(Guid.NewGuid(), "node") with
        {
            Transformers = [new ProjectTransformerDto(
                transformerId,
                ProjectTransformerKind.PublicIndoor,
                terminalId,
                "T1")],
            Terminals = [ValidTerminal(transformerId, terminalId)],
            ElectricalNodes = [new ProjectElectricalNodeDto(
                Guid.NewGuid(),
                "intermediate",
                "device",
                transformerId,
                null)]
        };

        Assert.Throws<InvalidOperationException>(() => ProjectDomainMapper.ToDomain(dto));
    }

    [Fact]
    public void DomainMapper_RejectsUnknownTransformerKind()
    {
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        ProjectDomainDto dto = ProjectDomainDto.Empty(Guid.NewGuid(), "enum") with
        {
            Transformers = [new ProjectTransformerDto(
                transformerId,
                (ProjectTransformerKind)999,
                terminalId,
                "T1")],
            Terminals = [ValidTerminal(transformerId, terminalId)]
        };

        Assert.Throws<InvalidDataException>(() => ProjectDomainMapper.ToDomain(dto));
    }

    [Fact]
    public void LayoutMapper_RequiresExactlyOneLayoutPerTransformer()
    {
        (DrawingDocument document, Transformer transformer, _) = CreateDocument(
            TransformerKind.PublicIndoor);

        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(
            document,
            ProjectLayoutDto.Empty(document.Id)));

        var orphan = ProjectLayoutDto.Empty(Guid.NewGuid()) with
        {
            TransformerLayouts = [new ProjectTransformerLayoutDto(
                Guid.NewGuid(),
                new ProjectPointDto(1, 2),
                ProjectTransformerOrientation.Horizontal)]
        };
        var emptyDocument = new DrawingDocument(orphan.DocumentId, "orphan");
        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(
            emptyDocument,
            orphan));

        var duplicate = ValidLayout(document.Id, transformer.Id) with
        {
            TransformerLayouts =
            [
                new ProjectTransformerLayoutDto(transformer.Id, new ProjectPointDto(1, 2), ProjectTransformerOrientation.Horizontal),
                new ProjectTransformerLayoutDto(transformer.Id, new ProjectPointDto(3, 4), ProjectTransformerOrientation.Vertical)
            ]
        };
        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(document, duplicate));
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    public void LayoutMapper_RejectsHorizontalPoleMountedOrientation(TransformerKind kind)
    {
        (DrawingDocument document, Transformer transformer, _) = CreateDocument(kind);

        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(
            document,
            ValidLayout(document.Id, transformer.Id)));
    }

    [Theory]
    [InlineData(ProjectTransformerOrientation.Horizontal)]
    [InlineData(ProjectTransformerOrientation.Vertical)]
    public void LayoutMapper_AcceptsBothPublicIndoorOrientations(
        ProjectTransformerOrientation orientation)
    {
        (DrawingDocument document, Transformer transformer, _) = CreateDocument(
            TransformerKind.PublicIndoor);
        ProjectLayoutDto layout = ValidLayout(document.Id, transformer.Id) with
        {
            TransformerLayouts = [new ProjectTransformerLayoutDto(
                transformer.Id,
                new ProjectPointDto(12.5, 25),
                orientation)]
        };

        ProjectLayoutSnapshot snapshot = ProjectLayoutMapper.ToSnapshot(document, layout);

        Assert.Equal(orientation, Assert.Single(snapshot.TransformerLayouts).Orientation);
    }

    [Fact]
    public void LayoutMapper_RejectsUnknownOrientation()
    {
        (DrawingDocument document, Transformer transformer, _) = CreateDocument(
            TransformerKind.PublicIndoor);
        ProjectLayoutDto layout = ValidLayout(document.Id, transformer.Id) with
        {
            TransformerLayouts = [new ProjectTransformerLayoutDto(
                transformer.Id,
                new ProjectPointDto(1, 2),
                (ProjectTransformerOrientation)999)]
        };

        Assert.Throws<InvalidDataException>(() => ProjectLayoutMapper.ToSnapshot(document, layout));
    }

    private static (DrawingDocument, Transformer, Terminal) CreateDocument(TransformerKind kind)
    {
        var document = new DrawingDocument(Guid.NewGuid(), kind.ToString());
        Guid transformerId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        var transformer = new Transformer(transformerId, kind, terminalId, "测试变压器");
        var terminal = new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            transformerId,
            Transformer.HvTerminalRole,
            Transformer.TenKilovolts,
            true,
            false,
            null,
            [transformer.AllowedConnectionType]);
        document.AddTransformer(transformer, terminal);
        return (document, transformer, terminal);
    }

    private static ProjectTerminalDto ValidTerminal(Guid transformerId, Guid terminalId) => new(
        terminalId,
        "device",
        transformerId,
        Transformer.HvTerminalRole,
        Transformer.TenKilovolts,
        true,
        false,
        null,
        ["cable"]);

    private static ProjectLayoutDto ValidLayout(Guid documentId, Guid transformerId) =>
        ProjectLayoutDto.Empty(documentId) with
        {
            TransformerLayouts = [new ProjectTransformerLayoutDto(
                transformerId,
                new ProjectPointDto(1, 2),
                ProjectTransformerOrientation.Horizontal)]
        };
}
