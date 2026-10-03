using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class WorkScopePersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ws-v10-{Guid.NewGuid():N}.kvdrawing");

    [Theory]
    [InlineData(false, 0, null)]
    [InlineData(false, 1, "")]
    [InlineData(true, 4, null)]
    [InlineData(true, 2, "snapshot")]
    public void V10RoundTripPreservesConfirmedSnapshotAndTicketStableReferences(bool multipleRegions, int boundaryCount, string? description)
    {
        (ProjectService service, ProjectSession project, RingCabinet cabinet) = CreateProject();
        Guid first = cabinet.Intervals[0].CableTerminalId!.Value, second = cabinet.Intervals[1].CableTerminalId!.Value;
        var connection = new Connection(Guid.NewGuid(), ConnectionType.Cable, first, second, "c", "10kV");
        project.Domain.AddConnection(connection);
        WorkScopeRegion[] regions = multipleRegions
            ? [new([first], [cabinet.MainBusNodeId]), new([second], [cabinet.Intervals[1].CircuitNodeId])]
            : [new([first, second], [cabinet.MainBusNodeId])];
        WorkScopeBoundary[] boundaries = new[]
        {
            new WorkScopeBoundary(cabinet.Id, BoundarySide.Line, first, connection.Id),
            new WorkScopeBoundary(cabinet.Id, BoundarySide.Bus),
            new WorkScopeBoundary(cabinet.Id, BoundarySide.Source, connectionId: connection.Id),
            new WorkScopeBoundary(cabinet.Id, BoundarySide.Load, second)
        }.Take(boundaryCount).ToArray();
        WorkScope scope = project.Domain.CreateWorkScope(Guid.NewGuid(), regions, boundaries, description);
        GroundingPoint point = project.Domain.CreateGroundingPoint(Guid.NewGuid(), first, "c", "S1");
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            WorkScopeIds = [scope.WorkScopeId], GroundingPointIds = [point.GroundingPointId],
            WorkScopeItems = [new(WorkScopeItemKind.ElectricalRange, scope.WorkScopeId), new(WorkScopeItemKind.Equipment, cabinet.Id)]
        };
        project.WorkTickets.Add(ticket);
        // Snapshot remains valid after state-only change; no EA is required to save/open.
        project.Domain.ChangeSwitchState(cabinet.Intervals[0].SwitchDevices[0].Id, SwitchState.Closed);
        service.SaveProject();
        ProjectSession restored = new ProjectService().LoadProject(_path);
        Assert.Equal(10, restored.OpenedFormatVersion);
        WorkScope actual = Assert.Single(restored.Domain.WorkScopes);
        Assert.Equal(scope.WorkScopeId, actual.WorkScopeId);
        Assert.Equal(description, actual.Description);
        Assert.Equal(regions.Length, actual.Regions.Count);
        for (int index = 0; index < regions.Length; index++)
        {
            Assert.Equal(regions[index].TerminalIds, actual.Regions[index].TerminalIds);
            Assert.Equal(regions[index].ElectricalNodeIds, actual.Regions[index].ElectricalNodeIds);
        }
        Assert.Equal(boundaries, actual.Boundaries);
        WorkTicketSession restoredTicket = Assert.Single(restored.WorkTickets.Tickets);
        Assert.Equal(scope.WorkScopeId, Assert.Single(restoredTicket.WorkScopeIds));
        Assert.Equal(point.GroundingPointId, Assert.Single(restoredTicket.GroundingPointIds));
        Assert.Equal(ticket.WorkScopeItems, restoredTicket.WorkScopeItems);
        JsonObject json = ScopeJson();
        Assert.NotNull(json["regions"]);
        Assert.NotNull(json["boundaries"]);
        Assert.Null(json["startBoundary"]);
        Assert.Null(json["endBoundary"]);
        Assert.Null(json["groundingPointIds"]);
    }

    [Theory]
    [InlineData("null-scope")]
    [InlineData("empty-scope-id")]
    [InlineData("null-region")]
    [InlineData("null-boundary")]
    [InlineData("missing-regions")]
    [InlineData("null-regions")]
    [InlineData("no-regions")]
    [InlineData("empty-region")]
    [InlineData("missing-terminal-list")]
    [InlineData("missing-node-list")]
    [InlineData("empty-terminal")]
    [InlineData("empty-node")]
    [InlineData("duplicate-terminal")]
    [InlineData("duplicate-node")]
    [InlineData("overlap-terminal")]
    [InlineData("overlap-node")]
    [InlineData("missing-terminal")]
    [InlineData("missing-node")]
    [InlineData("missing-device")]
    [InlineData("missing-boundary-terminal")]
    [InlineData("missing-connection")]
    [InlineData("duplicate-boundary")]
    [InlineData("invalid-side")]
    [InlineData("numeric-side")]
    [InlineData("unknown-side")]
    [InlineData("missing-boundaries")]
    [InlineData("null-boundaries")]
    [InlineData("legacy-schema")]
    [InlineData("unknown-field")]
    public void InvalidV10PayloadRejectsAtomicallyWithoutCleanup(string invalid)
    {
        (ProjectService service, ProjectSession project, RingCabinet cabinet) = CreateProject();
        Guid terminal = cabinet.Intervals[0].CableTerminalId!.Value;
        project.Domain.CreateWorkScope(Guid.NewGuid(), [new([terminal], [cabinet.MainBusNodeId])],
            [new(cabinet.Id, BoundarySide.Line, terminal)]);
        service.SaveProject();
        Mutate(payload =>
        {
            var scope = (JsonObject)payload["professional"]!["workScopes"]![0]!;
            var region = (JsonObject)scope["regions"]![0]!;
            var boundary = (JsonObject)scope["boundaries"]![0]!;
            switch (invalid)
            {
                case "null-scope": payload["professional"]!["workScopes"]![0] = null; break;
                case "empty-scope-id": scope["workScopeId"] = Guid.Empty.ToString(); break;
                case "null-region": scope["regions"]![0] = null; break;
                case "null-boundary": scope["boundaries"]![0] = null; break;
                case "missing-regions": scope.Remove("regions"); break;
                case "null-regions": scope["regions"] = null; break;
                case "no-regions": scope["regions"] = new JsonArray(); break;
                case "empty-region": region["terminalIds"] = new JsonArray(); region["electricalNodeIds"] = new JsonArray(); break;
                case "missing-terminal-list": region.Remove("terminalIds"); break;
                case "missing-node-list": region.Remove("electricalNodeIds"); break;
                case "empty-terminal": region["terminalIds"]![0] = Guid.Empty.ToString(); break;
                case "empty-node": region["electricalNodeIds"]![0] = Guid.Empty.ToString(); break;
                case "duplicate-terminal": ((JsonArray)region["terminalIds"]!).Add(terminal.ToString()); break;
                case "duplicate-node": ((JsonArray)region["electricalNodeIds"]!).Add(cabinet.MainBusNodeId.ToString()); break;
                case "overlap-terminal": ((JsonArray)scope["regions"]!).Add(JsonNode.Parse($"{{\"terminalIds\":[\"{terminal}\"],\"electricalNodeIds\":[]}}")); break;
                case "overlap-node": ((JsonArray)scope["regions"]!).Add(JsonNode.Parse($"{{\"terminalIds\":[],\"electricalNodeIds\":[\"{cabinet.MainBusNodeId}\"]}}")); break;
                case "missing-terminal": region["terminalIds"]![0] = Guid.NewGuid().ToString(); break;
                case "missing-node": region["electricalNodeIds"]![0] = Guid.NewGuid().ToString(); break;
                case "missing-device": boundary["deviceId"] = Guid.NewGuid().ToString(); break;
                case "missing-boundary-terminal": boundary["terminalId"] = Guid.NewGuid().ToString(); break;
                case "missing-connection": boundary["connectionId"] = Guid.NewGuid().ToString(); break;
                case "duplicate-boundary": ((JsonArray)scope["boundaries"]!).Add(boundary.DeepClone()); break;
                case "invalid-side": boundary["side"] = "Invalid"; break;
                case "numeric-side": boundary["side"] = 2; break;
                case "unknown-side": boundary["side"] = "Unknown"; break;
                case "missing-boundaries": scope.Remove("boundaries"); break;
                case "null-boundaries": scope["boundaries"] = null; break;
                case "legacy-schema": scope.Remove("regions"); scope.Remove("boundaries"); scope["startBoundary"] = boundary.DeepClone(); scope["endBoundary"] = boundary.DeepClone(); scope["groundingPointIds"] = new JsonArray(); break;
                case "unknown-field": scope["unexpected"] = true; break;
            }
        });
        byte[] before = File.ReadAllBytes(_path);
        ProjectSession active = service.Current!;
        Exception? error = Record.Exception(() => service.LoadProject(_path));
        Assert.True(error is InvalidDataException or JsonException, error?.ToString());
        Assert.Same(active, service.Current);
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.Single(active.Domain.WorkScopes);
    }

    [Fact]
    public void RawV10SaveRejectsDanglingScopeBeforeWriting()
    {
        (ProjectService service, ProjectSession project, RingCabinet cabinet) = CreateProject();
        WorkScope scope = project.Domain.CreateWorkScope(Guid.NewGuid(),
            [new([cabinet.Intervals[0].CableTerminalId!.Value], [])], []);
        service.SaveProject();
        ProjectFileDocument valid = new ProjectFileContainer().Open(_path);
        ProjectWorkScopeDto bad = valid.Professional!.WorkScopes[0] with
        {
            Regions = [new([Guid.NewGuid()], [])]
        };
        byte[] before = File.ReadAllBytes(_path);
        Assert.Throws<InvalidDataException>(() => new ProjectFileContainer().Save(_path, valid with
        {
            Professional = valid.Professional with { WorkScopes = [bad] }
        }));
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.Equal(scope.WorkScopeId, project.Domain.WorkScopes[0].WorkScopeId);
    }

    private (ProjectService, ProjectSession, RingCabinet) CreateProject()
    {
        var service = new ProjectService();
        ProjectSession project = service.CreateProject(_path, "confirmed snapshot");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "cabinet",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
        project.Domain.AddDevice(cabinet);
        var zero = new ProjectPointDto(0, 0);
        service.SetLayout(new ProjectLayoutSnapshot(ProjectLayoutDto.Empty(project.Domain.Id) with
        {
            RingCabinets = [new(cabinet.Id, zero, 100, 100, 20, zero,
                cabinet.Intervals.Select(interval => new ProjectRingCabinetIntervalLayoutDto(interval.IntervalId,
                    zero, 40, 80, zero, zero, interval.SwitchDevices.Select(device =>
                        new ProjectRingCabinetSwitchLayoutDto(device.Id, zero, 10, 10, zero)).ToArray())).ToArray())]
        }));
        return (service, project, cabinet);
    }

    private JsonObject ScopeJson()
    {
        using var zip = ZipFile.OpenRead(_path);
        using Stream stream = zip.GetEntry(ProjectFileFormat.DocumentEntryName)!.Open();
        return (JsonObject)JsonNode.Parse(stream)!["professional"]!["workScopes"]![0]!;
    }

    private void Mutate(Action<JsonObject> change)
    {
        using var zip = ZipFile.Open(_path, ZipArchiveMode.Update);
        ZipArchiveEntry entry = zip.GetEntry(ProjectFileFormat.DocumentEntryName)!;
        JsonObject payload;
        using (Stream input = entry.Open()) payload = (JsonObject)JsonNode.Parse(input)!;
        change(payload);
        entry.Delete();
        using Stream output = zip.CreateEntry(ProjectFileFormat.DocumentEntryName).Open();
        JsonSerializer.Serialize(output, payload);
    }

    public void Dispose() => File.Delete(_path);
}
