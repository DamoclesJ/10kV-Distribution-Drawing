# Repository Operating Instructions

- Before starting work, read `docs/project/STATUS.md`, `docs/project/WORKFLOW.md`, and the formal documentation relevant to the task.
- Use one Codex thread for one independently verifiable Work Package.
- Verify the branch, working tree, `HEAD`, and `origin/main`, then perform a read-only audit before editing.
- Keep changes within the agreed scope; repository files and Git are the implementation source of truth.
- Run the relevant tests and builds before reporting, and include the resulting repository status.
- By default, report for review before committing or pushing.
- Windows-required validation must use a committed and pushed version. A macOS WPF build pass does not establish Windows runtime or final UI validation.
- Keep feature implementation and Windows validation states explicit when they occur in separate steps.
- Update `docs/project/STATUS.md` or `docs/project/ROADMAP.md` when committed work changes project state or plans.
