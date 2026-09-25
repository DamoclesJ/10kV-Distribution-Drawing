# WP-WTA-01 Fix Report — 2026-09-25

Status: Implementation Complete / Code Review Passed / Awaiting Windows Acceptance. This is the `wp-wta-01` Windows acceptance candidate. Windows automated and professional acceptance have not started.

The second independent code review found four additional High paths. They were fixed in this same work package. A focused re-review then found one High case where an edited generated item lost its source fact; that case was fixed and the independent reviewer confirmed the remaining High closed. No new blocking issue was found in the focused final review.

## Architecture decision

`FormatVersion = V8` is the current development baseline. A V8 project requires `WorkTicketData`; new projects write an empty section. V8 Save, Load and complete ticket-data round trip are covered. The loader rejects every version below or above V8, and rejects a V8 payload missing `WorkTicketData`. Unknown V8 fields are rejected rather than preserved. V7 migration, upgrade Save As, old-file round trip and old-executable write compatibility were canceled by the user. This persistence change does not alter the closed Electrical Model facts.

## Review findings and disposition

| Finding | Fix |
| --- | --- |
| V7 silent work-ticket data loss | Required V8 section, strict version gate and unknown-field rejection; removed the unused historical migration pipeline and upgrade-save path. |
| 6.1 ignores electrical side | Rule checks `Side`, selected switch terminal, optional interval cable connection and interval structure. Grounding measures require a resolved line side. Unknown, unsupported or mismatched sides yield an issue and 6.1 `NeedsInput`; bus side omits grounding. |
| Edit equals confirm | `EditSection` preserves individual items and sets `NeedsConfirmation`; only `ConfirmSection` sets `Completed`. Empty items and unresolved placeholders cannot be confirmed. |
| 6.3 versus 16.1 | Stable measure IDs and draft item IDs preserve `SourceMeasureId`, generated/current text and edit provenance. Restoration is generated only from reversible structured 6.3 measures. Manual 6.3 text changes keep 16.1 stale, including after 6.3 confirmation. A “restore generated 6.3” action clears overrides before reanalysis; no Chinese-text parsing creates restoration facts. |
| Referenced object deletion | Command-level validation checks boundary, scope, grounding and all typed ticket references after deletion; rejection undoes the deletion atomically and gives the ticket ID. The selection planner and direct work-scope, grounding point, GAP, device and overhead-line delete paths are guarded. |
| 6.2 “无” | Generated as `NeedsConfirmation`; explicit confirmation is required before copy. |
| 6.5 risk stale | Risk facts now enter the input fingerprint, and 6.5 participates in stale completion and copy checks. |
| Edit provenance | Generated items remain distinct; unchanged items keep their origin; edited items retain stable IDs and source measure IDs; new text lines become separate user-added items. `IsUserEdited` survives confirmation and persistence. |
| Rule and phrase versions | Rule-pack (including risk rules) and phrase-library versions persist on the ticket. Version changes invalidate completed drafts and reanalysis requires renewed confirmation. |
| Multiple ticket overlay | Drawing overlay reads the selected ticket, and ticket selection triggers a scene render. |
| Copy contract | The exporter copies only completed sections, excludes unresolved recommendations, and refuses an incomplete six-section copy. Direct tests cover all six sections, edited and added text, recommendations and metadata exclusion. |
| Second review: duplicate facts on reanalysis | Null-source items are matched by generated content, source and references; only distinct manual free-text items are carried forward. Repeated analysis and fact removal are tested. |
| Second review: direct deletion bypass | Pole attachment, cable termination and cable segment direct delete controllers now use the same guarded command as the existing direct device, line, work-scope, grounding and GAP paths. A real pole-switch deletion regression test was added for ticket rejection, rollback and subsequent Undo; execution awaits Windows. |
| Second review: terminal side inference | Interval switch terminal nodes are checked against the exact load-switch or integrated-feeder topology, including the lower-lower structure. Ground switches cannot be boundary switches. Standalone switches without provable interval topology remain `NeedsInput`. The UI no longer selects a terminal by ordinal automatically. |
| Second review: unsaved edits lost on 6.3 restore | The restore action captures setup and all pending section text in one work-ticket command before restoring 6.3. Another section's pending text remains in the resulting session. |
| Focused re-review: edited item loses changed source | An unmatched edited generated item remains in the draft as user-authored text, keeps its item identity and previous generated text for comparison, and returns to `NeedsConfirmation` with an issue. Changing or removing its Risk fact is tested. |

## Verification

- Solution build: passed on macOS.
- Domain: 175 passed, 0 failed, 0 skipped.
- Application: 183 passed, 0 failed, 0 skipped.
- Infrastructure: 106 passed, 0 failed, 0 skipped.
- Desktop and Rendering.Wpf test projects: cross-build passed.
- Desktop and Rendering.Wpf test execution: **Automated verification blocked** on macOS because `Microsoft.WindowsDesktop.App` 10.0 is unavailable. No Windows runtime result is claimed.
- `git diff --check`: passed.

Added and updated tests cover side and terminal/connection resolution, interval topology, pole fuse path, repeated reanalysis and fact removal, reversible restoration and nonreversible measures, number/location/grounding-selection changes, edit/confirm states, retained-live confirmation, risk add/remove/change, rule/phrase invalidation, V8 persistence and version rejection, dependency checks, command rollback/Undo/Redo, selected-ticket identity and exporter contracts. Old migration and compatibility tests were removed because those product requirements were canceled.

## Remaining risks and Windows prerequisites

The V0.1 analyzer remains conservative when a boundary side or site electrical fact cannot be resolved; such measures need professional confirmation. Free-text changes to 6.3 reversible measures deliberately leave 16.1 stale until the structured source is corrected or generated text is restored and analysis rerun. The UI shell, delete command paths, ticket switching and visual overlay still require Windows test execution and professional GUI review.

This candidate must be validated on Windows as one fixed committed revision. WP-WTA-01 remains open until Windows automated and professional acceptance complete; no Closed Archive is created by this candidate.
