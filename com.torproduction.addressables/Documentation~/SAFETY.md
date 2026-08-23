# Addressables Automation safety notice

This notice applies to `0.1.0-preview.1` while the production workflows are being rebuilt.

- Installing or importing the package does not create configuration files, Addressables settings, groups, labels, or Build Settings entries.
- Setup is explicit under **Project Settings > Tor Production > Addressables Automation**. Opening the provider and reading configuration do not create or repair files.
- A new `AddressablesAutomationConfig` is created only after **Create** confirmation, under an `Editor` folder. **Select** accepts only a persistent main asset and stores only its Unity asset GUID in `ProjectSettings/TorProduction/AddressablesAutomationProjectSettings.asset`.
- Automatic scene processing is off by default. Saving its opt-in requires a valid selected config and existing Addressables settings. Relevant `.unity` changes are deferred and coalesced; unrelated imports return before configuration lookup.
- Deterministic group and scene synchronization are enabled only for a valid active configuration. Analyze is read-only. Apply requires confirmation, rejects stale plans, and changes only identities claimed by explicit rules or prior managed-scene records. Update All, dependency fixing, prefab relocation, and Addressables build commands remain disabled.
- Addressables settings are never created by configuration reads. The provider can open Unity's official Addressables Groups window after confirmation, but this package does not create settings there.
- The old project-settings window no longer reads or saves `ProjectSettings/ProjectConfig.json`; retained instances only redirect to the provider.
- **Preview Legacy Migration** is read-only. It inspects each legacy GUID independently, reports unmapped numeric app-state data, and never deletes or rewrites legacy JSON/assets. **Create Migrated Configuration** creates a separate new asset after a second confirmation; unresolved values remain blocking rather than being broadened or discarded.
- Explicit schema migration writes a recovery copy under `Library/TorProduction.Addressables/Recovery` before changing project state or a config asset. Corrupt/newer project state has a separate confirmed backup-and-reset action and is never repaired on read.
- Apply writes `Library/TorProduction.Addressables/Recovery/<scope>-sync-<operation-id>.json` before its first mutation. Scene snapshots include affected Addressables state, Build Settings, and the configuration ownership records. The default policy stops and rolls back on any failure. An incomplete rollback retains that file, blocks later Apply operations, and exposes **Recover Previous Apply** plus CLI recovery entry points.
- Group rules operate on explicit asset entries. They never clear the Default Group wholesale and never move source assets. Addressable folder entries that own a configured descendant are blocking conflicts.
- **Detach** clears the selected GUID and automatic opt-in. It does not delete configuration assets, legacy data, Addressables settings, groups, labels, or Build Settings entries. Detach before removing the package if the host project no longer wants its tracked package settings.
- The package is not release-ready. Publishing workflows remain disabled until the later release-readiness phase and the legal checks recorded in `ImplementationPlan.md` are complete.

Use `AddressablesAutomation.Analyze(config, AutomationScope.Groups)` or `AutomationScope.Scenes` and apply only the returned reviewed plan with `AddressablesAutomation.Apply(plan)`. The legacy ad-hoc group and scene mutation paths are retired.
