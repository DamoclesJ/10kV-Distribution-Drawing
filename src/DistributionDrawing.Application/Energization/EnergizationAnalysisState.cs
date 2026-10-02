using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Energization;

namespace DistributionDrawing.Application.Energization;

public enum EnergizationFreshness { NotAnalyzed, Current, Stale }

/// <summary>Transient analysis result for one open document.</summary>
public sealed class EnergizationAnalysisState
{
    private readonly EnergizationUiService _service = new();

    public EnergizationFreshness Freshness { get; private set; } =
        EnergizationFreshness.NotAnalyzed;
    public EnergizationResult? LatestResult { get; private set; }
    public EnergizationResult? CurrentResult =>
        Freshness == EnergizationFreshness.Current && LatestResult?.IsSuccess == true
            ? LatestResult : null;
    public IReadOnlyList<EnergizationDiagnosticDisplay> LatestDiagnostics { get; private set; } = [];
    public bool OverlayRequested { get; private set; } = true;
    public bool CanShowOverlay => OverlayRequested && CurrentResult is not null;
    public event EventHandler? Changed;

    public void Execute(
        DrawingDocument drawing,
        EnergizationScenario scenario,
        bool showOverlay = true)
    {
        Publish(_service.Analyze(drawing, scenario));
        if (showOverlay) OverlayRequested = true;
    }

    public void Publish(EnergizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LatestResult = result;
        LatestDiagnostics = _service.Diagnostics(result);
        Freshness = EnergizationFreshness.Current;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Invalidate()
    {
        if (Freshness != EnergizationFreshness.Current) return;
        Freshness = EnergizationFreshness.Stale;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetOverlayRequested(bool requested)
    {
        if (OverlayRequested == requested) return;
        OverlayRequested = requested;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
