using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class CablePersistenceV6Tests
{
    [Fact]
    public void CableSegment_RoundTrip_PreservesStableIdsAndConnectivity()
    {
        DrawingDocument original = CreateDocumentWithIntermediateTerminals(2);
        IntermediateTerminal[] terminals = original.IntermediateTerminals.ToArray();
        CableSegmentCreationResult cable = CreateCable(original, terminals[0], terminals[1], "A-B");
        new CreateCableSegmentCommand(original, cable).Execute();

        DrawingDocument restored = RoundTrip(original);
        CableSegment restoredCable = Assert.Single(restored.CableSegments);
        ElectricalConnectivityQuery query = BuildQuery(restored);

        Assert.Equal(cable.CableSegment.Id, restoredCable.Id);
        Assert.Equal(cable.Connection.Id, restored.Connections.Single().Id);
        Assert.Equal(cable.CableSegment.StartTerminalId, restoredCable.StartTerminalId);
        Assert.Equal(cable.CableSegment.EndTerminalId, restoredCable.EndTerminalId);
        Assert.True(query.IsConnected(
            restoredCable.StartTerminalId,
            restoredCable.EndTerminalId));
    }

    [Fact]
    public void SplitCable_RoundTrip_PreservesSegmentsIntermediateTerminalAndConnections()
    {
        DrawingDocument original = CreateDocumentWithIntermediateTerminals(2);
        IntermediateTerminal[] endpoints = original.IntermediateTerminals.ToArray();
        CableSegmentCreationResult cable = CreateCable(
            original,
            endpoints[0],
            endpoints[1],
            "A-B");
        new CreateCableSegmentCommand(original, cable).Execute();

        var split = new SplitCableCommand(original, cable.CableSegment.Id, "接头 X");
        split.Execute();
        CableSplitResult result = split.Result!;
        Guid[] segmentIds = [result.FirstCableSegment.Id, result.SecondCableSegment.Id];
        Guid[] connectionIds = [result.FirstConnection.Id, result.SecondConnection.Id];

        DrawingDocument restored = RoundTrip(original);
        IntermediateTerminal restoredIntermediate = Assert.Single(
            restored.IntermediateTerminals,
            item => item.Id == result.IntermediateTerminal.IntermediateTerminal.Id);
        ElectricalConnectivityQuery query = BuildQuery(restored);

        Assert.Equal(result.IntermediateTerminal.Terminal.Id, restoredIntermediate.TerminalId);
        Assert.Equal(segmentIds.Order(), restored.CableSegments.Select(item => item.Id).Order());
        Assert.Equal(connectionIds.Order(), restored.Connections.Select(item => item.Id).Order());
        Assert.True(query.IsConnected(
            result.FirstCableSegment.StartTerminalId,
            result.SecondCableSegment.EndTerminalId));
    }

    [Fact]
    public void ReconnectedCable_RoundTrip_PreservesSegmentAndNewConnection()
    {
        DrawingDocument original = CreateDocumentWithIntermediateTerminals(3);
        IntermediateTerminal[] terminals = original.IntermediateTerminals.ToArray();
        CableSegmentCreationResult cable = CreateCable(original, terminals[0], terminals[1], "A-B");
        new CreateCableSegmentCommand(original, cable).Execute();

        var reconnect = new ReconnectCableCommand(
            original,
            cable.CableSegment.Id,
            terminals[0].TerminalId,
            terminals[2].TerminalId);
        reconnect.Execute();
        CableReconnectResult result = reconnect.Result!;

        DrawingDocument restored = RoundTrip(original);
        CableSegment restoredCable = Assert.Single(restored.CableSegments);
        ElectricalConnectivityQuery query = BuildQuery(restored);

        Assert.Equal(result.AfterCableSegment.Id, restoredCable.Id);
        Assert.Equal(result.AfterConnection.Id, restoredCable.ConnectionId);
        Assert.DoesNotContain(restored.Connections, item => item.Id == result.BeforeConnection.Id);
        Assert.True(query.IsConnected(terminals[0].TerminalId, terminals[2].TerminalId));
        Assert.False(query.IsConnected(terminals[1].TerminalId, terminals[2].TerminalId));
    }

    private static DrawingDocument CreateDocumentWithIntermediateTerminals(int count)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Cable V6 persistence test");
        for (int index = 1; index <= count; index++)
        {
            IntermediateTerminalCreationResult result =
                new IntermediateTerminalCreationFactory().Create($"T{index}");
            new CreateIntermediateTerminalCommand(document, result).Execute();
        }

        return document;
    }

    private static CableSegmentCreationResult CreateCable(
        DrawingDocument document,
        IntermediateTerminal start,
        IntermediateTerminal end,
        string name)
    {
        return new CableSegmentCreationFactory().Create(
            document,
            start.TerminalId,
            end.TerminalId,
            name,
            "XLPE",
            100,
            "10kV");
    }

    private static DrawingDocument RoundTrip(DrawingDocument document)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-cable-v6-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, new ProjectFileDocument(
                ProjectFileManifest.Create(
                    document.Id,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                new ProjectFileMetadata(document.Title),
                ProjectDomainMapper.ToDto(document),
                ProjectLayoutDto.Empty(document.Id),
                ProjectProfessionalDto.Empty(document.Id)));

            return ProjectDomainMapper.ToDomain(container.Open(filePath).Domain!);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static ElectricalConnectivityQuery BuildQuery(DrawingDocument document)
    {
        return new ElectricalConnectivityQuery(
            new ElectricalConnectivityGraphBuilder().Build(document));
    }
}
