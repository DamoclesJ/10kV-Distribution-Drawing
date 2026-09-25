using DistributionDrawing.Application.WorkTickets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class WorkTicketTextExporterTests
{
    [Fact]
    public void CopiesOnlyConfirmedBusinessTextForEachSectionAndAllSix()
    {
        string[] codes = ["6.1", "6.2", "6.3", "6.4", "6.5", "16.1"];
        WorkTicketDraft draft = new(codes.Select((code, index) => new SectionDraft(code,
            [new DraftItem(Guid.NewGuid(), $"原文{index}", $"确认文字{index}", DraftOrigin.Confirmed,
                [], Guid.NewGuid()) { Source = index == 3 ? FactOrigin.SystemRecommendation : FactOrigin.ModelFact }],
            SectionCompletion.Completed)).ToArray());
        var exporter = new WorkTicketTextExporter();
        foreach (var (code, index) in codes.Select((code, index) => (code, index)))
            Assert.Equal($"确认文字{index}", exporter.Section(draft, code));
        string all = exporter.SixSections(draft);
        Assert.All(codes, code => Assert.Contains(code + "  确认文字", all));
        Assert.DoesNotContain("NeedsConfirmation", all);
        Assert.DoesNotContain("Stale", all);
        Assert.DoesNotContain("SystemRecommendation", all);
        Assert.DoesNotContain("SourceMeasureId", all);
        Assert.DoesNotContain("原文", all);
    }

    [Fact]
    public void UnconfirmedAndStaleSectionsDoNotCopy()
    {
        WorkTicketDraft draft = new([new SectionDraft("6.2",
            [new DraftItem(Guid.NewGuid(), "无", "无", DraftOrigin.RuleDerived, [])],
            SectionCompletion.NeedsConfirmation)]);
        Assert.Equal("", new WorkTicketTextExporter().Section(draft, "6.2"));
        Assert.Throws<InvalidOperationException>(() => new WorkTicketTextExporter().SixSections(draft));
    }

    [Fact]
    public void ConfirmedEditedAndUserAddedItemsCopyWithoutInternalMetadata()
    {
        Guid sourceId = Guid.NewGuid();
        SectionDraft section = new("6.3",
            [new DraftItem(Guid.NewGuid(), "旧措施", "人工修改措施", DraftOrigin.Confirmed, [], sourceId)
                { IsUserEdited = true },
             new DraftItem(Guid.NewGuid(), "", "新增措施", DraftOrigin.Confirmed, [])
                { Source = FactOrigin.UserAdded, IsUserEdited = true }],
            SectionCompletion.Completed);
        string copied = new WorkTicketTextExporter().Section(new WorkTicketDraft([section]), "6.3");
        Assert.Equal("人工修改措施" + Environment.NewLine + "新增措施", copied);
        Assert.DoesNotContain(sourceId.ToString(), copied);
        Assert.DoesNotContain("旧措施", copied);
    }
}
