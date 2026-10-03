# WP-GS-01 — Windows Acceptance Report

## State

- Governance: **OPEN**. This report does not mark WP-GS-01 CLOSED or ACCEPTED.
- Branch: `wp-gs-01`
- Implementation fix commit: `fb0e0333edd38984f7679ca7dbd2ac33dbd66949`
- Validation source: tracked code/test content identical to the fix commit, executed locally before commit/push. See [Implementation Report](WP-GS-01-IMPLEMENTATION-REPORT.md).
- FormatVersion: V9 (`ProjectFileFormat.CurrentVersion = Version9`).
- `TestResults/` remains untracked and was preserved.

## Windows Release build and automated suites

| Suite | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | --- |
| Full solution Release build | — | 0 errors | — | PASS (final build: 0 warnings) |
| Domain | 183 | 0 | 0 | PASS |
| Application | 243 | 0 | 0 | PASS |
| Infrastructure, including ProjectPersistence | 115 | 0 | 0 | PASS |
| Rendering.Wpf | 669 | 0 | 0 | PASS |
| Desktop | 362 | 0 | 0 | PASS |
| **Total tests** | **1,572** | **0** | **0** | **PASS on tracked source/test content** |

TRX files: `TestResults/WP-GS-01-Windows/{Domain,Application,Infrastructure,Rendering.Wpf,Desktop}.Final.trx`.

This Windows run occurred before commit/push, against tracked source/test content now recorded by fix commit `fb0e0333edd38984f7679ca7dbd2ac33dbd66949`. Repository workflow requires acceptance on the clean pushed candidate, so a full rerun remains pending.

## GUI cases

All GUI cases are **NOT RUN**. Attempting to launch the built application through the approved Windows computer-use interface returned `Computer Use was not approved to use desktop`. No alternate UI automation route was used.

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

WP-GS-01 closure conditions are **not met**. Required remaining work is to push the reviewed commits, rerun the full Windows build and all five test suites on a clean pushed candidate, and complete every GUI case on the same candidate. Keep the governance state OPEN until those checks pass.
