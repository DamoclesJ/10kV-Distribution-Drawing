using System.Text.Json.Nodes;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class PTIntervalPersistenceTests
{
    [Fact]
    public void PTInterval_RoundTrip_PreservesKindStableIdsSwitchStatesAndTerminalIds()
    {
        DrawingDocument originalDocument = CreateDocument();
        RingCabinet originalCabinet = Assert.Single(
            originalDocument.Devices.OfType<RingCabinet>());
        RingCabinetInterval originalInterval = Assert.Single(originalCabinet.Intervals);
        Guid[] originalTerminalIds = originalCabinet.Terminals
            .Select(terminal => terminal.Id)
            .Order()
            .ToArray();
        Guid[] originalSwitchIds = originalInterval.SwitchDevices
            .Select(device => device.Id)
            .ToArray();
        SwitchState?[] originalSwitchStates = originalInterval.SwitchDevices
            .Select(device => device.SwitchState)
            .ToArray();
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-pt-v6-{Guid.NewGuid():N}.kvdrawing");

        try
        {
            var container = new ProjectFileContainer();
            container.Save(filePath, new ProjectFileDocument(
                ProjectFileManifest.Create(
                    originalDocument.Id,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow),
                new ProjectFileMetadata(originalDocument.Title),
                ProjectDomainMapper.ToDto(originalDocument),
                ProjectLayoutDto.Empty(originalDocument.Id),
                ProjectProfessionalDto.Empty(originalDocument.Id)));

            ProjectFileDocument opened = container.Open(filePath);
            JsonObject savedPayload = ReadDocumentPayload(filePath);
            JsonObject savedInterval = Assert.IsType<JsonObject>(Assert.Single(GetIntervals(savedPayload)));
            DrawingDocument restoredDocument = ProjectDomainMapper.ToDomain(opened.Domain!);
            RingCabinet restoredCabinet = Assert.Single(
                restoredDocument.Devices.OfType<RingCabinet>());
            RingCabinetInterval restoredInterval = Assert.Single(restoredCabinet.Intervals);

            Assert.Equal(ProjectFileFormat.Version6, opened.Manifest.FormatVersion);
            Assert.Equal("pt-interval", savedInterval["intervalKind"]!.GetValue<string>());
            Assert.Equal(originalCabinet.Id, restoredCabinet.Id);
            Assert.Equal(originalInterval.IntervalId, restoredInterval.IntervalId);
            Assert.Equal(IntervalKind.PTInterval, restoredInterval.IntervalKind);
            Assert.Equal(originalSwitchIds, restoredInterval.SwitchDevices.Select(device => device.Id));
            Assert.Equal(originalSwitchStates, restoredInterval.SwitchDevices.Select(device => device.SwitchState));
            Assert.Equal(
                originalInterval.SwitchDevices.Select(device => device.TerminalIds),
                restoredInterval.SwitchDevices.Select(device => device.TerminalIds));
            Assert.Equal(originalTerminalIds, restoredCabinet.Terminals.Select(terminal => terminal.Id).Order());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static DrawingDocument CreateDocument()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "PT persistence test");
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "PT cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Closed,
                    SwitchState.Open,
                    "负7 PT间隔")]));
        document.AddDevice(cabinet);
        return document;
    }

    private static JsonObject ReadDocumentPayload(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new System.IO.Compression.ZipArchive(
            stream,
            System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry(ProjectFileFormat.DocumentEntryName)
            ?? throw new InvalidDataException("Document entry is missing.");
        using StreamReader reader = new(entry.Open());
        return JsonNode.Parse(reader.ReadToEnd()) as JsonObject
            ?? throw new InvalidDataException("Document payload is not an object.");
    }

    private static JsonArray GetIntervals(JsonObject payload)
    {
        var domain = payload["domain"] as JsonObject
            ?? throw new InvalidDataException("Domain payload is missing.");
        var cabinets = domain["ringCabinets"] as JsonArray
            ?? throw new InvalidDataException("Ring cabinets are missing.");
        var cabinet = cabinets.Single() as JsonObject
            ?? throw new InvalidDataException("Ring cabinet payload is invalid.");
        return cabinet["intervals"] as JsonArray
            ?? throw new InvalidDataException("Intervals are missing.");
    }
}
