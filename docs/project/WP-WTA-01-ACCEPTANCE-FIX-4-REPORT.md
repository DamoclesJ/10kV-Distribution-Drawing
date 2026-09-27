# WP-WTA-01 Acceptance Fix 4 Report

Status: Implemented locally; awaiting code review. No commit or push was made. Windows GUI acceptance remains pending.

Baseline: branch `wp-wta-01`, HEAD `54b121ecfc8e3ef6b15c8aee9502a32091edd0b9`. FormatVersion remains V8.

## Changes

1. **Previous UX root cause:** the work-range view was nested above the ordinary Inspector inside one `DockPanel`, so both could appear together. Opening it also entered a ticket range-edit lock. Its Confirm path required an Actual WorkScope, and Analyze rejected empty task text and missing Actual WorkScope.
2. **Right-side host integration:** the Inspector column now has one `Grid` host with mutually exclusive Work Range and Inspector contents. Both left and top toolbox entries still use the existing “工作范围” button and handler.
3. **Pending range lifecycle:** Boundary slots and the two text fields stay in an in-memory draft associated with the currently selected ticket. Hiding the panel or selecting another object/tool does not submit or clear it. It is discarded when the active document changes or closes; it is not persisted.
4. **Boundary picker:** only a row's “选择” button enters the short device-picking state. The next supported switch click is intercepted. Replacing a boundary temporarily retains the old value for picker cancellation; an accepted replacement and subsequent side choice update the draft.
5. **Side choice:** after a device click the side selector is shown immediately. It uses the existing `WorkTicketRangeSetup.AvailableSides` and `TryResolve` contracts: cabinet interval switches show bus/line side; pole switches show smaller/larger-number side. A valid side ends the picker; ordinary canvas selection then works again.
6. **Boundary highlight:** pending boundaries receive one whole-device steel-blue halo and an `[A]`, `[B]`, etc. label. The overlay does not use the selected side and does not express energized/deenergized state. It follows the pending draft while the panel is hidden, and is removed when that slot is replaced or removed. Confirmed-ticket overlay keeps its existing opt-in lifecycle.
7. **Simplified range panel:** contains WorkTask content, work object/location, ordered A/B/… boundary rows with side choice, add/remove controls, and one primary “确定” button. Both task fields start blank for a new ticket and may remain blank.
8. **Removed from the range panel:** Actual WorkScope rows and picker, Equipment WorkScope picker, ElectricalRange picker, add-existing/new-range controls, and range Cancel action. Legacy WorkScope models and ticket fields remain available for compatibility; the ticket page keeps their editors inside a collapsed advanced expander.
9. **Confirm and navigation:** Confirm validates at least one Boundary and resolves every Boundary through the existing validator. It writes task and ordered Boundary facts through the existing `CommandStack` command, creating the first WorkTicket only then, clears the transient buffer, and switches to the WorkTicket page.
10. **Analyze prerequisite:** the analyzer now requires only one or more valid Boundaries. It no longer requires nonempty WorkTask fields or any Actual WorkScope/Equipment WorkScope/ElectricalRange facts. Empty or unavailable sections remain section-level input/confirmation states. Energization analysis and topology traversal were not added.
11. **WorkTicket synchronization:** WorkTask fields on the ticket page are read-only and reflect the confirmed session. Boundary A/B/… are listed, with “返回图纸修改工作范围” as the editing path. Manual empty-ticket creation was removed from the page.
12. **Stale behavior:** Boundary order and side remain part of the analyzed input; changing either invalidates completed draft sections. Legacy WorkScope references are preserved but excluded from Analyzer range gating and its ticket-input fingerprint.

## Tests and validation

13. Added/updated tests cover empty WorkTask analysis, range confirmation without Actual WorkScope, invalid and mixed Boundary rejection, Boundary ordering and stale state, picker cancellation/side completion, mutually exclusive host markup, pending draft preservation by source-level acceptance assertions, ticket creation only on Confirm, and side-independent whole-device overlay geometry.
14. macOS validation:
   - Domain tests: 175 passed, 0 failed.
   - Application tests: 195 passed, 0 failed.
   - Infrastructure tests: 107 passed, 0 failed.
   - Full solution Windows-targeting cross-build: passed, 0 errors.
   - Desktop.Tests Windows-targeting cross-build: passed, 0 errors.
   - `git diff --check`: passed.
   - Desktop/WPF test execution was attempted, but the testhost exited before running tests because this macOS host has no `Microsoft.WindowsDesktop.App` 10.0 runtime. GUI acceptance remains pending. Cross-build success does not establish Windows runtime behavior.
   - Build output included a NuGet vulnerability-index cache access warning (`NU1900`); it did not prevent restore-free builds or tests.

## Files changed

15. Application: `WorkTicketAnalyzer.cs`, `WorkTicketRangeSetup.cs`; Desktop: `MainWindow.TicketRange.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `TicketRangePickerState.cs`, `WorkTicketOverlayBuilder.cs`, `WorkTicketRangeCommit.cs`, `WorkTicketWorkspace.xaml`, `WorkTicketWorkspace.xaml.cs`; removed `WorkTicketRangeNavigationState.cs`; tests: `WorkTicketAnalyzerTests.cs`, `WorkTicketRangeSetupTests.cs`, `TicketRangePickerStateTests.cs`, `WorkRangeEntryAcceptanceTests.cs`, `WorkTicketRangeCommandTests.cs`, and new `WorkTicketRangeOverlayTests.cs`.

## Remaining risks and Windows GUI acceptance

16. Windows acceptance is still required for actual WPF input routing, right-panel presentation, boundary halo/label placement, and the full user flow. Pending range data is intentionally transient and can be lost when the project closes or the active document changes. No FormatVersion change was made.
17. After review, commit and push the reviewed revision, then run these Windows GUI steps on that exact revision:
   1. Open the supplied acceptance drawing and verify FormatVersion V8.
   2. Click “工作范围” in both left-toolbox and top-toolbar layouts; verify one right-side Work Range panel appears, with no Inspector alongside it and no Actual WorkScope/ElectricalRange controls.
   3. Leave both task fields blank. Select Boundary A, click the ring-cabinet负3隔离刀闸, choose “线路侧”; verify whole-device halo and `[A]`, with no half-side or red/black styling.
   4. Select Boundary B, click the P02 isolator, choose the topology-resolved smaller/larger-number side; verify `[B]` and the displayed side name.
   5. Click an ordinary device. Verify normal selection and Inspector work. Reopen “工作范围”; verify text, A/B, side choices, and highlights remain. Add/edit a drawing object and repeat.
   6. Replace Boundary A and verify the old highlight is removed; remove Boundary B and verify its highlight disappears. Cancel a boundary picker with Esc/right-click and verify the prior completed boundary remains.
   7. Confirm one valid Boundary with no WorkScope and blank task fields; verify one WorkTicket is created/updated in the same session and the app switches to the WorkTicket page showing the task and ordered boundaries.
   8. Click Analyze and verify a Draft is created despite blank task text/object and missing Actual WorkScope. Verify section-level NeedsInput/NeedsConfirmation appears where facts are absent.
   9. Change Boundary order and then side; verify completed Draft state becomes stale and reanalysis is available.
   10. Open Work Range without confirming, navigate away and back, and verify no empty WorkTicket was created. Close/reopen the project only after confirming pending data is allowed to be discarded.

Repository status remains uncommitted on `wp-wta-01`; no push was made. Code review is the next step.
