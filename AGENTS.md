# Repository instructions

## Product and authority

- This repository contains a Unity Package Manager product owned and branded by Tor Production. The package is under `com.torproduction.addressables/`; `AddressablesProject/` is its development and integration-test host.
- Do not reintroduce former-owner branding, URLs, namespaces, identifiers, or attribution unless the current task explicitly requires it and the owner has approved it for legal reasons.
- Read `ImplementationPlan.md` before changing code. Treat it as the source of truth for product decisions, issue IDs, phase and batch progress, and current scope.

## Safety and package boundaries

- Preserve Unity `.meta` files and their GUIDs. Move or rename a retained Unity asset together with its existing `.meta` file; never regenerate GUIDs casually.
- Package import, initialization, and configuration reads must be inert when setup is absent, incomplete, or invalid.
- Never silently modify Addressables settings, groups, entries, labels, Build Settings, scenes, assets, or other host-project state.
- Any editor command that mutates a project must be explicit, validated before mutation, scoped to package-owned/configured state, and provide clear failure reporting.
- Preserve Runtime, Editor, Samples, and Tests assembly boundaries. Do not make production assemblies depend on sample or test assemblies, and do not introduce editor APIs into runtime code.
- Keep the package project-agnostic. Avoid unnecessary dependencies, hardcoded host paths, game-specific types, scene states, asset conventions, and assumptions about pre-existing Addressables configuration.

## Working practice

- Inspect `git status` before editing. Preserve unrelated user changes and never overwrite or reformat them as collateral work.
- Make focused, incremental changes tied to the active phase and stable issue IDs. Stop at the scope boundary defined by the current task and `ImplementationPlan.md`.
- Validate changes with the relevant static checks, Unity compilation, and EditMode or PlayMode tests. Never claim an unavailable or unexecuted check passed; report exactly what could not be run and why.
- Do not commit, tag, push, publish, or enable publication unless the current task explicitly authorizes that action.
- After completing an implementation batch, update the plan status, execution progress, and affected phase or issue progress in `ImplementationPlan.md` before any authorized batch commit.
