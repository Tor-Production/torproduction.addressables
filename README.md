# Tor Production Addressables

This repository develops `com.torproduction.addressables`, an editor-first Unity Package Manager package for safe, previewable Addressables automation. `AddressablesProject/` is the pinned Unity 6 integration-test host; the package itself is under `com.torproduction.addressables/`.

The implemented surface includes explicit GUID-backed configuration; deterministic group and scene synchronization; duplicate-dependency analysis/Fix; recoverable Full, Content Update, Editor-Compatible, and Multi-Platform builds; a curated sample; clean-install/removal checks; and release-archive validation. Preview hashes, stale-plan rejection, recovery snapshots, rollback, UI, public editor API, and CLI paths share the same safety boundaries. Package import, configuration reads, and unrelated asset imports are inert when setup is absent or invalid.

## Development

Requirements:

- Unity `6000.0.78f1`
- Addressables `2.7.6` for the minimum lane
- Addressables `2.9.1` for the compatibility lane
- Windows PowerShell 5.1 or PowerShell 7 for the repository validation scripts

Run static validation from the repository root:

```powershell
pwsh ./Tools/CI/Validate-PhaseZero.ps1
pwsh ./Tools/CI/Validate-PackageManifest.ps1 -PackagePath ./com.torproduction.addressables
```

Run a clean-install, EditMode, inert-import, and removal lane:

```powershell
pwsh ./Tools/CI/Test-CleanInstall.ps1 `
  -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.0.78f1\Editor\Unity.exe' `
  -PackagePath ./com.torproduction.addressables `
  -AddressablesVersion 2.7.6 `
  -ArtifactsPath ./artifacts/clean-install-2.7.6 `
  -ExcludeSamples
```

Repeat with `2.9.1`. The paid Unity compatibility workflow is intentionally manual and does not run for ordinary branch pushes or pull requests.

Phase 7 provides `New-PackageArchive.ps1`, archive-aware clean installation, selected PlayMode validation, disposable Package Validation Suite execution, and a protected tag-only GitHub pre-release workflow. Generated archives, reports, projects, and caches stay under ignored `artifacts/` or the system temporary directory.

## Installation status

The authorized public preview candidate is `0.1.0-preview.1`. Until its signed release tag and GitHub pre-release have passed every recorded gate, use a local package reference. Once published, the supported Git URL is:

```text
https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#v0.1.0-preview.1
```

Do not treat phase-verification tags as public package releases.

## Safety and release status

- Import, settings reads, and unrelated asset imports must remain inert.
- Mutating commands require an explicit valid configuration and a confirmed plan.
- `ImplementationPlan.md` is the source of truth for phase status, known limitations, and verification evidence.
- The GitHub pre-release may be created only from the signed `v0.1.0-preview.1` tag after the exact candidate passes the manual compatibility run and protected-environment approval.

See `com.torproduction.addressables/Documentation~/com.torproduction.addressables.md` for the complete package documentation index and `PROVENANCE_AUDIT.md` for the approved template-notice decision.
