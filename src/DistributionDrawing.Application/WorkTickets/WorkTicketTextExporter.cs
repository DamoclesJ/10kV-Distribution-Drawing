namespace DistributionDrawing.Application.WorkTickets;

public interface IWorkTicketTextExporter
{
    string Section(WorkTicketDraft draft, string code);
    string SixSections(WorkTicketDraft draft);
}

public sealed class WorkTicketTextExporter : IWorkTicketTextExporter
{
    private static readonly string[] Codes = ["6.1", "6.2", "6.3", "6.4", "6.5", "16.1"];

    public string Section(WorkTicketDraft draft, string code)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!Codes.Contains(code)) throw new ArgumentOutOfRangeException(nameof(code));
        if (draft.Section(code).Completion != SectionCompletion.Completed ||
            draft.Section(code).Items.Any(item => item.CurrentText.Contains("待填写编号", StringComparison.Ordinal)) ||
            code == "16.1" &&
            draft.Sections.FirstOrDefault(section => section.Code == "6.3")?
                .Items.Any(item => item.IsUserEdited) == true)
            return "";
        return string.Join(Environment.NewLine, draft.Section(code).Items
            .Where(item => code == "6.2" || item.Source != FactOrigin.SystemRecommendation ||
                item.Origin == DraftOrigin.Confirmed)
            .Select(item => item.CurrentText)).Trim();
    }

    public string SixSections(WorkTicketDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Codes.Any(code => draft.Section(code).Completion != SectionCompletion.Completed ||
            string.IsNullOrWhiteSpace(Section(draft, code))))
            throw new InvalidOperationException("六栏尚未全部确认，不能复制。");
        return string.Join(Environment.NewLine + Environment.NewLine,
            Codes.Select(code => $"{code}  {Section(draft, code)}"));
    }
}
