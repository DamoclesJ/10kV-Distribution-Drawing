namespace DistributionDrawing.Application.Templates.RingCabinets;

public sealed record TemplateId
{
    public TemplateId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Template ID is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
