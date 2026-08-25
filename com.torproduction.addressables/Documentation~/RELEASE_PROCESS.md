# Intended release process

This document describes the gate order; it does not authorize a release.

1. Freeze a reviewed package candidate without changing the real package version during dry-run validation.
2. Pass repository static checks, manifest/metadata checks, API snapshot, EditMode tests, selected PlayMode tests, clean path install/removal, sample import/removal, archive creation/content comparison, archive install/removal, Package Validation Suite, and inertness/cleanliness checks.
3. Confirm required hosted Unity lanes for Addressables `2.7.6` and `2.9.1`. Run the prepared latest experimental lane separately when authorized. Paid Unity jobs must be manual or an explicitly authorized release condition, never an ordinary commit/PR schedule.
4. Record factual provenance and obtain owner/legal decisions for ownership, redistribution, relicensing authority, copyright lines, and required third-party notices. Apply only the approved license/notice changes and revalidate the resulting candidate.
5. Select the release version, update `package.json` and `CHANGELOG.md` together, and regenerate the archive. Confirm archive filename, embedded manifest, changelog heading, metadata, file list, and SHA-256 are consistent.
6. Review the staged diff, verify the remote branch has not advanced, commit, and rerun the final hosted matrix on the exact candidate commit.
7. Only with separate explicit authorization, create the signed/annotated `v<version>` tag and GitHub Release with the validated archive/checksum and release notes.
8. Registry publication and OpenUPM submission are separate explicitly authorized actions. Verify the published package by installing it into a clean project before announcing it.

## Publication safety

The repository intentionally contains no ordinary-push publication path. Release-readiness workflows are validation-only. No script in this package calls `npm publish`, creates a GitHub Release/tag, or submits to a registry/OpenUPM. Credentials must not be present in validation jobs, and workflow permissions remain least privilege.

Phase-verification tags are engineering checkpoints and are not semantic version tags or public releases. Phase 7 cannot be marked complete until legal decisions, the final authorized hosted verification, candidate-version consistency, and the separately authorized release actions required by `ImplementationPlan.md` are complete.
