# Development and validation

## Starting work

1. Read [AGENTS.md](../AGENTS.md) and [PROJECT_STATE.md](PROJECT_STATE.md).
2. Inspect `git status`, branches, worktrees, remotes, and the task-relevant source and tests.
3. For normal repository changes, update from remote `develop`, create an isolated task branch/worktree, and target `develop` with the pull request. Keep `main` release-only.
4. Use the current task or issue for scope. Read the archived [ImplementationPlan.md](../ImplementationPlan.md) only when historical execution evidence is relevant.

## Validation sources

The scripts under [`Tools/CI/`](../Tools/CI/) and workflows under [`.github/workflows/`](../.github/workflows/) are authoritative for executable checks. Common static gates include:

```powershell
pwsh ./Tools/CI/Validate-PhaseZero.ps1
pwsh ./Tools/CI/Validate-PackageManifest.ps1 -PackagePath ./com.torproduction.addressables
```

Select Unity EditMode, PlayMode, clean-install, sample, archive, or Package Validation Suite checks according to the affected product surface. State exact Unity and Addressables versions and report skipped or unavailable checks honestly. Paid Unity workflows are manual and require separate authorization.

## Documentation-only changes

For changes confined to repository Markdown outside the package subtree:

- run `git diff --check`;
- validate every changed relative link, heading fragment, and referenced repository path;
- search current documents for stale authority statements and paths;
- compare protected tree IDs, remote release tags and peeled targets, `main`, release artifacts, and checksums;
- re-run the context-debt measurements when routing or mandatory context changes.

Do not run Unity merely for a documentation-only diff. If a non-documentation file changes unexpectedly, stop and investigate before deciding which product validation is required.

## Generated evidence

Keep generated projects, `Library`, caches, archives, logs, reports, screenshots, and temporary validation scripts outside tracked package content. Pull requests must include exact reproducible commands and observable pass criteria rather than claiming an inferred result.
