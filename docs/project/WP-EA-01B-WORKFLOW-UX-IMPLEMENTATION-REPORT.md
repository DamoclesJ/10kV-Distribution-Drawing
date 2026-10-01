# WP-EA-01B GUI Workflow & Candidate UX Refinement — Implementation Report

## Scope and candidate

- Base: `wip/wp-ea-01b-fix2-reconcile` at `6d2bd3fe31de3bde052c42c5d0d119967709026e`.
- Candidate branch: `wip/wp-ea-01b-fix2-reconcile`.
- Implementation is limited to transient EA workflow/UI behavior, candidate naming and side selection, and native energization rendering identities.
- Formal `wp-ea-01b` was not changed. No closure or WP-EA-01C work was performed.

## Changes

- EA configuration stays open as a right-panel mode while drawing selections refresh candidates. Explicit Exit returns to Inspector; successful analysis also returns to Inspector while preserving the scenario and result.
- Current result display is controlled separately from analysis freshness and panel mode. The toolbar can hide or show a current result; stale results are never displayed. Switch state changes, including Undo/Redo, trigger reanalysis when a current result and seeds exist while preserving the user's overlay visibility preference.
- The EA panel uses a Chinese source-side selector with no default choice. Add and Replace require a resolvable selected side. Candidate and seed names share a business-fact projection for cabinet switches and pole-mounted devices.
- Device and pole labels use hazard aggregation (Energized, then Unknown, then Deenergized); conductor and electrical symbols retain exact electrical-state mapping.
- The existing PNG export path renders `session.Scene` and does not apply the transient EA style or toolbar visibility. This was audited and left unchanged; no export subsystem was added.

## Files changed

Production:

- `src/DistributionDrawing.Application/Energization/EnergizationAnalysisState.cs`
- `src/DistributionDrawing.Application/Energization/EnergizationUiService.cs`
- `src/DistributionDrawing.Desktop/Energization/DrawingOverlayVisibility.cs`
- `src/DistributionDrawing.Desktop/Energization/EnergizationPanel.xaml`
- `src/DistributionDrawing.Desktop/Energization/EnergizationPanel.xaml.cs`
- `src/DistributionDrawing.Desktop/MainWindow.xaml`
- `src/DistributionDrawing.Desktop/MainWindow.xaml.cs`
- `src/DistributionDrawing.Desktop/ProjectRuntimeSession.cs`
- `src/DistributionDrawing.Rendering.Wpf/Rendering/DrawingSceneBuilder.cs`
- `src/DistributionDrawing.Rendering.Wpf/Rendering/EnergizationSceneStyler.cs`
- `src/DistributionDrawing.Rendering.Wpf/Rendering/MixedPoleRenderer.cs`
- `src/DistributionDrawing.Rendering.Wpf/Rendering/RingCabinetRenderer.cs`
- `src/DistributionDrawing.Rendering.Wpf/Scene/ElectricalVisualIdentity.cs`

Tests:

- `tests/DistributionDrawing.Application.Tests/EnergizationUiTests.cs`
- `tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj`
- `tests/DistributionDrawing.Desktop.Tests/DrawingRightPanelAcceptanceTests.cs`
- `tests/DistributionDrawing.Desktop.Tests/EnergizationPanelTests.cs`
- `tests/DistributionDrawing.Desktop.Tests/EnergizationRuntimeTests.cs`
- `tests/DistributionDrawing.Rendering.Wpf.Tests/EnergizationNativeRenderingTests.cs`

## Windows Release automated regression

Each command ran the full project without a filter. `dotnet test` built the current project and its references in Release. Isolated build outputs are under the ignored repository `artifacts` directory so the running Desktop process did not have to be stopped. TRX files are under the Windows user's local temporary directory.

| Project | Test project | Command | Passed | Failed | Skipped | Total | TRX |
|---|---|---|---:|---:|---:|---:|---|
| Domain | `tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj` | `dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\FullRegression\Domain --logger "trx;LogFileName=Domain-workflow-ux.trx" --results-directory C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Domain -v minimal` | 177 | 0 | 0 | 177 | `C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Domain\Domain-workflow-ux.trx` |
| Application | `tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj` | `dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\FullRegression\Application --logger "trx;LogFileName=Application-workflow-ux.trx" --results-directory C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Application -v minimal` | 216 | 0 | 0 | 216 | `C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Application\Application-workflow-ux.trx` |
| Infrastructure | `tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj` | `dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\FullRegression\Infrastructure --logger "trx;LogFileName=Infrastructure-workflow-ux.trx" --results-directory C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Infrastructure -v minimal` | 113 | 0 | 0 | 113 | `C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Infrastructure\Infrastructure-workflow-ux.trx` |
| Rendering.Wpf | `tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj` | `dotnet test tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\FullRegression\Rendering.Wpf --logger "trx;LogFileName=Rendering.Wpf-workflow-ux.trx" --results-directory C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Rendering.Wpf -v minimal` | 654 | 0 | 0 | 654 | `C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Rendering.Wpf\Rendering.Wpf-workflow-ux.trx` |
| Desktop | `tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj` | `dotnet test tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\FullRegression\Desktop --logger "trx;LogFileName=Desktop-workflow-ux.trx" --results-directory C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Desktop -v minimal` | 358 | 0 | 0 | 358 | `C:\Users\antio\AppData\Local\Temp\WP-EA-01B-UX\FullRegression\Desktop\Desktop-workflow-ux.trx` |

Total: **1518 passed, 0 failed, 0 skipped**.

## Build and contract audit

- `dotnet build src/DistributionDrawing.sln -c Release --artifacts-path D:\Personal\Project\10kV-Distribution-Drawing\artifacts\WP-EA-01B-UX\SolutionBuild -v minimal`: succeeded, 0 errors (22 existing nullable/analyzer warnings).
- `git diff --check`: passed.
- Domain contract: no Domain files changed.
- V9 persistence: no Infrastructure or serialization files changed. `FormatVersion` remains V9. Energization result, overlay visibility, and EA panel mode remain runtime state; automated tests verify that analysis and overlay state are absent from project JSON.
- WTA business contract: no WorkTicket/WorkRange business implementation changed. Drawing overlay arbitration was adjusted only to let current EA display be controlled independently while preserving the existing panel/work-ticket exclusion.

## GUI state

No manual Windows GUI acceptance was performed in this implementation run. The Desktop process already running during verification was left undisturbed. GUI acceptance remains pending on the pushed candidate; this report does not claim WP-EA-01B closure.

