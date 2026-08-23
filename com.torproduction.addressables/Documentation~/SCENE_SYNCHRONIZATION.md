# GUID-based scene synchronization

## Rules and identity

Each ordered scene rule identifies a source folder by Unity GUID, optional excluded nested-folder GUIDs, and one mode:

- **Addressable** places each discovered `.unity` asset in the configured Addressables group. A relative-path policy regenerates the address from the rule folder and optional prefix; preserve-managed-address keeps the last package-managed address across rename or move.
- **Local Build Settings** keeps each discovered scene out of package-managed Addressables state and places it in Build Settings after unrelated scenes, ordered by rule then normalized path.

Optional string categories and labels are generic metadata. The package does not create game-specific numeric state mappings. Scene filenames are never identity: the planner uses the Unity asset GUID, so duplicate names in different folders remain independent.

The configuration schema stores package-managed scene records containing GUID, last-known path, mode, address, destination group, and managed labels. These records let a full analysis reconcile deletes without loading a deleted `SceneAsset`, distinguish managed state from unrelated project state, and preserve addresses across path changes.

## Analyze, Apply, and transitions

**Analyze Scenes (No Changes)** reads the configured folders, Addressables entries, Build Settings, and managed records, then produces a sorted immutable plan. It does not mutate settings, assets, or the configuration. Missing settings, invalid or overlapping rules, unreadable scenes, incompatible claims, read-only/non-buildable groups, unrelated Addressables conflicts, and address collisions block Apply.

**Apply Scene Preview** confirms that source and plan hashes still match. It writes a recovery snapshot before mutation, then applies Addressables group/schema/entry/address/label operations, one deterministic Build Settings replacement, and one managed-record update. The configuration and Addressables settings are dirtied and saved at the transaction boundary. A successful Apply converges add, rename, move, delete, and Addressable/local folder transitions in one pass; the next analysis is empty.

Unrelated Addressables entries are not removed. Unrelated Build Settings rows retain their enabled state and relative order. Once a scene is explicitly claimed by a rule, that scene's package-managed placement may change to satisfy its configured mode.

## Automatic processing

Automatic scene processing is disabled by default and must be explicitly enabled in Project Settings after a valid scene configuration exists. The asset postprocessor first checks changed path suffixes case-insensitively and returns immediately if no `.unity` path is present. Relevant events are coalesced through `EditorApplication.delayCall`; the callback resolves the opt-in configuration and invokes the same public Analyze and Apply services used by the manual UI. A re-entry guard suppresses import callbacks caused by the transaction itself.

## Recovery, API, and CLI

Scene Apply snapshots are stored under `Library/TorProduction.Addressables/Recovery/scene-sync-<operation-id>.json`. They include affected Addressables entries/groups/labels, the complete pre-apply Build Settings list, and the configuration JSON. Failed rollback or snapshot cleanup retains recovery evidence and blocks another Apply. Use **Recover Previous Apply** or `AddressablesAutomationCli.RecoverScenes`.

Editor API:

```csharp
AutomationPlan plan = AddressablesAutomation.Analyze(config, AutomationScope.Scenes);
if (plan.IsValid && plan.HasChanges) {
    AutomationReport report = AddressablesAutomation.Apply(plan);
}
```

Batch-mode entry points are `AddressablesAutomationCli.AnalyzeScenes`, `AddressablesAutomationCli.ApplyScenes`, and `AddressablesAutomationCli.RecoverScenes`. They emit JSON to the Unity log and throw for blocking diagnostics or failed Apply/recovery so the process can fail visibly.
