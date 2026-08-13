using System.Collections.Frozen;

namespace DistributionDrawing.Application.Templates.RingCabinets.Library;

public sealed class RingCabinetTemplateLibrary
{
    private readonly IReadOnlyList<RingCabinetTemplate> _templates;
    private readonly FrozenDictionary<TemplateId, RingCabinetTemplate> _templatesById;

    public RingCabinetTemplateLibrary(IEnumerable<RingCabinetTemplate> templates)
    {
        RingCabinetTemplate[] values = templates?.ToArray()
            ?? throw new ArgumentNullException(nameof(templates));

        if (values.Any(template => template is null))
        {
            throw new ArgumentException(
                "Template collection cannot contain null entries.",
                nameof(templates));
        }

        TemplateId? duplicateId = values
            .GroupBy(template => template.TemplateId)
            .FirstOrDefault(group => group.Skip(1).Any())?
            .Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"Template collection contains duplicate TemplateId '{duplicateId}'.",
                nameof(templates));
        }

        _templates = Array.AsReadOnly(values);
        _templatesById = values.ToFrozenDictionary(template => template.TemplateId);
    }

    public IReadOnlyList<RingCabinetTemplate> Templates => _templates;

    public bool TryGet(
        TemplateId templateId,
        out RingCabinetTemplate? template)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        return _templatesById.TryGetValue(templateId, out template);
    }
}
