# Contributing

Changes must preserve inert package import, explicit validated mutation, deterministic plans, recovery data, Unity assembly boundaries, and existing `.meta` GUIDs.

Before editing, read `AGENTS.md` and `ImplementationPlan.md`. Keep work tied to the active phase and issue IDs, preserve unrelated worktree changes, and update the plan after completing an implementation batch.

Before proposing a change:

- Run `pwsh ./Tools/CI/Validate-PhaseZero.ps1`.
- Run the relevant EditMode and clean-install lanes for Addressables `2.7.6` and `2.9.1`.
- Confirm package import and removal remain inert.
- Report checks that were not run; do not infer or claim unavailable results.

Do not publish the package, create a GitHub Release, create a version tag, change licensing, or remove attribution without the separate authorizations recorded in the plan.
