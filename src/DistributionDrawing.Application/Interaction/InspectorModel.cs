namespace DistributionDrawing.Application.Interaction;

public sealed record InspectorProperty(string Key, string Value);

public sealed record InspectorModel(
    string Title,
    IReadOnlyList<InspectorProperty> Properties);
