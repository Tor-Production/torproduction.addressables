# Tor Production Addressables

Editor tooling for project-agnostic, previewable Addressables automation on Unity 6.

## Current capabilities

- Explicit GUID-backed project configuration under Project Settings.
- Deterministic group analysis and convergence for group, address, and label state.
- GUID-based scene reconciliation for Addressables and local Build Settings, including rename, move, delete, and folder-mode transitions.
- Analyze-only duplicate-dependency reporting plus a separately confirmed, version-gated Fix that creates or validates the destination group and schemas without moving physical assets.
- Missing group/schema planning, collision checks, stale-plan rejection, transaction snapshots, rollback, and manual recovery.
- Project Settings, menu, public editor API, and CLI entry points that share the same planner.
- Explicit Full, Content Update, Editor-Compatible, and deterministic Multi-Platform content builds with exact-target preflight, domain-reload recovery, preserved reports, cancellation, and built-in existing-build Play Mode selection guarded by freshness receipts.
- Inert package import and configuration reads when setup is missing or invalid.

The curated Basic Setup sample, package-layout checks, archive dry run, and release-readiness validation are available. Publication remains disabled pending the ownership/licensing decisions and final authorized hosted verification. The former project-specific prefab/interactable migration tool was removed rather than generalized inside the package.

## Development installation

Add the local package folder through Unity Package Manager, or reference it from a development project manifest:

```json
{
  "dependencies": {
    "com.torproduction.addressables": "file:../../com.torproduction.addressables"
  }
}
```

The repository also supports Git dependencies with the package subfolder:

```text
https://github.com/Yurii-Tor/torproduction.addressables.git?path=/com.torproduction.addressables#<verified-tag>
```

There is no authorized public version release yet. Phase-verification tags record engineering evidence and are not package releases.

## Use

Open `Project Settings > Tor Production > Addressables Automation` to create or select a configuration and analyze Groups, Scenes, or duplicate Dependencies. Manual Apply/Fix is always explicit, rejects stale previews, and blocks when complete convergence cannot be proven. Automatic scene processing is separately opt-in and uses the same scene plan and transaction as the manual workflow.

For complete usage, start with `Documentation~/INSTALLATION.md`, `CONFIGURATION.md`, and `com.torproduction.addressables.md`. Detailed group, scene, dependency, build, CLI, sample, recovery, limitations, and troubleshooting material is indexed there.

## Compatibility

- Unity `6000.0.78f1` verified baseline
- Addressables `2.7.6` minimum lane
- Addressables `2.9.1` compatibility lane
- Unity `6000.0.82f1` / Addressables `2.11.2` prepared experimental lane (not yet verified)

The exact verification evidence is maintained in the repository `ImplementationPlan.md`.

## License and provenance

The existing license and attribution files are preserved. Public release remains blocked until ownership, redistribution rights, relicensing authority, and required attribution are confirmed by the owner. See `Documentation~/PROVENANCE_AUDIT.md` for verified facts and the exact unresolved questions.
