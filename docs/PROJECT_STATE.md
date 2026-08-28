# Current project state

Last reviewed: 2026-08-28.

## Status

- Product: `com.torproduction.addressables`, an editor-only Unity Package Manager package.
- Current public preview: `0.1.0-preview.3`.
- Lifecycle: Phase 7 is closed; there is no active implementation phase or batch. New work is maintenance or a separately scoped feature.
- Integration: `main` is release-only, `develop` is the normal integration branch, and ordinary changes use isolated task branches or worktrees with pull requests to `develop`.
- Publication: the signed Git tag and GitHub pre-release are public. No npm, OpenUPM, Unity Registry, Asset Store, or other registry publication is authorized.
- Active work: none recorded here. A future task or issue owns its own scope; do not reopen the archived implementation plan as a backlog.

## Supported and prepared environments

| Unity | Addressables | Current claim | Evidence |
| --- | --- | --- | --- |
| `6000.0.78f1` | `2.7.6` | Declared production dependency and verified required lane | [Package manifest](../com.torproduction.addressables/package.json), [required workflow](../.github/workflows/unity_phase_zero.yml) |
| `6000.0.78f1` | `2.9.1` | Verified compatibility lane | [Required workflow](../.github/workflows/unity_phase_zero.yml), [package compatibility snapshot](../com.torproduction.addressables/Documentation~/COMPATIBILITY.md) |
| `6000.0.82f1` | `2.11.2` | Prepared manual experimental lane; not a supported release claim | [Experimental workflow](../.github/workflows/unity_latest_experimental.yml) |

The package manifest's exact `2.7.6` dependency is authoritative. A consumer override or a prepared workflow does not create a support claim without recorded verification.

## Current authorities

- Implemented product boundaries and behavior: [DECISIONS.md](DECISIONS.md), backed by its linked released source and tests.
- Development and validation: [DEVELOPMENT.md](DEVELOPMENT.md), backed by `Tools/CI/` and the workflows.
- Release status, immutable evidence, and publication boundaries: [RELEASES.md](RELEASES.md).
- Provenance, MIT notice, and attribution: [PROVENANCE_AUDIT.md](../PROVENANCE_AUDIT.md) and the released legal files it links.
- Package-user guidance: [package documentation index](../com.torproduction.addressables/Documentation~/com.torproduction.addressables.md).
- Historical phase, batch, and execution evidence: archived [ImplementationPlan.md](../ImplementationPlan.md), only when that history is relevant.

For current facts, prefer the released source, tests, manifest, workflows, Git refs, and committed release artifacts over narrative history.
