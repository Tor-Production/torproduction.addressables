# Tor Production Addressables

This repository develops `com.torproduction.addressables`, an editor-first Unity Package Manager package for explicit, previewable, and recoverable Addressables automation. The package is under `com.torproduction.addressables/`; `AddressablesProject/` is the pinned Unity 6 development and integration-test host.

## Start here

- [Current project state](docs/PROJECT_STATE.md) — current version, maintenance status, compatibility, active work, and release boundaries.
- [Internal documentation map](docs/README.md) — authoritative routing for architecture, development, releases, provenance, and history.
- [Package documentation](com.torproduction.addressables/Documentation~/com.torproduction.addressables.md) — installation, configuration, workflows, safety, and troubleshooting for package users.
- [Contributing](CONTRIBUTING.md) — repository contribution entry point.

The public preview is `0.1.0-preview.3`. Exact release and compatibility evidence is routed from `docs/PROJECT_STATE.md` and `docs/RELEASES.md`; the completed `ImplementationPlan.md` is retained only as historical evidence.

## Development

Use [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for branch policy, validation commands, Unity-test selection, and documentation-only checks. The repository is post-release: `main` is release-only, ordinary work starts from remote `develop`, and changes are reviewed through task branches and pull requests.

Current provenance and approved notice wording are recorded in [PROVENANCE_AUDIT.md](PROVENANCE_AUDIT.md). Do not change licensing, attribution, tags, Releases, or publication state without separate explicit authorization.
