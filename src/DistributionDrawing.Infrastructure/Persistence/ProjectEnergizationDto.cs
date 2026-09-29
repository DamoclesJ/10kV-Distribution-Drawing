using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectEnergizedSeedDto(
    Guid SeedId,
    Guid BoundaryDeviceId,
    string Side);

public sealed record ProjectEnergizationScenarioDto(
    Guid DocumentId,
    Guid ScenarioId,
    bool IsSourceSetComplete,
    IReadOnlyList<ProjectEnergizedSeedDto> Seeds)
{
    public static ProjectEnergizationScenarioDto Empty(Guid documentId) =>
        new(documentId, Guid.NewGuid(), false, []);
}

internal static class ProjectEnergizationMapper
{
    public static ProjectEnergizationScenarioDto ToDto(
        EnergizationScenario scenario,
        Guid documentId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var dto = new ProjectEnergizationScenarioDto(
            documentId,
            scenario.Id,
            scenario.IsSourceSetComplete,
            scenario.Seeds.Select(seed => new ProjectEnergizedSeedDto(
                seed.Id, seed.BoundaryDeviceId, seed.Side.ToString())).ToArray());
        Validate(dto, documentId);
        return dto;
    }

    public static EnergizationScenario ToDomain(
        ProjectEnergizationScenarioDto? dto,
        Guid documentId)
    {
        if (dto is null) throw new InvalidDataException("V9 project requires EnergizationScenario.");
        Validate(dto, documentId);
        return new EnergizationScenario(dto.ScenarioId,
            dto.Seeds.Select(seed => new EnergizedSeed(
                seed.SeedId, seed.BoundaryDeviceId, ParseSide(seed.Side))),
            dto.IsSourceSetComplete);
    }

    public static void Validate(ProjectEnergizationScenarioDto dto, Guid documentId)
    {
        if (dto.DocumentId != documentId || dto.ScenarioId == Guid.Empty || dto.Seeds is null)
            throw new InvalidDataException("Energization scenario identity or seeds are invalid.");
        if (dto.Seeds.Any(seed => seed is null || seed.SeedId == Guid.Empty ||
            seed.BoundaryDeviceId == Guid.Empty || !IsKnownSide(seed.Side)) ||
            dto.Seeds.Select(seed => seed.SeedId).Distinct().Count() != dto.Seeds.Count)
            throw new InvalidDataException("Energization scenario contains invalid seeds.");
    }

    private static bool IsKnownSide(string? side) => side is
        "Bus" or "Line" or "SmallerNumber" or "LargerNumber";

    private static EnergizationSide ParseSide(string side) => side switch
    {
        "Bus" => EnergizationSide.Bus,
        "Line" => EnergizationSide.Line,
        "SmallerNumber" => EnergizationSide.SmallerNumber,
        "LargerNumber" => EnergizationSide.LargerNumber,
        _ => throw new InvalidDataException($"Unknown energization side '{side}'.")
    };
}
