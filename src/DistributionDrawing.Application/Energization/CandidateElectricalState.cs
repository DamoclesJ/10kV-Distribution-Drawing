using System.Collections.ObjectModel;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.Energization;

public sealed class CandidateElectricalState : ISwitchStateView
{
    private readonly DrawingDocument _drawing;
    private readonly IReadOnlyDictionary<Guid, SwitchState> _switchOverrides;
    private readonly IReadOnlyList<EnergizedSeed> _seeds;

    private CandidateElectricalState(
        DrawingDocument drawing,
        IReadOnlyDictionary<Guid, SwitchState> switchOverrides,
        EnergizationScenario candidateScenario)
    {
        _drawing = drawing;
        _switchOverrides = switchOverrides;
        ScenarioId = candidateScenario.Id;
        IsSourceSetComplete = candidateScenario.IsSourceSetComplete;
        _seeds = Array.AsReadOnly(candidateScenario.Seeds.ToArray());
    }

    public Guid ScenarioId { get; }

    /// <summary>Retained for V9 compatibility; it has no analysis meaning.</summary>
    public bool IsSourceSetComplete { get; }

    public IReadOnlyDictionary<Guid, SwitchState> SwitchOverrides => _switchOverrides;

    public IReadOnlyList<EnergizedSeed> Seeds => _seeds;

    public static CandidateElectricalState Create(
        DrawingDocument drawing,
        IReadOnlyDictionary<Guid, SwitchState>? switchOverrides,
        EnergizationScenario candidateScenario)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(candidateScenario);

        HashSet<Guid> switchIds = drawing.Devices.OfType<SwitchDevice>()
            .Select(device => device.Id)
            .ToHashSet();
        var copiedOverrides = new Dictionary<Guid, SwitchState>();
        if (switchOverrides is not null)
        {
            foreach ((Guid switchId, SwitchState state) in switchOverrides)
            {
                if (switchId == Guid.Empty || !switchIds.Contains(switchId))
                {
                    throw new ArgumentException(
                        $"Candidate switch '{switchId}' does not belong to the drawing.",
                        nameof(switchOverrides));
                }
                if (!Enum.IsDefined(state))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(switchOverrides),
                        $"Candidate state '{state}' is not defined.");
                }
                copiedOverrides.Add(switchId, state);
            }
        }

        var readOnlyOverrides = new ReadOnlyDictionary<Guid, SwitchState>(copiedOverrides);
        return new CandidateElectricalState(drawing, readOnlyOverrides, candidateScenario);
    }

    public SwitchState? GetSwitchState(SwitchDevice switchDevice)
    {
        ArgumentNullException.ThrowIfNull(switchDevice);
        return _switchOverrides.TryGetValue(switchDevice.Id, out SwitchState state)
            ? state
            : switchDevice.SwitchState;
    }

    public EnergizationScenario CreateScenarioSnapshot() =>
        new(ScenarioId, _seeds, IsSourceSetComplete);
}
