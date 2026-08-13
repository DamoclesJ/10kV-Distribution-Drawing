namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed record RingCabinetLayoutRule
{
    public const string DefaultRuleId = "builtin:ring-cabinet/default-v1";

    public RingCabinetLayoutRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            throw new ArgumentException("Layout rule ID is required.", nameof(ruleId));
        }

        RuleId = ruleId.Trim();
    }

    public string RuleId { get; }

    public static RingCabinetLayoutRule Default { get; } = new(DefaultRuleId);
}
