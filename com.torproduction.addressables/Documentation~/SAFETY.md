# Addressables Automation safety notice

This notice applies to `0.1.0-preview.1` while the production workflows are being rebuilt.

- Installing or importing the package does not create configuration files, Addressables settings, groups, labels, or Build Settings entries.
- Setup is explicit under **Project Settings > Tor Production > Addressables Automation**. Opening the provider and reading configuration do not create or repair files.
- A new `AddressablesAutomationConfig` is created only after **Create** confirmation, under an `Editor` folder. **Select** accepts only a persistent main asset and stores only its Unity asset GUID in `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset`.
- Automatic scene processing is off by default. Saving its opt-in requires a valid selected config and existing Addressables settings; scene reconciliation itself remains disabled until Phase 3.
- Deterministic group synchronization is enabled only for a valid active configuration. **Analyze Groups** is read-only. **Apply Group Preview** requires confirmation, rejects stale plans, and changes only assets claimed by explicit group rules. Update All, scene synchronization, dependency fixing, prefab relocation, and Addressables build commands remain disabled.
- Addressables settings are never created by configuration reads. The provider can open Unity's official Addressables Groups window after confirmation, but this package does not create settings there.
- The old project-settings window no longer reads or saves `ProjectSettings/ProjectConfig.json`; retained instances only redirect to the provider.
- **Preview Legacy Migration** is read-only. It inspects each legacy GUID independently, reports unmapped numeric app-state data, and never deletes or rewrites legacy JSON/assets. **Create Migrated Configuration** creates a separate new asset after a second confirmation; unresolved values remain blocking rather than being broadened or discarded.
- Explicit schema migration writes a recovery copy under `Library/TorProduction.Addressables/Recovery` before changing project state or a config asset. Corrupt/newer project state has a separate confirmed backup-and-reset action and is never repaired on read.
- Group Apply writes `Library/TorProduction.Addressables/Recovery/group-sync-<operation-id>.json` before its first mutation. The default policy stops and rolls back on any failure. An incomplete rollback retains that file, blocks later Apply operations, and exposes **Recover Previous Group Apply** plus the CLI recovery entry point.
- Group rules operate on explicit asset entries. They never clear the Default Group wholesale and never move source assets. Addressable folder entries that own a configured descendant are blocking conflicts.
- **Detach** clears the selected GUID and automatic opt-in. It does not delete configuration assets, legacy data, Addressables settings, groups, labels, or Build Settings entries. Detach before removing the package if the host project no longer wants its tracked package settings.
- The package is not release-ready. Publishing workflows remain disabled until the later release-readiness phase and the legal checks recorded in `ImplementationPlan.md` are complete.

Use `AddressablesAutomation.Analyze(config, AutomationScope.Groups)` and apply only the returned reviewed plan with `AddressablesAutomation.Apply(plan)`. The legacy ad-hoc group controller and timestamped report are compiled out.
