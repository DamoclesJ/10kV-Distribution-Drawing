# Development Workflow

## Collaboration Model

- **Repository / Git:** implementation source of truth.
- **ChatGPT:** requirements, architecture, planning, and review.
- **Codex:** implementation, tests, and repository changes.
- **Windows:** WPF runtime and final UI validation.

Stable project facts belong in repository documentation. Chat history and old Codex threads are not durable facts or substitutes for the current repository state.

## Standard Work Package

1. Discuss requirements and boundaries in ChatGPT.
2. Define one independently verifiable Work Package.
3. Start a new Codex thread for that Work Package.
4. Before editing, run:

   ```bash
   git status
   git branch --show-current
   git rev-parse HEAD
   git rev-parse origin/main
   ```

5. Read `docs/project/STATUS.md`, this workflow, and the formal documentation and code relevant to the Work Package.
6. Perform a read-only audit before making changes.
7. Implement only the agreed scope.
8. Run the relevant tests and builds.
9. By default, do not commit or push yet.
10. Provide a structured report covering the baseline, audit, changes, verification, remaining risks, and repository status.
11. Review the result in ChatGPT.
12. After confirmation, commit and push.
13. If Windows validation is required, pull the committed version on Windows and perform runtime and visual validation.
14. Close the Work Package only after it passes its defined acceptance checks.
15. Update `STATUS.md` or `ROADMAP.md` when the project state or committed plan changes.

## Operating Rules

- One Codex thread should correspond to one Work Package.
- Do not depend on an old thread's chat context as a source of truth.
- Do not expand scope because an adjacent change is convenient.
- Uncommitted code cannot be validated on another machine through `git pull`.
- A version required for Windows acceptance must be committed and pushed first.
- A macOS WPF build pass is not a Windows runtime test pass.
- Feature implementation and Windows validation may use a two-step commit flow, but the implementation and validation state must remain explicit.
- When blocked, find the root cause. Do not conceal invariant violations through Save-time cleanup, swallowed exceptions, or equivalent workarounds.
- Move stable facts reached at each stage into repository documentation instead of leaving them only in chat history.

## Windows Performance Diagnostic Baseline

- Performance diagnostics are opt-in and must be disabled for ordinary use. Diagnostic records must not enter V7 project files or alter Domain, Layout, Routing, Rendering, CommandStack, Undo / Redo, pointer frequency, `LastValid`, rollback, continuity, or final MouseUp behavior.
- Each collection must record the application commit, project-file SHA-256, `FormatVersion`, object / Connection / Guide counts, selected target, drag coordinates and trajectory, hardware, Windows build, .NET runtime, GPU / driver, monitor refresh rate, resolution, scaling, power mode, and whether diagnostics were enabled.
- Use one `GestureId` for the pointer-down-to-commit/cancel lifetime, one `UpdateId` for each processed drag MouseMove, one `BuildAttemptId` for Candidate / Rollback / CommitBefore / CommitAfter, one `SceneBuildId` per scene build, and the stable `ConnectionId` for connection routing. Aggregate exclusive named phases or the enclosing total, never a parent plus its children.
- A `VisualCollection` publication timestamp proves only that UI-thread visual publication returned. It does not prove compositor presentation or displayed FPS. Any display-presentation claim requires a separately approved capture or presentation methodology.
- Run diagnostic-off control passes as well as diagnostic-on passes to characterize collection overhead. Do not freeze performance thresholds from macOS builds, synthetic-only samples, or a single Windows run.
- Windows validation requires a committed and pushed revision. Until then, local preparation remains reviewable but cannot be pulled or accepted on the Windows machine.
