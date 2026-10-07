using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Infrastructure.Persistence;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class WorkTicketPersistenceTests
{
    [Fact]
    public void ConfirmedWorkScopeHandoffSnapshotRoundTripsInV10WithoutEaState()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ws-handoff-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "Confirmed WorkScope handoff");
            RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "Handoff ring",
                [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
                 RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
            project.Domain.AddDevice(cabinet);
            var zero = new ProjectPointDto(0, 0);
            service.SetLayout(new ProjectLayoutSnapshot(ProjectLayoutDto.Empty(project.Domain.Id) with
            {
                RingCabinets = [new(cabinet.Id, zero, 100, 100, 20, zero,
                    cabinet.Intervals.Select(interval => new ProjectRingCabinetIntervalLayoutDto(interval.IntervalId,
                        zero, 40, 80, zero, zero, interval.SwitchDevices.Select(device =>
                            new ProjectRingCabinetSwitchLayoutDto(device.Id, zero, 10, 10, zero)).ToArray())).ToArray())]
            }));
            Guid terminalId = cabinet.Intervals[0].CableTerminalId!.Value;
            WorkScope scope = project.Domain.CreateWorkScope(Guid.NewGuid(),
                [new WorkScopeRegion([terminalId], [cabinet.Intervals[0].CircuitNodeId])], []);
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                WorkScopeIds = [scope.WorkScopeId],
                IsolationBoundaries = []
            };
            ticket = new WorkTicketAnalyzer().AnalyzeConfirmedWorkScopeHandoff(project.Domain, ticket);
            project.WorkTickets.Add(ticket);

            service.SaveProject();
            ProjectSession reopened = new ProjectService().LoadProject(path);

            Assert.Equal(10, reopened.OpenedFormatVersion);
            WorkScope restoredScope = Assert.Single(reopened.Domain.WorkScopes);
            WorkTicketSession restored = Assert.Single(reopened.WorkTickets.Tickets);
            Assert.Equal(scope.WorkScopeId, Assert.Single(restored.WorkScopeIds));
            Assert.Equal(restoredScope.WorkScopeId, restored.WorkScopeIds[0]);
            Assert.Empty(restored.IsolationBoundaries);
            Assert.NotNull(restored.Analysis);
            Assert.Equal(ticket.Analysis!.Issues, restored.Analysis!.Issues);
            Assert.Equal(ticket.Draft!.Sections.Select(item => (item.Code, item.Completion, item.Text)),
                restored.Draft!.Sections.Select(item => (item.Code, item.Completion, item.Text)));
            Assert.Equal(ticket.AnalyzedFingerprint, restored.AnalyzedFingerprint);
            Assert.Equal(ticket.RulePackVersion, restored.RulePackVersion);
            Assert.Equal(ticket.PhraseLibraryVersion, restored.PhraseLibraryVersion);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void RequiredWorkTicketSectionRoundTripsManualDraftAndKeepsFormatVersion()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "工作票测试");
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                Task = new WorkTask("检修设备", "一号柜"),
                AnalyzedFingerprint = "old-fingerprint",
                RulePackVersion = "first-kind-v0.1",
                PhraseLibraryVersion = "first-kind-phrases-v0.1",
                Draft = new WorkTicketDraft([new SectionDraft("6.1",
                    [new DraftItem(Guid.NewGuid(), "措施", "措施", DraftOrigin.Confirmed, [])],
                    SectionCompletion.Completed), new SectionDraft("6.5",
                    [new DraftItem(Guid.NewGuid(), "", "人工风险项", DraftOrigin.UserEdited, [])],
                    SectionCompletion.NeedsConfirmation)])
            };
            project.WorkTickets.Add(ticket);
            service.SaveProject();
            ProjectSession opened = new ProjectService().LoadProject(path);
            Assert.Equal(10, opened.OpenedFormatVersion);
            WorkTicketSession restored = Assert.Single(opened.WorkTickets.Tickets);
            Assert.Equal(ticket.Id, restored.Id);
            Assert.Equal("检修设备", restored.Task.Content);
            Assert.Equal(ticket.RulePackVersion, restored.RulePackVersion);
            Assert.Equal(ticket.PhraseLibraryVersion, restored.PhraseLibraryVersion);
            Assert.Equal("人工风险项", restored.Draft!.Section("6.5").Text);
            Assert.Equal(DraftOrigin.UserEdited, Assert.Single(restored.Draft.Section("6.5").Items).Origin);
            Assert.Equal(SectionCompletion.Stale, restored.EffectiveCompletion("6.1",
                restored.AnalyzedFingerprint != WorkTicketAnalyzer.Fingerprint(opened.Domain, restored)));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CurrentFormatRoundTripsEquipmentWorkScopeTargetIdentity()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-equipment-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "设备工作范围");
            SwitchDevice equipment = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.LoadSwitch,
                Guid.NewGuid(), Guid.NewGuid(), displayName: "验收测试环网柜");
            project.Domain.AddDevice(equipment);
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                Task = new WorkTask("更换验收测试环网柜", "验收测试环网柜"),
                WorkScopeItems = [new WorkScopeItem(WorkScopeItemKind.Equipment, equipment.Id)]
            };
            project.WorkTickets.Add(ticket);
            service.SaveProject();

            WorkTicketSession restored = Assert.Single(new ProjectService().LoadProject(path).WorkTickets.Tickets);
            WorkScopeItem target = Assert.Single(restored.WorkScopeItems);
            Assert.Equal(WorkScopeItemKind.Equipment, target.Kind);
            Assert.Equal(equipment.Id, target.TargetId);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void SaveRejectsBrokenTicketReferencesBeforeReplacingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "工作票测试");
            project.WorkTickets.Add(WorkTicketSession.Create() with
            {
                WorkScopeIds = [Guid.NewGuid()]
            });
            Assert.Throws<InvalidDataException>(() => service.SaveProject());
            Assert.Empty(new ProjectService().LoadProject(path).WorkTickets.Tickets);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    public void UnsupportedProjectVersionsAreRejected(int version)
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-version-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            new ProjectService().CreateProject(path, "版本测试");
            Mutate(path, ProjectFileFormat.ManifestEntryName, json => json["formatVersion"] = version);
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CurrentFormatRequiresWorkTicketData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-required-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            new ProjectService().CreateProject(path, "必填工作票段");
            Mutate(path, ProjectFileFormat.DocumentEntryName, json => json.Remove("workTicketData"));
            Assert.Throws<InvalidDataException>(() => new ProjectService().LoadProject(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CurrentFormatRoundTripsCompleteTicketAnalysisAndDraftFields()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wta-full-{Guid.NewGuid():N}.kvdrawing");
        try
        {
            var service = new ProjectService();
            ProjectSession project = service.CreateProject(path, "完整数据");
            Guid measureId = Guid.NewGuid();
            MeasureFact measure = new(measureId, "6.3", MeasureKind.Barrier, "Barrier", [], "位置A",
                Reversible: true, Origin: FactOrigin.UserAdded);
            WorkTicketAnalysis analysis = new([], [measure], [],
                [new RiskItem("风险A", "措施A", FactOrigin.UserAdded)],
                [new RestorationMeasure(measureId, "Barrier", [], "位置A", null, FactOrigin.UserAdded)],
                ["待核实"]);
            string[] codes = ["6.1", "6.2", "6.3", "6.4", "6.5", "16.1"];
            WorkTicketDraft draft = new(codes.Select(code => new SectionDraft(code,
                [new DraftItem(Guid.NewGuid(), "原文", "人工修改", DraftOrigin.Confirmed, [],
                    code == "6.3" ? measureId : null)
                    { Source = FactOrigin.UserAdded, IsUserEdited = true }],
                SectionCompletion.Completed)).ToArray());
            WorkTicketSession ticket = WorkTicketSession.Create() with
            {
                Task = new WorkTask("工作内容", "工作对象"),
                UserFacts = [new UserTicketFact("Risk", "风险A", [], true)],
                Analysis = analysis, Draft = draft, AnalyzedFingerprint = "fingerprint",
                RulePackVersion = "rules-1", PhraseLibraryVersion = "phrases-1"
            };
            project.WorkTickets.Add(ticket);
            service.SaveProject();
            WorkTicketSession restored = Assert.Single(new ProjectService().LoadProject(path).WorkTickets.Tickets);
            Assert.Equal(ticket.Id, restored.Id);
            Assert.Equal(ticket.Task, restored.Task);
            Assert.Equal(ticket.UserFacts[0].Text, restored.UserFacts[0].Text);
            Assert.Equal(measureId, Assert.Single(restored.Analysis!.WorkGroupMeasures).Id);
            Assert.Equal(measureId, Assert.Single(restored.Analysis.RestorationMeasures).SourceMeasureId);
            Assert.Equal("措施A", Assert.Single(restored.Analysis.Risks).Mitigation);
            Assert.Equal("待核实", Assert.Single(restored.Analysis.Issues));
            Assert.Equal(codes, restored.Draft!.Sections.Select(section => section.Code));
            Assert.All(restored.Draft.Sections, section =>
            {
                Assert.Equal("人工修改", Assert.Single(section.Items).CurrentText);
                Assert.True(section.Items[0].IsUserEdited);
            });
            Assert.Equal(measureId, restored.Draft.Section("6.3").Items[0].SourceMeasureId);
            Assert.Equal("rules-1", restored.RulePackVersion);
            Assert.Equal("phrases-1", restored.PhraseLibraryVersion);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void Mutate(string path, string entryName, Action<JsonObject> mutate)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);
        ZipArchiveEntry entry = archive.GetEntry(entryName)!;
        JsonObject json;
        using (Stream source = entry.Open()) json = (JsonObject)JsonNode.Parse(source)!;
        mutate(json);
        entry.Delete();
        using Stream target = archive.CreateEntry(entryName).Open();
        JsonSerializer.Serialize(target, json);
    }
}
