# Tor Production Addressables

This repository develops `com.torproduction.addressables`, an editor-first Unity Package Manager package for safe, previewable Addressables automation. `AddressablesProject/` is the pinned Unity 6 integration-test host; the package itself is under `com.torproduction.addressables/`.

The implemented surface includes explicit GUID-backed configuration plus deterministic group and scene synchronization with dry-run plans, stale-plan rejection, recovery snapshots, rollback, UI, and CLI entry points. Scene reconciliation manages Addressables and local Build Settings by scene GUID while preserving unrelated state. Package import, configuration reads, and unrelated asset imports are inert when setup is absent or invalid. Dependency analysis, build workflows, final package-layout cleanup, and release automation remain unavailable until their owning implementation phases are complete.

## Development

Requirements:

- Unity `6000.0.78f1`
- Addressables `2.7.6` for the minimum lane
- Addressables `2.9.1` for the compatibility lane
- PowerShell 7 for the repository validation scripts

Run static validation from the repository root:

```powershell
pwsh ./Tools/CI/Validate-PhaseZero.ps1
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

## Installation status

No version release is currently authorized. For development, use a local package reference. A future verified phase tag can be installed from:

```text
https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#<verified-tag>
```

Do not treat phase-verification tags as public package releases.

## Safety and release status

- Import, settings reads, and unrelated asset imports must remain inert.
- Mutating commands require an explicit valid configuration and a confirmed plan.
- `ImplementationPlan.md` is the source of truth for phase status, known limitations, verification evidence, and legal/provenance blockers.
- Package publication, a GitHub Release, and version tags remain disabled pending later implementation and explicit legal/release authorization.

See `com.torproduction.addressables/Documentation~/SAFETY.md`, `GROUP_SYNCHRONIZATION.md`, and `SCENE_SYNCHRONIZATION.md` for the implemented safety model.
