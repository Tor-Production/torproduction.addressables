# Duplicate dependency analysis

Configure the destination group under `Dependency Settings` on the active `AddressablesAutomationConfig`, then open **Project Settings > Tor Production > Addressables Automation**. **Analyze Duplicate Dependencies (No Changes)** is the default and does not create Addressables settings, groups, schemas, or entries.

The compatibility adapter subclasses Addressables' built-in `CheckBundleDupeDependencies`, calls its public `RefreshAnalysis` lifecycle, and reads only the documented protected `CheckDupeResults`. No private field, method, or reflection access is used. Fix capability is enabled only for the exact tested Addressables `2.7.6` and `2.9.1` versions. Other versions show an actionable blocking diagnostic and keep Fix disabled until a dedicated adapter is implemented and verified.

The report distinguishes:

- implicit duplicated dependencies, which are proposed as explicit entries in the configured destination group; and
- already-explicit Addressable entries, which are informational and are never moved by this workflow.

If implicit candidates exist, the plan creates the destination group only when missing, adds `BundledAssetGroupSchema` and `ContentUpdateGroupSchema` only when missing, and then creates the reviewed entries. A read-only group or invalid bundled paths blocks Fix. Analyze is immutable and a second analysis after a successful Fix is empty.

**Fix Analyzed Duplicate Dependencies** is a separate action. It requires a confirmation dialog, reruns analysis to reject a stale preview, writes a recovery snapshot under `Library/TorProduction.Addressables/Recovery`, and uses the shared stop-and-rollback transaction. It never calls `AssetDatabase.MoveAsset` and never changes a physical asset path.

The former prefab/interactable organizer and its migration configuration were removed. Existing host prefabs are not changed during upgrade. If a consumer still requires that project-specific migration, preserve a private copy outside this package before upgrading and review its collision, destination, and recovery behavior independently.
