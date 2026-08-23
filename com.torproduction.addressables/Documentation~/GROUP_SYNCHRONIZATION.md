# Deterministic group synchronization

## Configure rules

Create or select an editor-only `AddressablesAutomationConfig` under **Project Settings > Tor Production > Addressables Automation**. Each group rule stores:

- a source-folder Unity GUID and optional explicitly excluded nested-folder GUIDs;
- a destination Addressables group GUID plus its fallback display name;
- an optional relative address prefix;
- a relative-path or preserve-existing address policy;
- required Addressables labels and either preserve-unrelated or exact label behavior;
- zero or more assembly-qualified type names. An empty filter includes every loadable, non-folder main asset.

Relative addresses are the source-relative asset path with forward slashes and only the final extension removed. For example, `Assets/Game/Items/UI/Icon.prefab` under `Assets/Game/Items` becomes `UI/Icon`; prefix `catalog` produces `catalog/UI/Icon`. Duplicate filenames in different subfolders therefore remain distinct.

The explicit legacy-migration preview reads the misspelled serialized `Lables` field and converts a simple type name only when exactly one loaded project type matches. Missing or ambiguous type names remain visible blocking diagnostics; legacy assets and JSON are not rewritten.

## Analyze and Apply

**Analyze Groups (No Changes)** scans configured folders, resolves filters, compares existing Addressables entries, and returns a sorted immutable plan. It does not create Addressables settings, groups, schemas, labels, or entries and does not dirty assets.

Preflight blocks Apply for missing settings or folders, unresolved types, any failed main-asset load, incompatible rule claims, duplicate final addresses, read-only or invalid bundled groups, and explicit Addressable folder entries that implicitly own a claimed descendant. A failed load is never warning-only: skipping an unreadable candidate could leave stale managed state, so complete convergence cannot be proven. Missing destination groups, `BundledAssetGroupSchema`, `ContentUpdateGroupSchema`, and required labels are proposed as operations rather than created during analysis.

**Apply Group Preview** requires confirmation and verifies the source-state and plan hashes. Any config, source-asset, group, entry, address, or label change after preview makes the plan stale and requires another analysis. Apply creates or validates groups and schemas first, then converges each explicit entry's group, address, and labels. It never clears the Default Group and never moves user assets. A second analysis after a successful Apply should contain no operations.

## Failure and recovery

Before its first mutation, Apply records affected entries, groups, schemas, and created labels under `Library/TorProduction.Addressables/Recovery/group-sync-<operation-id>.json`. The default policy stops on the first failure and restores through public Addressables APIs. A successful Apply or rollback removes the snapshot.

If rollback is incomplete, throws unexpectedly, or cannot clean up its snapshot, an atomically written snapshot remains and further Apply operations are blocked. Use **Recover Previous Group Apply** in Project Settings or the group synchronization window. Recovery touches only identities recorded by the package snapshot; unrelated groups and entries are not cleared.

## API and CLI

Editor API:

```csharp
AutomationPlan plan = AddressablesAutomation.Analyze(config, AutomationScope.Groups);
if (plan.IsValid && plan.HasChanges) {
    AutomationReport report = AddressablesAutomation.Apply(plan);
}
```

Unity batch-mode dry run:

```text
Unity -batchmode -quit -projectPath <project> -executeMethod TorProduction.Addressables.Editor.AddressablesAutomationCli.AnalyzeGroups
```

Explicit CLI Apply and recovery use `AddressablesAutomationCli.ApplyGroups` and `AddressablesAutomationCli.RecoverGroups`. Each emits deterministic JSON to the Unity log and throws on blocking diagnostics or failure so batch mode returns a failing result.
