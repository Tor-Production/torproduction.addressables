# Release-readiness map

Phase 7A covers non-legal, non-publishing preparation only. It does not complete Phase 7 or authorize a hosted paid run, license change, tag, GitHub Release, registry publication, or OpenUPM submission.

| Phase 7 requirement | Implementation/evidence | Phase 7A state |
| --- | --- | --- |
| EditMode regression coverage | Package test assembly plus `Tools/CI/Test-CleanInstall.ps1`; exact-count result artifacts | Implemented; local results recorded in `ImplementationPlan.md` |
| Relevant PlayMode coverage | A marked disposable fixture builds a known Addressable, selects Unity's built-in packed Play Mode builder, loads the asset, verifies no package runtime production surface/components, removes the fixture, and rechecks inertness | Passed locally; exact result recorded in the plan |
| Package Validation Suite | Disposable `Run-PackageValidation.ps1`, PVS `0.86.0-preview`, two-pass import, exported log/report, inertness assertion | Executed: all non-XML-doc validations pass; the suite remains failed because its bundled `FindMissingDocs.exe` throws a dependency `TypeLoadException` only when launched by Unity. No blanket exception was added; exact blocker is recorded in the plan. |
| Clean UPM path install/removal | Disposable project, two Addressables lanes, exact EditMode count, compilation-log scan, inertness before/after removal | Implemented; results recorded in the plan |
| Sample import/removal | Marked clean-project lane imports the declared sample, validates GUID/config/scene/missing scripts, removes it, preserves a sentinel, and removes the package | Implemented; result recorded in the plan |
| Archive creation/content validation | `Validate-PackageManifest.ps1` plus `New-PackageArchive.ps1`; source/archive file-list comparison, extracted validation, filename/version/manifest/changelog checks, SHA-256 | Implemented; dry-run archive only |
| Archive installation/removal | Archive-aware clean-install harness uses a local `.tgz` dependency and repeats tests/inertness/removal | Implemented; result recorded in the plan |
| Metadata consistency | Manifest/content validator checks name, SemVer, dependency, Unity line, author/repository, sample, current changelog heading, top-level allowlist, meta pairing/GUID uniqueness, prohibited content/links | Implemented |
| Repository cleanliness/inertness | All generated projects, caches, logs, reports, and archives use system temp or ignored `artifacts`; `Assert-InertProject.ps1`; final `git status` | Implemented |
| Required compatibility lanes | Manual hosted Unity matrix retains Addressables `2.7.6` and `2.9.1` | Preserved; not dispatched in Phase 7A |
| Current compatibility lane | Separate manual experimental Unity `6000.0.82f1` / Addressables `2.11.2` workflow | Prepared, not yet verified, no schedule |
| Workflow hardening | Immutable third-party SHAs, official Actions replacements, least privilege, paid Unity triggers restricted, local YAML/actionlint gate | Implemented; stable GameCI Node 20 exception documented until an upstream stable Node 24 release |
| User documentation | Installation, compatibility, configuration, preview/Apply, recovery, groups, scenes, dependencies, builds, CLI, samples, limitations, troubleshooting, contribution, and intended release process | Implemented |
| Provenance/licensing | `PROVENANCE_AUDIT.md` separates evidence/assumptions and lists exact owner/legal questions; notices preserved | Audit complete; decisions blocked on owner/legal review |
| Candidate version/license/notices | Requires owner/legal answers and separately selected release version | Blocked; no real bump or license change in Phase 7A |
| Final hosted verification | Required paid Unity lanes on exact Phase 7 candidate | Blocked pending owner response and separate authorization |
| Tag/release/publication | Semantic version tag, GitHub Release, registry/OpenUPM actions | Intentionally absent and unauthorized |

The authoritative execution status, exact commit/run IDs, and remaining blockers are maintained in the repository `ImplementationPlan.md`.
