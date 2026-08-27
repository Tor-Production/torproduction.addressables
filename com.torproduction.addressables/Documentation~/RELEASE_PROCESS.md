# Release process

The current public preview candidate is `0.1.0-preview.2`. Its manifest version, changelog heading, archive filename, signed tag, and GitHub Release title must agree exactly.

1. Apply the approved MIT license and minimal Stan’s Assets template notice without changing retained Unity `.meta` files or GUIDs.
2. Sanitize repository-owned reachable history, preserving the Phase 0–6 boundary and verification tags. Review the rewritten range and push only with a validated force-with-lease.
3. Pass repository static checks, manifest/metadata checks, actionlint, API/assembly snapshots, EditMode tests, selected PlayMode validation, clean path install/removal for Addressables `2.7.6` and `2.9.1`, sample import/removal, Package Validation Suite with the recorded fail-closed XML-documentation fallback, direct bundled `FindMissingDocs.exe`, archive source/file-list validation, archive installation/removal, and inertness/clean-worktree checks.
4. Commit and push the exact release candidate. An ordinary branch push must not publish or start Unity validation.
5. Dispatch the manual **Unity compatibility validation** workflow exactly once for that candidate. Both required Addressables jobs and their authoritative XML results must pass for the exact SHA.
6. Preserve the existing `phase-7-verified` tag, create a clearly named additional Preview 2 verification tag on the hosted-tested candidate, confirm it triggers no workflow, then create and locally verify the cryptographically signed `v0.1.0-preview.2` tag on the same commit.
7. Push only the signed semantic tag. It may trigger only the non-Unity GitHub pre-release workflow.
8. The release job waits for approval in the protected `release` environment. It verifies GitHub’s tag signature, the exact successful manual Unity run and both jobs, version agreement, approved notice hashes, the deterministic `.tgz` and committed SHA-256, and release notes exported from this changelog.
9. The workflow creates a draft GitHub pre-release with the archive, checksum, and release-notes assets. It never publishes to a registry.
10. Download the exact draft `.tgz`, independently verify its checksum and contents, and install both it and the signed Git tag in disposable Unity projects. Confirm compilation, all tests, inert removal, and absence of the hidden `Samples~` metadata warning.
11. Stop with the release still draft. Publish only after the owner manually tests that exact downloaded draft asset in a clean project and explicitly authorizes publication.

## Publication safety

Ordinary pushes and pull requests never publish. Paid Unity validation remains `workflow_dispatch` only. Pushing the additional Preview 2 verification tag triggers nothing. The release workflow accepts only `v0.1.0-preview.2`, has top-level read-only permissions, and grants `contents: write` only to its protected release-creation job.

This release does not call `npm publish`, submit to OpenUPM, publish to Unity Registry or Asset Store, or use any other registry. Those actions remain separate future decisions.
