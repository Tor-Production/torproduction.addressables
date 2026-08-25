# Configuration

## Select a configuration

Open **Project Settings > Tor Production > Addressables Automation**. The provider stores only the Unity GUID of a persistent, main-asset `AddressablesAutomationConfig` in package-owned project settings. The configuration must live under an `Editor` folder so it cannot enter a player build.

- **Create** is the only configuration-asset creation path and requires confirmation.
- **Select** validates the chosen asset before writing the GUID.
- **Detach** clears the selected GUID and automatic scene opt-in; it does not delete project assets or Addressables state.
- Reading or opening the provider does not create/repair settings, assets, groups, labels, entries, scenes, or Build Settings rows.

## Rules

Group and scene rules identify source folders and destinations by Unity GUID. Display names are retained as diagnostics/fallbacks, not as identity. Use assembly-qualified names for type filters. An empty group type filter includes every loadable, non-folder main asset under that rule.

Choose relative-path addresses when addresses should converge from folder-relative paths, or the relevant preserve policy when existing/package-managed addresses must survive moves. Required labels can preserve unrelated labels or enforce the exact configured set. Rule overlap, duplicate final addresses, unreadable assets, invalid schemas, and incompatible ownership claims block Apply.

Dependency settings identify the destination group for explicit duplicate dependencies. Build operations do not require additional serialized configuration but do require existing valid Addressables settings and exact target prerequisites.

## Preview and Apply

Analyze is read-only and returns a deterministic plan with source and plan hashes. Review all operations and blocking diagnostics. Apply/Fix is a separate confirmed action and reruns current-state validation. If configuration, source assets, groups, entries, labels, addresses, scenes, Build Settings, or analyzer output changed after preview, the stale plan is rejected.

Apply changes only identities claimed by configured rules, prior package-managed scene records, or the reviewed duplicate-dependency result. It never clears the Default Group wholesale and never moves physical assets. A successful Apply should be followed by a second Analyze that reports no operations.

## Automatic scene processing

Automatic scene synchronization is disabled by default. Enable it only after selecting a valid configuration with scene rules and existing Addressables settings. Relevant `.unity` imports are coalesced and use the same plan and transaction as manual Apply; unrelated imports return before configuration lookup.

## Migration and recovery

Legacy migration is explicit and backup-first. Preview reads recoverable fields independently without rewriting the legacy JSON/assets. Creating a migrated configuration creates a separate asset; ambiguous types and unmapped numeric application-state values remain blocking.

Before a mutation, the package writes a scoped recovery snapshot under `Library/TorProduction.Addressables`. Incomplete rollback blocks new Apply operations. Use **Recover Previous Apply** for group/scene/dependency transactions. Build jobs have separate Resume, Cancel, Restore Original Target, and Abandon/Reset actions. See [Safety](SAFETY.md) and [Troubleshooting](TROUBLESHOOTING.md).
