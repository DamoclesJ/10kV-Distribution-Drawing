# WP-GS-01 — Windows Acceptance Report

## State

- Governance: **OPEN**. This report does not mark WP-GS-01 CLOSED or ACCEPTED.
- Branch: `wp-gs-01`
- Implementation fix commit: `fb0e0333edd38984f7679ca7dbd2ac33dbd66949`
- Clean pushed candidate: `9e16ffb3b998659eadf6a5710bf1d42997483728`
- FormatVersion: V9 (`ProjectFileFormat.CurrentVersion = Version9`).
- `TestResults/` remains untracked and was preserved.

## Windows Release build and automated suites

| Suite | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | --- |
| Full solution Release build | — | 0 errors | — | PASS (22 warnings) |
| Domain | 183 | 0 | 0 | PASS |
| Application | 243 | 0 | 0 | PASS |
| Infrastructure, including ProjectPersistence | 115 | 0 | 0 | PASS |
| Rendering.Wpf | 669 | 0 | 0 | PASS |
| Desktop | 362 | 0 | 0 | PASS |
| **Total tests** | **1,572** | **0** | **0** | **PASS on clean pushed candidate** |

TRX files: `TestResults/WP-GS-01-Windows/{Domain,Application,Infrastructure,Rendering.Wpf,Desktop}.Candidate.trx`.

The Release build and all five test suites passed on clean pushed candidate `9e16ffb3b998659eadf6a5710bf1d42997483728`. `git diff --exit-code` passed after testing; no tracked files were changed by validation. `FormatVersion` remains V9.

## GUI cases

All GUI cases are **NOT RUN**. The first attempt returned `Computer Use was not approved to use desktop`; the later candidate launch request timed out with `Computer Use app approval timed out`. No alternate UI automation route was used.

| Case | Status |
| --- | --- |
| No valid EA: GroundingPoint and Seed/switch edits preserve existing behavior; no implicit EA | NOT RUN |
| Energized Terminal/GAP grounding rejection is mutation-free; Deenergized Terminal/GAP grounding is allowed | NOT RUN |
| PoleSwitch sides are evaluated independently | NOT RUN |
| Ordinary RingCabinet energized GroundSwitch close is blocked | NOT RUN |
| UpperLowerGrounding and LowerLowerGrounding behavior | NOT RUN |
| UpperIsolationGrounding: GroundSwitch Closed + Breaker Open is allowed; energized Breaker close is blocked | NOT RUN |
| Upstream energization is blocked by an existing GroundingPoint | NOT RUN |
| Upstream energization is blocked by existing Effective Grounding | NOT RUN |
| Seed analysis that energizes a grounding location is rejected atomically | NOT RUN |
| After NoSeeds, adding a Seed does not restore EA until explicit analysis | NOT RUN |
| Undo / Redo cannot bypass Grounding Safety | NOT RUN |
| Grounding removal, opening switches, and Seed removal remain available | NOT RUN |
| Simple blocking messages only; no new safety panel or locator | NOT RUN |
| Save / reopen remains functional and FormatVersion remains V9 | NOT RUN |
| Existing EA rendering remains correct | NOT RUN |

## Closure gate

WP-GS-01 closure conditions are **not met**. Complete every GUI case on the final candidate SHA before starting Closure Review. Keep the governance state OPEN until the GUI cases pass.
