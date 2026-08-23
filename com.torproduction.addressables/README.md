# Tor Production Addressables

Editor tooling for project-agnostic, previewable Addressables automation on Unity 6.

## Current capabilities

- Explicit GUID-backed project configuration under Project Settings.
- Deterministic group analysis and convergence for group, address, and label state.
- GUID-based scene reconciliation for Addressables and local Build Settings, including rename, move, delete, and folder-mode transitions.
- Missing group/schema planning, collision checks, stale-plan rejection, transaction snapshots, rollback, and manual recovery.
- Project Settings, menu, public editor API, and CLI entry points that share the same planner.
- Inert package import and configuration reads when setup is missing or invalid.

Dependency fixing, content builds, final sample/layout cleanup, and release automation are not yet available.

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

Open `Project Settings > Tor Production > Addressables Automation` to create or select a configuration and analyze Groups or Scenes. Manual Apply is always explicit, rejects stale previews, and blocks when complete convergence cannot be proven. Automatic scene processing is separately opt-in and uses the same scene plan and transaction as the manual workflow.

For current limitations and recovery behavior, see `Documentation~/SAFETY.md`, `Documentation~/GROUP_SYNCHRONIZATION.md`, and `Documentation~/SCENE_SYNCHRONIZATION.md`.

## Compatibility

- Unity `6000.0.78f1`
- Addressables `2.7.6` minimum lane
- Addressables `2.9.1` compatibility lane

The exact verification evidence is maintained in the repository `ImplementationPlan.md`.

## License and provenance

The existing license and attribution files are preserved. Public release remains blocked until ownership, redistribution rights, relicensing authority, and required attribution are confirmed by the owner.
