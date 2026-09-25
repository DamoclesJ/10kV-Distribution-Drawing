using DistributionDrawing.Domain.Professional;

namespace DistributionDrawing.Application.WorkTickets;

public enum TicketReferenceKind { Device, Terminal, Connection, RingInterval, GroundingPoint, GroundingAccessPoint, WorkScope }
public sealed record TicketReference(TicketReferenceKind Kind, Guid Id);
public enum BoundarySide { Unknown, Bus, Line, SmallerNumber, LargerNumber, Source, Load }
public sealed record IsolationBoundary(Guid DeviceId, BoundarySide Side, Guid? TerminalId = null, Guid? ConnectionId = null);
public sealed record IsolationScope(IReadOnlyList<IsolationBoundary> Boundaries);
public sealed record WorkTask(string Content, string WorkObject);
public sealed record UserTicketFact(string Kind, string Text, IReadOnlyList<TicketReference> References,
    bool Confirmed, string? RestorationText = null);
public enum FactOrigin { ModelFact, SystemRecommendation, UserAdded }
public enum DraftOrigin { Generated, RuleDerived, UserAdded, UserEdited, Confirmed, Rejected }
public enum SectionCompletion { Empty, Generated, NeedsInput, NeedsConfirmation, Completed, Stale }
public enum MeasureKind { Check, OpenSwitch, CloseGroundSwitch, CloseBreaker, RemoveFuseTube, VerifyDead, InstallGround, Sign, Barrier, RedCloth, Other }
public sealed record MeasureFact(
    Guid Id, string Section, MeasureKind Kind, string PhraseKey,
    IReadOnlyList<TicketReference> References, string? Location = null,
    string? Number = null, bool Reversible = false, FactOrigin Origin = FactOrigin.ModelFact,
    string? RestorationText = null);
public sealed record RetainedLivePart(string Description, IReadOnlyList<TicketReference> References, FactOrigin Origin);
public sealed record RiskItem(string Description, string? Mitigation, FactOrigin Origin);
public sealed record RestorationMeasure(Guid SourceMeasureId, string PhraseKey,
    IReadOnlyList<TicketReference> References, string? Location, string? Number, FactOrigin Origin,
    string? RestorationText = null);
public sealed record WorkTicketAnalysis(
    IReadOnlyList<MeasureFact> SwitchingMeasures,
    IReadOnlyList<MeasureFact> WorkGroupMeasures,
    IReadOnlyList<RetainedLivePart> RetainedLiveParts,
    IReadOnlyList<RiskItem> Risks,
    IReadOnlyList<RestorationMeasure> RestorationMeasures,
    IReadOnlyList<string> Issues);
public sealed record DraftItem(
    Guid Id, string GeneratedText, string CurrentText, DraftOrigin Origin,
    IReadOnlyList<TicketReference> RelatedModelRefs, Guid? SourceMeasureId = null)
{
    public FactOrigin Source { get; init; } = FactOrigin.ModelFact;
    public bool IsUserEdited { get; init; }
    public DraftItem Edit(string text) => this with
    { CurrentText = text, Origin = DraftOrigin.UserEdited, IsUserEdited = true };
}
public sealed record SectionDraft(string Code, IReadOnlyList<DraftItem> Items, SectionCompletion Completion)
{
    public string Text => string.Join(Environment.NewLine, Items.Select(item => item.CurrentText));
}
public sealed record WorkTicketDraft(IReadOnlyList<SectionDraft> Sections)
{
    public SectionDraft Section(string code) => Sections.Single(section => section.Code == code);
    public string CopySixSections() => new WorkTicketTextExporter().SixSections(this);
}

// This business state is separate from DrawingDocument and is required in V8.
public sealed record WorkTicketSession(
    Guid Id,
    WorkTask Task,
    IReadOnlyList<IsolationBoundary> IsolationBoundaries,
    IReadOnlyList<Guid> WorkScopeIds,
    IReadOnlyList<Guid> GroundingPointIds,
    IReadOnlyList<UserTicketFact> UserFacts,
    WorkTicketAnalysis? Analysis,
    WorkTicketDraft? Draft,
    string? AnalyzedFingerprint)
{
    public string? RulePackVersion { get; init; }
    public string? PhraseLibraryVersion { get; init; }
    private static readonly HashSet<string> ModelDependentSections = ["6.1", "6.2", "6.3", "6.4", "6.5", "16.1"];

    public static WorkTicketSession Create() => new(Guid.NewGuid(), new WorkTask("", ""), [], [], [], [], null, null, null);
    public IsolationScope GetOutageScope() => new(IsolationBoundaries);
    public WorkTicketSession Invalidate() => this with { Draft = Draft is null ? null : new WorkTicketDraft(
        Draft.Sections.Select(section => ModelDependentSections.Contains(section.Code)
            ? section with { Completion = SectionCompletion.Stale } : section).ToArray()) };

    public SectionCompletion EffectiveCompletion(string code, bool fingerprintChanged)
    {
        SectionDraft? section = Draft?.Sections.SingleOrDefault(item => item.Code == code);
        if (section is null) return SectionCompletion.Empty;
        return fingerprintChanged && ModelDependentSections.Contains(code)
            ? SectionCompletion.Stale : section.Completion;
    }
    public WorkTicketSession EditSection(string code, string text)
    {
        if (Draft is null) throw new InvalidOperationException("Analyze before editing a section.");
        SectionDraft section = Draft.Section(code);
        string[] lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        DraftItem[] items = section.Items.Select((existing, index) =>
        {
            string current = index < lines.Length ? lines[index] : "";
            return current == existing.CurrentText ? existing : existing.Edit(current);
        }).ToArray();
        if (lines.Length > items.Length)
            items = [..items, ..lines.Skip(items.Length).Select(line => new DraftItem(
                Guid.NewGuid(), "", line, DraftOrigin.UserAdded, [])
                { Source = FactOrigin.UserAdded, IsUserEdited = true })];
        return this with { Draft = new WorkTicketDraft(Draft.Sections.Select(existing =>
            existing.Code == code ? new SectionDraft(code, items, SectionCompletion.NeedsConfirmation) :
            code == "6.3" && existing.Code == "16.1"
                ? existing with { Completion = SectionCompletion.Stale } : existing).ToArray()) };
    }

    public WorkTicketSession RestoreGeneratedSection(string code)
    {
        if (Draft is null) throw new InvalidOperationException("Analyze before restoring a section.");
        _ = Draft.Section(code);
        return this with { Draft = new WorkTicketDraft(Draft.Sections.Select(section =>
            section.Code == code ? section with
            {
                Items = section.Items.Where(item => item.SourceMeasureId is not null ||
                    item.Source != FactOrigin.UserAdded).Select(item => item with
                {
                    CurrentText = item.GeneratedText,
                    Origin = DraftOrigin.RuleDerived,
                    IsUserEdited = false
                }).ToArray(),
                Completion = SectionCompletion.NeedsConfirmation
            } : code == "6.3" && section.Code == "16.1"
                ? section with { Completion = SectionCompletion.Stale } : section).ToArray()) };
    }

    public WorkTicketSession ConfirmSection(string code)
    {
        if (Draft is null) throw new InvalidOperationException("Analyze before confirming a section.");
        if (Draft.Section(code).Completion == SectionCompletion.Stale)
            throw new InvalidOperationException("该栏目已失效，请先重新分析。");
        if (Draft.Section(code).Completion is SectionCompletion.Empty or SectionCompletion.NeedsInput)
            throw new InvalidOperationException("该栏目还有待补充的事实，不能确认。");
        if (Draft.Section(code).Items.Count == 0 || Draft.Section(code).Items.Any(item =>
                string.IsNullOrWhiteSpace(item.CurrentText)))
            throw new InvalidOperationException("该栏目存在空白措施，请先补全或在工作票准备区调整结构化事实。");
        if (Draft.Section(code).Items.Any(item => item.CurrentText.Contains("待填写编号", StringComparison.Ordinal)))
            throw new InvalidOperationException("该栏目还有待填写的接地线编号。");
        if (code == "6.4" && Draft.Section(code).Items.Any(item =>
                item.Source == FactOrigin.SystemRecommendation && !item.IsUserEdited))
            throw new InvalidOperationException("6.4 仍含待核实的带电关系；请先补充并确认现场事实，或人工编辑该栏。");
        return this with { Draft = new WorkTicketDraft(Draft.Sections.Select(section =>
            section.Code == code ? section with
            {
                Items = section.Items.Select(item => item with { Origin = DraftOrigin.Confirmed }).ToArray(),
                Completion = SectionCompletion.Completed
            } : section).ToArray()) };
    }
}
