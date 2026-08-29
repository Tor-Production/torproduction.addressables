# Release policy and evidence

## Current release

The current public preview is `0.1.0-preview.3`. The signed annotated tag `v0.1.0-preview.3` and verification tag `phase-7-preview-3-verified` both peel to candidate commit `4db569212015776c26e323c622a466166434637d`. The committed archive checksum is `5f5114372c019c296b7dedd1bc08da4d4b4739eb52ea3587f825752abe167485`.

Authoritative evidence:

- [Package manifest](../com.torproduction.addressables/package.json)
- [Changelog](../com.torproduction.addressables/CHANGELOG.md)
- [Committed Preview 3 checksum](../Release/com.torproduction.addressables-0.1.0-preview.3.tgz.sha256)
- [Protected GitHub pre-release workflow](../.github/workflows/release_github_prerelease.yml)
- [Release-readiness validation](../Tools/CI/Validate-ReleaseReadiness.ps1)
- [Public GitHub pre-release](https://github.com/Tor-Production/torproduction.addressables/releases/tag/v0.1.0-preview.3)
- Archived detailed execution evidence in [ImplementationPlan.md](../ImplementationPlan.md)

## Branch and publication policy

- `main` is release-only. Normal maintenance and feature work starts from remote `develop` and returns through a pull request to `develop`.
- Existing phase, verification, and semantic release tags are immutable. Never move, recreate, overwrite, or delete them.
- Existing GitHub Releases and drafts are retained unless a separate task explicitly authorizes a change. Opening an ordinary pull request must not alter release state.
- No npm, OpenUPM, Unity Registry, Asset Store, or other registry publication is authorized for the current release.
- Paid Unity compatibility validation, new `v*` tags, GitHub Release changes, registry publication, and deployment each require separate explicit authorization.

The workflow and validation scripts are authoritative for executable gates. Package files under `com.torproduction.addressables/Documentation~/` are the documentation snapshot shipped with the released package; their historical pre-publication wording is not live repository status.

## Future release work

A future release task must start from current repository and remote evidence, not replay the archived phase protocol. It must identify its exact version, candidate commit, required compatibility lanes, authorization boundary, checksums, protected workflow, and stopping point before changing any release object.
