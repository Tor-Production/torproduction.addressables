# Release-readiness map

The authorized public preview candidate is `0.1.0-preview.3`. The repository `ImplementationPlan.md` is the authoritative live evidence record; package contents describe the gates without claiming the release exists before publication.

| Requirement | Implementation or required evidence |
| --- | --- |
| EditMode and integration coverage | Deterministic package test suites plus development-host and disposable clean-project results |
| Selected PlayMode coverage | A disposable known Addressable is built, selected through Addressables’ built-in packed Play Mode builder, loaded, verified, removed, and followed by inertness checks |
| Clean UPM path install/removal | Addressables `2.7.6` and `2.9.1` lanes compile, pass exact test counts, remove the package, and preserve project state |
| Sample workflow and removal | The declared Basic Setup sample imports through Unity Package Manager, creates default Addressables settings, activates without validation errors, proposes and applies the expected scene group/schemas/label/entry, converges on a second Analyze, removes cleanly, and preserves unrelated state |
| Package Validation Suite | Official PVS `0.86.0-preview` runs in a disposable project; the unmodified XML launcher outcome remains accurately recorded, while the hash-identical bundled `FindMissingDocs.exe` must exit `0` with empty stdout/stderr |
| Archive | `New-PackageArchive.ps1` validates the source, compares source/archive file lists, validates extraction, and writes the exact `.tgz` plus SHA-256 |
| Archive installation/removal | The exact `.tgz` installs as a local UPM dependency, passes tests and inertness checks, then removes cleanly |
| Metadata | Manifest, changelog, archive, checksum, release notes, tag, and GitHub Release agree on `0.1.0-preview.3` |
| License and notices | MIT text and both approved copyright lines are present; the package-root notice contains only the approved minimal template attribution |
| Required hosted lanes | One manual workflow run targets the exact candidate; Addressables `2.7.6` and `2.9.1` jobs both succeed and their XML artifacts independently verify zero failures, skips, and inconclusive tests |
| Release protection | A protected GitHub environment named `release` requires manual approval; top-level workflow permissions are read-only and only the release job receives `contents: write` |
| Signed tags | a new Preview 3 verification tag and signed `v0.1.0-preview.3` peel to the exact hosted-tested candidate; GitHub marks the semantic tag signature Verified |
| Tag installation | A disposable project installs from the signed Git URL, compiles, tests, removes inertly, and preserves host state |
| GitHub pre-release | A verified draft carries the exact archive, checksum, and changelog-derived release notes; it remains draft until the owner manually tests the exact downloaded asset and explicitly authorizes publication |
| Registry scope | No npm, OpenUPM, Unity Registry, Asset Store, or other registry publication occurs |

The experimental Unity `6000.0.82f1` / Addressables `2.11.2` workflow and future Addressables `4.0.1` work are non-blocking future compatibility investigations.
