# Contributing

Contributions should be focused, project-agnostic, and tied to an issue or phase in the repository `ImplementationPlan.md`.

## Safety rules

- Preserve Unity `.meta` files and GUIDs when moving retained assets.
- Keep Runtime, Editor, Samples, and Tests assembly boundaries. Production code must not depend on tests or samples, and editor APIs must not enter runtime code.
- Import and configuration reads must remain inert. Mutations require explicit intent, complete validation, scoped ownership, stale-state rejection, and clear failure/recovery behavior.
- Do not clear unrelated Addressables settings, groups, entries, labels, Build Settings rows, scenes, assets, or build data.
- Do not add project-specific types, paths, scenes, conventions, former-owner branding, or private/reflected Addressables APIs.
- Do not remove attribution, change the license, publish, or create release/version tags until the recorded ownership and legal gates are resolved.

## Validation

Run the repository static gates, package manifest/content validation, the relevant Unity EditMode tests, and any selected PlayMode/integration tests. Package-layout changes also require clean local path installation/removal, sample import/removal, archive creation/content validation, archive installation/removal, and Package Validation Suite in disposable projects.

Generated projects, `Library`, logs, reports, archives, screenshots, and caches belong outside tracked package content. Never claim a check passed unless its result was actually inspected.

## Pull requests

Use a semantic title such as `fix: reject stale group plan` or `docs: clarify content-update recovery`. Explain the safety boundary, tests run, and any compatibility/provenance impact. Third-party code or assets require an identified source, version/commit, license, and attribution decision before inclusion.
