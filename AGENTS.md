# Repository instructions

## Product and authority

- This repository contains a Unity Package Manager product owned and branded by Tor Production. The package is under `com.torproduction.addressables/`; `AddressablesProject/` is its development and integration-test host.
- Do not reintroduce former-owner branding, URLs, namespaces, identifiers, or attribution unless the current task explicitly requires it and the owner has approved it for legal reasons.
- Treat code provenance, licensing, redistribution rights, and legally required attribution as unresolved until the owner records confirmation in `ImplementationPlan.md`. Do not create a public release, publish the package, change the license, or remove legally required notices before that confirmation.
- Read `ImplementationPlan.md` before changing code. Treat it as the source of truth for product decisions, issue IDs, phase and batch progress, and current scope.

## Safety and package boundaries

- Preserve Unity `.meta` files and their GUIDs. Move or rename a retained Unity asset together with its existing `.meta` file; regenerate a GUID only when the current task explicitly requires a new asset identity and all serialized-reference consequences have been checked.
- Package import, initialization, and configuration reads must be inert when setup is absent, incomplete, or invalid.
- Never silently modify Addressables settings, groups, entries, labels, Build Settings, scenes, assets, or other host-project state.
- Any editor command that mutates a project must be explicit, validated before mutation, scoped to package-owned/configured state, and provide clear failure reporting.
- Preserve Runtime, Editor, Samples, and Tests assembly boundaries. Do not make production assemblies depend on sample or test assemblies, and do not introduce editor APIs into runtime code.
- Keep the package project-agnostic. Avoid unnecessary dependencies, hardcoded host paths, game-specific types, scene states, asset conventions, and assumptions about pre-existing Addressables configuration.

## Working practice

- At the start of every substantive repository task, generated prompt, or implementation plan, use `$codex-project-workflow:project-context-optimizer` when the `codex-project-workflow` plugin is available and provide its compact **Codex setup** recommendation. At handoff, state whether the next action should continue in the same task or start a new one. If its context-debt audit confirms reorganization is required, treat the dedicated, user-authorized reorganization task as a prerequisite to later feature work. Plugin installation and the non-blocking fallback are documented in `.agents/CODEX_WORKFLOW.md`.
- This repository is post-release. For every repository-changing implementation task, use `$codex-project-workflow:release-git-flow` when the plugin is available: keep `main` release-only, use `develop` as the integration base, work in an isolated task branch or worktree, validate the exact change, and finish with a pull request to the appropriate target. Read-only work does not require a branch or pull request. If the plugin is unavailable, follow these embedded rules directly rather than blocking the task. This workflow governs authorized work but does not itself authorize commits, pushes, merges, tags, releases, or publication.
- Inspect `git status` before editing. Preserve unrelated user changes and never overwrite or reformat them as collateral work.
- Make focused, incremental changes tied to the active phase and stable issue IDs. Stop at the scope boundary defined by the current task and `ImplementationPlan.md`.
- Validate changes with the relevant static checks, Unity compilation, and EditMode or PlayMode tests. Never claim an unavailable or unexecuted check passed; report exactly what could not be run and why.
- Do not commit, tag, push, publish, or enable publication unless the current task explicitly authorizes it or the phase-completion protocol in `ImplementationPlan.md` applies. Publication, release creation, and `v*` tags always require separate explicit authorization.
- After completing an implementation batch, update the plan status, execution progress, and affected phase or issue progress in `ImplementationPlan.md` before any authorized batch commit.
- Phase status must always name the latest completed phase, the next incomplete phase, and the active batch. After a phase passes its completion protocol, advance to the next incomplete phase without reopening completed phases for unrelated cleanup.
