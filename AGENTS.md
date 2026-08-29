# Repository instructions

## Required context and authority

- This repository contains the Tor Production Unity Package Manager product under `com.torproduction.addressables/`; `AddressablesProject/` is its development and integration-test host.
- Before substantive repository work, read this file and `docs/PROJECT_STATE.md`. Load only the task-specific sources routed from `docs/README.md` after that.
- `docs/PROJECT_STATE.md` is authoritative for current version, maintenance status, supported environments, active work, and release boundaries.
- `docs/DECISIONS.md` records only decisions verified against released source, tests, manifests, workflows, or final release evidence. Source, tests, and authoritative configuration take precedence if documentation drifts.
- `PROVENANCE_AUDIT.md` is authoritative for the current provenance, license-notice, and attribution decision. The existing MIT and third-party notices are approved for the current release; changing them requires separate explicit authorization.
- `ImplementationPlan.md` is retained historical implementation and release evidence. It is not current scope, a live backlog, or mandatory context.
- User-facing package behavior is documented under `com.torproduction.addressables/Documentation~/`. Repository development and release routing is under `docs/`.

## Safety and package boundaries

- Do not reintroduce former-owner branding, URLs, namespaces, identifiers, or attribution unless the task explicitly requires it and the owner has approved it for legal reasons.
- Preserve Unity `.meta` files and GUIDs. Move or rename a retained Unity asset with its existing `.meta`; create a new GUID only for an explicitly new asset identity after checking serialized-reference consequences.
- Package import, initialization, and configuration reads must remain inert when setup is absent, incomplete, or invalid.
- Never silently modify Addressables settings, groups, entries, labels, Build Settings, scenes, assets, or other host-project state.
- Project mutations must be explicit, validated before mutation, limited to package-owned or configured state, and report failures and recovery clearly.
- Preserve production, test, and sample assembly boundaries. Do not introduce editor APIs into player code or production dependencies on tests or samples.
- Keep the package project-agnostic: avoid hardcoded host paths, game-specific types or states, private/reflected Addressables APIs, and assumptions about existing Addressables configuration.

## Working practice

- Use `$codex-project-workflow:project-context-optimizer` before substantive repository work, generated task prompts, and implementation plans when available. Follow its non-blocking fallback in `.agents/CODEX_WORKFLOW.md` when unavailable.
- This is a post-release repository. For repository changes, use `$codex-project-workflow:release-git-flow` when available: keep `main` release-only, start normal work from the current remote `develop`, isolate it in a task branch/worktree, validate it, and target `develop` with a pull request. Read-only work needs no branch.
- Workflow guidance does not authorize commits, pushes, merges, tags, releases, deployment, publication, paid Unity workflows, or changes to external state. Obtain the authorization required by the task.
- Inspect `git status` before editing. Preserve unrelated work and do not hide, move, overwrite, or reformat it as collateral work.
- Use the current task or issue as the scope boundary. Do not append routine maintenance work to the historical implementation plan.
- Run validation proportional to the changed surface using `docs/DEVELOPMENT.md`. Report every skipped or unavailable check accurately.
- Never move, recreate, overwrite, or delete release tags or GitHub Releases. Publication, licensing changes, and `v*` tags always require separate explicit authorization.
