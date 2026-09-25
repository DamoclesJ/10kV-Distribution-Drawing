using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkTicketAnalyzerTests
{
    [Fact]
    public void BoundarySideAndTerminalControlGroundingMeasures()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "侧别测试");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice device = cabinet.Intervals[0].SwitchDevices.First(item => item.SwitchKind == SwitchKind.LoadSwitch);
        var rules = new FirstKindRulePack();

        Assert.Contains(rules.Switching(drawing, new IsolationBoundary(device.Id, BoundarySide.Line,
            device.SecondTerminalId)), item => item.Kind == MeasureKind.CloseGroundSwitch);
        Assert.DoesNotContain(rules.Switching(drawing, new IsolationBoundary(device.Id, BoundarySide.Bus,
            device.FirstTerminalId)), item => item.Kind == MeasureKind.CloseGroundSwitch);
        IsolationBoundary mismatch = new(device.Id, BoundarySide.Line, device.FirstTerminalId);
        Assert.NotNull(rules.BoundaryIssue(drawing, mismatch));
        Assert.DoesNotContain(rules.Switching(drawing, mismatch), item => item.Kind == MeasureKind.CloseGroundSwitch);
        IsolationBoundary missing = new(device.Id, BoundarySide.Unknown);
        Assert.Contains("缺少", rules.BoundaryIssue(drawing, missing));
        Assert.DoesNotContain(rules.Switching(drawing, missing), item => item.Kind == MeasureKind.CloseGroundSwitch);
        Assert.NotNull(rules.BoundaryIssue(drawing, new IsolationBoundary(device.Id, BoundarySide.Line,
            ConnectionId: Guid.NewGuid())));
        RingCabinet other = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "二号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(other);
        var connection = new Connection(Guid.NewGuid(), ConnectionType.Cable,
            cabinet.Intervals[0].CableTerminalId!.Value, other.Intervals[0].CableTerminalId!.Value,
            "外部电缆", "10kV");
        drawing.AddConnection(connection);
        IsolationBoundary viaConnection = new(device.Id, BoundarySide.Line,
            ConnectionId: connection.Id);
        Assert.Null(rules.BoundaryIssue(drawing, viaConnection));
        Assert.Contains(rules.Switching(drawing, viaConnection), item =>
            item.Kind == MeasureKind.CloseGroundSwitch);
    }

    [Fact]
    public void PoleDropoutFuseKeepsRemovalAsRecommendationAfterOpening()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "柱上保险");
        SwitchDevice fuse = SwitchDevice.CreateForPole(Guid.NewGuid(), SwitchKind.DropoutFuse,
            Guid.NewGuid(), Guid.NewGuid(), displayName: "保险");
        drawing.AddDevice(fuse);
        MeasureFact[] measures = new FirstKindRulePack().Switching(drawing,
            new IsolationBoundary(fuse.Id, BoundarySide.Line, fuse.SecondTerminalId)).ToArray();
        Assert.Equal(new[] { MeasureKind.OpenSwitch, MeasureKind.RemoveFuseTube, MeasureKind.Sign },
            measures.Select(item => item.Kind));
        Assert.Equal(FactOrigin.SystemRecommendation, measures[1].Origin);
    }

    [Fact]
    public void EditingKeepsGeneratedItemsAndRequiresExplicitConfirmation()
    {
        Guid source = Guid.NewGuid();
        DraftItem first = new(Guid.NewGuid(), "原措施一", "原措施一", DraftOrigin.RuleDerived, [], source);
        DraftItem second = new(Guid.NewGuid(), "原措施二", "原措施二", DraftOrigin.RuleDerived, []);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Draft = new WorkTicketDraft([new SectionDraft("6.3", [first, second],
                SectionCompletion.Generated), new SectionDraft("16.1", [], SectionCompletion.Completed)])
        };
        WorkTicketSession edited = ticket.EditSection("6.3", "修改措施一\n原措施二\n人工新增");
        SectionDraft section = edited.Draft!.Section("6.3");
        Assert.Equal(SectionCompletion.NeedsConfirmation, section.Completion);
        Assert.Equal(first.Id, section.Items[0].Id);
        Assert.Equal(source, section.Items[0].SourceMeasureId);
        Assert.Equal(first.GeneratedText, section.Items[0].GeneratedText);
        Assert.Equal(second.Id, section.Items[1].Id);
        Assert.Equal(DraftOrigin.RuleDerived, section.Items[1].Origin);
        Assert.Equal(DraftOrigin.UserAdded, section.Items[2].Origin);
        Assert.Equal(SectionCompletion.Stale, edited.Draft.Section("16.1").Completion);
        WorkTicketSession confirmed = edited.ConfirmSection("6.3");
        Assert.Equal(SectionCompletion.Completed, confirmed.Draft!.Section("6.3").Completion);
        Assert.True(confirmed.Draft.Section("6.3").Items[0].IsUserEdited);
        WorkTicketSession restored = confirmed.RestoreGeneratedSection("6.3");
        Assert.Equal(first.Id, restored.Draft!.Section("6.3").Items[0].Id);
        Assert.Equal(first.GeneratedText, restored.Draft.Section("6.3").Items[0].CurrentText);
        Assert.DoesNotContain(restored.Draft.Section("6.3").Items, item => item.Source == FactOrigin.UserAdded);
        Assert.Equal(SectionCompletion.Stale, restored.Draft.Section("16.1").Completion);
    }

    [Fact]
    public void EditedRetainedLiveRecommendationNeedsExplicitConfirmation()
    {
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Draft = new WorkTicketDraft([new SectionDraft("6.4",
                [new DraftItem(Guid.NewGuid(), "请核实带电关系", "请核实带电关系",
                    DraftOrigin.RuleDerived, []) { Source = FactOrigin.SystemRecommendation }],
                SectionCompletion.NeedsConfirmation)])
        };
        Assert.Throws<InvalidOperationException>(() => ticket.ConfirmSection("6.4"));
        WorkTicketSession edited = ticket.EditSection("6.4", "现场确认邻近设备带电");
        Assert.Equal(SectionCompletion.NeedsConfirmation, edited.Draft!.Section("6.4").Completion);
        WorkTicketSession confirmed = edited.ConfirmSection("6.4");
        Assert.Equal(SectionCompletion.Completed, confirmed.Draft!.Section("6.4").Completion);
        Assert.Equal("现场确认邻近设备带电",
            new WorkTicketTextExporter().Section(confirmed.Draft, "6.4"));
    }

    [Fact]
    public void ManualRiskTextNeedsConfirmationAfterEditing()
    {
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Draft = new WorkTicketDraft([new SectionDraft("6.5", [], SectionCompletion.NeedsInput)])
        };
        WorkTicketSession edited = ticket.EditSection("6.5", "现场风险");
        Assert.Equal(SectionCompletion.NeedsConfirmation, edited.Draft!.Section("6.5").Completion);
        Assert.Equal(SectionCompletion.Completed,
            edited.ConfirmSection("6.5").Draft!.Section("6.5").Completion);
    }

    [Fact]
    public void RestoringGenerated63KeepsOtherPendingSectionEdits()
    {
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Draft = new WorkTicketDraft([
                new SectionDraft("6.3", [new DraftItem(Guid.NewGuid(), "原措施", "人工措施",
                    DraftOrigin.UserEdited, [], Guid.NewGuid()) { IsUserEdited = true }],
                    SectionCompletion.NeedsConfirmation),
                new SectionDraft("6.5", [], SectionCompletion.NeedsInput),
                new SectionDraft("16.1", [new DraftItem(Guid.NewGuid(), "恢复", "恢复",
                    DraftOrigin.RuleDerived, [])], SectionCompletion.Stale)])
        };
        WorkTicketSession after = ticket.EditSection("6.5", "未保存的风险文字")
            .RestoreGeneratedSection("6.3");
        Assert.Equal("未保存的风险文字", after.Draft!.Section("6.5").Text);
        Assert.Equal("原措施", after.Draft.Section("6.3").Text);
    }

    [Fact]
    public void RiskInputChangesAndPhraseVersionInvalidateCompletedDraft()
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        var analyzer = new WorkTicketAnalyzer();
        WorkTicketSession analyzed = analyzer.Analyze(drawing, ticket);
        WorkTicketSession completed = analyzed.ConfirmSection("6.1");
        Assert.False(analyzer.IsStale(drawing, completed));
        UserTicketFact risk = new("Risk", "现场风险", [], true);
        Assert.True(analyzer.IsStale(drawing, completed with { UserFacts = [risk] }));
        WorkTicketSession withRisk = analyzer.Analyze(drawing, completed with { UserFacts = [risk] });
        Assert.True(analyzer.IsStale(drawing, withRisk with { UserFacts = [] }));
        Assert.True(analyzer.IsStale(drawing, withRisk with
        {
            UserFacts = [risk with { Text = "变更风险" }]
        }));
        Assert.True(new WorkTicketAnalyzer(phrases: new VersionedPhrases()).IsStale(drawing, completed));
        Assert.True(new WorkTicketAnalyzer(rules: new VersionedRules()).IsStale(drawing, completed));
        Assert.Equal(SectionCompletion.Stale,
            new WorkTicketAnalyzer(phrases: new VersionedPhrases()).RefreshStale(drawing, completed)
                .Draft!.Section("6.1").Completion);
        Assert.Equal(SectionCompletion.NeedsConfirmation,
            new WorkTicketAnalyzer(phrases: new VersionedPhrases()).Analyze(drawing, completed)
                .Draft!.Section("6.1").Completion);
    }

    [Fact]
    public void ReanalysisDoesNotDuplicateOrResurrectRemovedUserFacts()
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        UserTicketFact risk = new("Risk", "风险A", [], true);
        UserTicketFact retainedLive = new("RetainedLive", "现场带电事实", [], true);
        var analyzer = new WorkTicketAnalyzer();
        WorkTicketSession first = analyzer.Analyze(drawing,
            ticket with { UserFacts = [risk, retainedLive] });
        WorkTicketSession second = analyzer.Analyze(drawing, first);
        Assert.Single(second.Draft!.Section("6.5").Items);
        Assert.Single(second.Draft.Section("6.4").Items, item => item.CurrentText == "现场带电事实");
        WorkTicketSession removed = analyzer.Analyze(drawing,
            second with { UserFacts = [] });
        Assert.Empty(removed.Draft!.Section("6.5").Items);
        Assert.DoesNotContain(removed.Draft.Section("6.4").Items,
            item => item.CurrentText == "现场带电事实");
        WorkTicketSession manual = analyzer.Analyze(drawing,
            removed.EditSection("6.5", "人工补充风险"));
        Assert.Single(analyzer.Analyze(drawing, manual).Draft!.Section("6.5").Items);
    }

    [Fact]
    public void ReanalysisPreservesEditedTextWhenItsRiskFactChangesOrDisappears()
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        UserTicketFact risk = new("Risk", "风险A", [], true);
        var analyzer = new WorkTicketAnalyzer();
        WorkTicketSession edited = analyzer.Analyze(drawing, ticket with { UserFacts = [risk] })
            .EditSection("6.5", "人工风险A");
        WorkTicketSession changed = analyzer.Analyze(drawing, edited with
        {
            UserFacts = [risk with { Text = "风险B" }]
        });
        Assert.Equal(2, changed.Draft!.Section("6.5").Items.Count);
        Assert.Contains(changed.Draft.Section("6.5").Items,
            item => item.CurrentText == "人工风险A" && item.IsUserEdited);
        Assert.Equal(SectionCompletion.NeedsConfirmation, changed.Draft.Section("6.5").Completion);
        Assert.Contains(changed.Analysis!.Issues, issue => issue.Contains("来源事实已变化"));

        WorkTicketSession removed = analyzer.Analyze(drawing, changed with { UserFacts = [] });
        Assert.Equal("人工风险A", Assert.Single(removed.Draft!.Section("6.5").Items).CurrentText);
        Assert.Equal(FactOrigin.UserAdded, removed.Draft.Section("6.5").Items[0].Source);
        Assert.Equal(SectionCompletion.NeedsConfirmation, removed.Draft.Section("6.5").Completion);
    }

    [Fact]
    public void BarrierAndSignHaveRestorationWhileNonreversibleCheckDoesNot()
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        ticket = ticket with
        {
            UserFacts = [new UserTicketFact("SimpleOperation", "检查设备", [], true),
                new UserTicketFact("Barrier", "围栏位置", [], true),
                new UserTicketFact("Sign", "标牌位置", [], true)]
        };
        WorkTicketSession analyzed = new WorkTicketAnalyzer().Analyze(drawing, ticket);
        Assert.Equal(new[] { MeasureKind.Other, MeasureKind.Barrier, MeasureKind.Sign },
            analyzed.Analysis!.WorkGroupMeasures.Select(item => item.Kind));
        Assert.Equal(2, analyzed.Analysis.RestorationMeasures.Count);
        Assert.Equal(analyzed.Analysis.WorkGroupMeasures[2].Id,
            analyzed.Analysis.RestorationMeasures[0].SourceMeasureId);
        Assert.Equal(analyzed.Analysis.WorkGroupMeasures[1].Id,
            analyzed.Analysis.RestorationMeasures[1].SourceMeasureId);
        Assert.DoesNotContain(analyzed.Analysis.RestorationMeasures, item =>
            item.SourceMeasureId == analyzed.Analysis.WorkGroupMeasures[0].Id);
        Assert.Contains("围栏位置", analyzed.Draft!.Section("6.3").Text);
        Assert.Contains("标牌位置", analyzed.Draft.Section("16.1").Text);
    }

    [Fact]
    public void ReferenceGuardRejectsMissingBoundaryAndScope()
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        var root = new WorkTicketDataRoot(drawing.Id, [ticket]);
        WorkTicketReferenceGuard.Validate(drawing, root);
        root.Replace(ticket with { IsolationBoundaries = [new IsolationBoundary(Guid.NewGuid(), BoundarySide.Line)] });
        Assert.Contains("正在被工作票", Assert.Throws<InvalidOperationException>(() =>
            WorkTicketReferenceGuard.Validate(drawing, root)).Message);
        root.Replace(ticket with { WorkScopeIds = [Guid.NewGuid()] });
        Assert.Throws<InvalidOperationException>(() => WorkTicketReferenceGuard.Validate(drawing, root));
    }

    [Fact]
    public void SelectedTicketResolvesTwoTicketsIndependently()
    {
        WorkTicketSession first = WorkTicketSession.Create();
        WorkTicketSession second = WorkTicketSession.Create();
        var root = new WorkTicketDataRoot(Guid.NewGuid(), [first, second]);
        Assert.Equal(first.Id, root.Selected(first.Id)!.Id);
        Assert.Equal(second.Id, root.Selected(second.Id)!.Id);
        Assert.Null(root.Selected(null));
    }

    [Theory]
    [InlineData(TicketReferenceKind.Terminal)]
    [InlineData(TicketReferenceKind.Connection)]
    [InlineData(TicketReferenceKind.RingInterval)]
    [InlineData(TicketReferenceKind.GroundingPoint)]
    [InlineData(TicketReferenceKind.GroundingAccessPoint)]
    [InlineData(TicketReferenceKind.WorkScope)]
    public void ReferenceGuardCoversEveryTicketReferenceKind(TicketReferenceKind kind)
    {
        (DrawingDocument drawing, WorkTicketSession ticket) = ReadyTicket();
        ticket = ticket with { UserFacts = [new UserTicketFact("Risk", "测试",
            [new TicketReference(kind, Guid.NewGuid())], true)] };
        var root = new WorkTicketDataRoot(drawing.Id, [ticket]);
        Assert.Contains(kind.ToString(), Assert.Throws<InvalidOperationException>(() =>
            WorkTicketReferenceGuard.Validate(drawing, root)).Message);
    }

    private static (DrawingDocument Drawing, WorkTicketSession Ticket) ReadyTicket()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "票据测试");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        Guid first = cabinet.Intervals[0].CableTerminalId!.Value;
        Guid second = cabinet.Intervals[1].CableTerminalId!.Value;
        WorkScope scope = WorkScope.Create(Guid.NewGuid(), new BoundaryPoint(cabinet.Id, first, "线路侧"),
            new BoundaryPoint(cabinet.Id, second, "线路侧"), "检修范围", []);
        drawing.AddWorkScope(scope);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Task = new WorkTask("检修", "一号柜"),
            IsolationBoundaries = [new IsolationBoundary(cabinet.Intervals[0].SwitchDevices[0].Id,
                BoundarySide.Line)], WorkScopeIds = [scope.WorkScopeId]
        };
        return (drawing, ticket);
    }

    private sealed class VersionedPhrases : IWorkTicketPhraseLibrary
    {
        private readonly FirstKindPhraseLibrary _inner = new();
        public string Version => "test-new-version";
        public string Switching(MeasureFact measure, DrawingDocument drawing) => _inner.Switching(measure, drawing);
        public string WorkGroup(MeasureFact measure, DrawingDocument drawing) => _inner.WorkGroup(measure, drawing);
        public string Restoration(RestorationMeasure measure, DrawingDocument drawing) =>
            _inner.Restoration(measure, drawing);
    }

    private sealed class VersionedRules : IWorkTicketRulePack
    {
        private readonly FirstKindRulePack _inner = new();
        public string Version => "test-new-rules";
        public string? BoundaryIssue(DrawingDocument drawing, IsolationBoundary boundary) =>
            _inner.BoundaryIssue(drawing, boundary);
        public IEnumerable<MeasureFact> Switching(DrawingDocument drawing, IsolationBoundary boundary) =>
            _inner.Switching(drawing, boundary);
        public IEnumerable<MeasureFact> BoundaryDecorations(WorkTicketSession ticket, IsolationBoundary boundary) =>
            _inner.BoundaryDecorations(ticket, boundary);
        public IEnumerable<MeasureFact> WorkGroup(DrawingDocument drawing, WorkTicketSession ticket) =>
            _inner.WorkGroup(drawing, ticket);
        public IEnumerable<RetainedLivePart> RetainedLive(DrawingDocument drawing, WorkTicketSession ticket) =>
            _inner.RetainedLive(drawing, ticket);
        public string CoordinatedOutage(DrawingDocument drawing, WorkTicketSession ticket) =>
            _inner.CoordinatedOutage(drawing, ticket);
    }
    [Fact]
    public void UserSpecifiedReversibleOperationCarriesItsExactRestorationText()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "恢复措施测试");
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            UserFacts = [new UserTicketFact("OtherReversible", "设置临时遮栏", [], true,
                "拆除该临时遮栏")]
        };
        MeasureFact measure = Assert.Single(new FirstKindRulePack().WorkGroup(drawing, ticket));
        Assert.True(measure.Reversible);
        Assert.Equal("设置临时遮栏", new FirstKindPhraseLibrary().WorkGroup(measure, drawing));
        var restoration = new RestorationMeasure(measure.Id, measure.PhraseKey,
            measure.References, measure.Location, measure.Number, measure.Origin,
            measure.RestorationText);
        Assert.Equal("拆除该临时遮栏", new FirstKindPhraseLibrary().Restoration(restoration, drawing));
        Assert.Equal(measure.Id, restoration.SourceMeasureId);
    }

    [Fact]
    public void RetainedLiveRequiresExplicitElectricalStateOrUserConfirmation()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "带电事实测试");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "一号柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        SwitchDevice switchDevice = cabinet.Intervals[0].SwitchDevices.First();
        Terminal terminal = drawing.Terminals.First(item =>
            switchDevice.TerminalIds.Contains(item.Id) && item.ElectricalNodeId is not null);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            IsolationBoundaries = [new IsolationBoundary(switchDevice.Id, BoundarySide.Bus, terminal.Id)]
        };
        var rules = new FirstKindRulePack();
        Assert.Equal(FactOrigin.SystemRecommendation,
            Assert.Single(rules.RetainedLive(drawing, ticket), item =>
                item.References.Any(reference => reference.Kind == TicketReferenceKind.Device &&
                    reference.Id == switchDevice.Id)).Origin);
        drawing.ElectricalNodes.Single(item => item.Id == terminal.ElectricalNodeId)
            .SetElectricalState(ElectricalState.Energized);
        RetainedLivePart part = Assert.Single(rules.RetainedLive(drawing, ticket), item =>
            item.References.Any(reference => reference.Kind == TicketReferenceKind.Device &&
                reference.Id == switchDevice.Id));
        Assert.Equal(FactOrigin.ModelFact, part.Origin);
        Assert.Contains(part.References, item => item.Kind == TicketReferenceKind.Terminal &&
            item.Id == terminal.Id);
    }

    [Fact]
    public void DifferentIntervalsKeepBoundaryOrderAndTheirOwnSwitchStructure()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "测试图纸");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "环网柜",
        [
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(2,
                GroundingStructureKind.UpperIsolationGrounding, SwitchState.Closed,
                SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(3,
                GroundingStructureKind.LowerLowerGrounding, SwitchState.Closed,
                SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(4,
                GroundingStructureKind.UpperLowerGrounding, SwitchState.Closed,
                SwitchState.Closed, SwitchState.Open)
        ]));
        drawing.AddDevice(cabinet);
        var rules = new FirstKindRulePack();
        SwitchDevice[] switches = cabinet.Intervals.Select(interval => interval.SwitchDevices.First()).ToArray();
        MeasureFact[] first = rules.Switching(drawing, new IsolationBoundary(switches[0].Id, BoundarySide.Line)).ToArray();
        MeasureFact[] upper = rules.Switching(drawing, new IsolationBoundary(switches[1].Id, BoundarySide.Line)).ToArray();
        MeasureFact[] lower = rules.Switching(drawing, new IsolationBoundary(switches[2].Id, BoundarySide.Line)).ToArray();
        MeasureFact[] upperLower = rules.Switching(drawing,
            new IsolationBoundary(switches[3].Id, BoundarySide.Line)).ToArray();
        Assert.Equal(new[] { MeasureKind.OpenSwitch, MeasureKind.CloseGroundSwitch, MeasureKind.Sign },
            first.Select(item => item.Kind));
        Assert.Equal(new[] { MeasureKind.OpenSwitch, MeasureKind.OpenSwitch, MeasureKind.CloseGroundSwitch,
            MeasureKind.CloseBreaker, MeasureKind.Sign, MeasureKind.Sign }, upper.Select(item => item.Kind));
        Assert.Equal(new[] { MeasureKind.OpenSwitch, MeasureKind.OpenSwitch, MeasureKind.CloseGroundSwitch,
            MeasureKind.Sign }, lower.Select(item => item.Kind));
        Assert.DoesNotContain(lower, item => item.Kind == MeasureKind.CloseBreaker);
        Assert.DoesNotContain(upperLower, item => item.Kind == MeasureKind.CloseBreaker);
        SwitchDevice lowerIsolator = cabinet.Intervals[2].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.IsolationSwitch);
        Assert.Null(rules.BoundaryIssue(drawing, new IsolationBoundary(lowerIsolator.Id,
            BoundarySide.Bus, lowerIsolator.FirstTerminalId)));
        Assert.Null(rules.BoundaryIssue(drawing, new IsolationBoundary(lowerIsolator.Id,
            BoundarySide.Line, lowerIsolator.SecondTerminalId)));
        SwitchDevice lowerGround = cabinet.Intervals[2].SwitchDevices.Single(item =>
            item.SwitchKind == SwitchKind.GroundSwitch);
        Assert.NotNull(rules.BoundaryIssue(drawing, new IsolationBoundary(lowerGround.Id,
            BoundarySide.Line, lowerGround.SecondTerminalId)));
        Assert.Empty(rules.Switching(drawing, new IsolationBoundary(lowerGround.Id,
            BoundarySide.Line, lowerGround.SecondTerminalId)));
        IsolationBoundary selectedBoundary = new(switches[0].Id, BoundarySide.Line);
        WorkTicketSession proposed = WorkTicketSession.Create() with
        {
            UserFacts = [new UserTicketFact("RedCloth61", "负1邻近带电侧",
                [new TicketReference(TicketReferenceKind.Device, switches[0].Id)], false)]
        };
        Assert.Empty(rules.BoundaryDecorations(proposed, selectedBoundary));
        Assert.Equal("red-cloth-boundary", Assert.Single(rules.BoundaryDecorations(
            proposed with { UserFacts = [proposed.UserFacts[0] with { Confirmed = true }] },
            selectedBoundary)).PhraseKey);
    }

    [Fact]
    public void GroundingMeasureIsOrderedAndRestorationKeepsTargetNumberAndLocation()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "测试图纸");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "环网柜",
        [
            RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Closed, SwitchState.Open),
            RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Closed, SwitchState.Open)
        ]));
        drawing.AddDevice(cabinet);
        Guid firstTerminal = cabinet.Intervals[0].CableTerminalId!.Value;
        Guid secondTerminal = cabinet.Intervals[1].CableTerminalId!.Value;
        GroundingPoint point = GroundingPoint.Create(Guid.NewGuid(), firstTerminal, "负1电缆侧", "S01");
        drawing.AddGroundingPoint(point);
        WorkScope scope = WorkScope.Create(Guid.NewGuid(),
            new BoundaryPoint(cabinet.Id, firstTerminal, "线路侧"),
            new BoundaryPoint(cabinet.Id, secondTerminal, "线路侧"), "负1至负2", [point.GroundingPointId]);
        drawing.AddWorkScope(scope);
        WorkTicketSession ticket = WorkTicketSession.Create() with
        {
            Task = new WorkTask("检修", "环网柜"),
            IsolationBoundaries = [
                new IsolationBoundary(cabinet.Intervals[0].SwitchDevices[0].Id, BoundarySide.Line),
                new IsolationBoundary(cabinet.Intervals[1].SwitchDevices[0].Id, BoundarySide.Line)],
            WorkScopeIds = [scope.WorkScopeId],
            GroundingPointIds = [point.GroundingPointId]
        };
        WorkTicketSession analyzed = new WorkTicketAnalyzer().Analyze(drawing, ticket);
        Assert.Equal(6, analyzed.Analysis!.SwitchingMeasures.Count);
        Assert.Equal(cabinet.Intervals[0].SwitchDevices[0].Id,
            analyzed.Analysis.SwitchingMeasures[0].References[0].Id);
        Assert.Equal(cabinet.Intervals[1].SwitchDevices[0].Id,
            analyzed.Analysis.SwitchingMeasures[3].References[0].Id);
        Assert.Equal(new[] { MeasureKind.Check, MeasureKind.VerifyDead, MeasureKind.InstallGround },
            analyzed.Analysis.WorkGroupMeasures.Select(item => item.Kind));
        Assert.DoesNotContain(analyzed.Analysis.RestorationMeasures, item =>
            item.SourceMeasureId == analyzed.Analysis.WorkGroupMeasures[0].Id ||
            item.SourceMeasureId == analyzed.Analysis.WorkGroupMeasures[1].Id);
        RestorationMeasure restoration = Assert.Single(analyzed.Analysis.RestorationMeasures);
        MeasureFact install = analyzed.Analysis.WorkGroupMeasures[2];
        Assert.Equal(install.Id, restoration.SourceMeasureId);
        Assert.Equal(install.Number, restoration.Number);
        Assert.Equal(install.Location, restoration.Location);
        Assert.Equal(install.References, restoration.References);
        Assert.Contains("S01", analyzed.Draft!.Section("16.1").Text);
        Assert.Equal("无", analyzed.Draft.Section("6.2").Text);
        var exporter = new WorkTicketTextExporter();
        Assert.Equal(SectionCompletion.NeedsConfirmation, analyzed.Draft.Section("6.2").Completion);
        Assert.Equal("", exporter.Section(analyzed.Draft, "6.2"));
        Assert.Equal("无", exporter.Section(analyzed.ConfirmSection("6.2").Draft!, "6.2"));
        Assert.Equal("", exporter.Section(analyzed.Draft, "6.4"));
        Assert.Throws<InvalidOperationException>(() => analyzed.ConfirmSection("6.4"));
        Assert.Equal(SectionCompletion.NeedsInput, analyzed.Draft.Section("6.5").Completion);
        WorkTicketSession edited = analyzed.EditSection("6.5", "现场风险待核实");
        Assert.Equal(DraftOrigin.UserAdded, Assert.Single(edited.Draft!.Section("6.5").Items).Origin);
        Assert.Equal(SectionCompletion.Stale, edited.Invalidate().Draft!.Section("6.3").Completion);
        Assert.Equal(SectionCompletion.Stale, edited.Invalidate().Draft!.Section("6.5").Completion);
        WorkTicketSession regenerated = new WorkTicketAnalyzer().Analyze(drawing, edited);
        Assert.Equal("现场风险待核实", regenerated.Draft!.Section("6.5").Text);
        WorkTicketSession confirmed = analyzed.ConfirmSection("6.1");
        Assert.Equal(SectionCompletion.Completed,
            new WorkTicketAnalyzer().Analyze(drawing, confirmed).Draft!.Section("6.1").Completion);
        cabinet.Intervals[0].SwitchDevices[0].SetDispatchNumber("新编号");
        Assert.Equal(SectionCompletion.Stale, analyzed.EffectiveCompletion("6.1",
            analyzed.AnalyzedFingerprint != WorkTicketAnalyzer.Fingerprint(drawing, analyzed)));
        Assert.Equal(SectionCompletion.Stale, analyzed.EffectiveCompletion("6.5", true));
        WorkTicketSession changedWorkGroup = analyzed.EditSection("6.3", string.Join("\n",
            analyzed.Draft.Section("6.3").Items.Take(2).Select(item => item.CurrentText)
                .Append("现场人工更改的接地措施")));
        Assert.Equal(SectionCompletion.Stale, changedWorkGroup.Draft!.Section("16.1").Completion);
        Assert.Equal("", exporter.Section(changedWorkGroup.Draft, "16.1"));
        WorkTicketSession reanalyzed = new WorkTicketAnalyzer().Analyze(drawing, changedWorkGroup);
        Assert.Contains(reanalyzed.Analysis!.Issues, issue => issue.Contains("16.1"));
        Assert.Equal(SectionCompletion.Stale, reanalyzed.Draft!.Section("16.1").Completion);
        WorkTicketSession confirmedEdit = new WorkTicketAnalyzer().Analyze(drawing,
            changedWorkGroup.ConfirmSection("6.3"));
        Assert.Equal(SectionCompletion.Stale, confirmedEdit.Draft!.Section("16.1").Completion);
        WorkTicketSession regeneratedMeasures = new WorkTicketAnalyzer().Analyze(drawing,
            confirmedEdit.RestoreGeneratedSection("6.3"));
        Assert.Equal(SectionCompletion.NeedsConfirmation,
            regeneratedMeasures.Draft!.Section("16.1").Completion);
        Assert.Contains("S01", regeneratedMeasures.Draft.Section("16.1").Text);
        drawing.UpdateGroundingPoint(point.GroundingPointId, firstTerminal, "更新位置", "S02");
        WorkTicketSession updatedGround = new WorkTicketAnalyzer().Analyze(drawing, ticket);
        Assert.Contains("S02", updatedGround.Draft!.Section("6.3").Text);
        Assert.Contains("更新位置", updatedGround.Draft.Section("16.1").Text);
        GroundingPoint otherPoint = GroundingPoint.Create(Guid.NewGuid(), secondTerminal, "第二位置", "S03");
        drawing.AddGroundingPoint(otherPoint);
        WorkTicketSession twoGrounds = new WorkTicketAnalyzer().Analyze(drawing,
            ticket with { GroundingPointIds = [point.GroundingPointId, otherPoint.GroundingPointId] });
        Assert.Equal(2, twoGrounds.Analysis!.RestorationMeasures.Count);
        Assert.Equal(2, twoGrounds.Draft!.Section("16.1").Items.Count);
        WorkTicketSession switchedTarget = new WorkTicketAnalyzer().Analyze(drawing,
            ticket with { GroundingPointIds = [otherPoint.GroundingPointId] });
        Assert.Single(switchedTarget.Analysis!.RestorationMeasures);
        Assert.Contains("S03", switchedTarget.Draft!.Section("16.1").Text);
        Assert.DoesNotContain("S02", switchedTarget.Draft.Section("16.1").Text);
        Assert.Throws<InvalidOperationException>(() => drawing.UpdateGroundingPoint(
            point.GroundingPointId, secondTerminal, "非法换目标", "S04"));
    }
}
