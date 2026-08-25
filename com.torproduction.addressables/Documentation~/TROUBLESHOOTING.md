# Troubleshooting

## The provider says no configuration is selected

Installation does not create one. Open **Project Settings > Tor Production > Addressables Automation** and explicitly create or select a persistent `AddressablesAutomationConfig` under an `Editor` folder. If the recorded GUID no longer resolves, select the intended asset again; do not create a replacement merely by opening settings.

## Addressables settings are missing

Analyze will fail closed and will not create settings. Use the provider's confirmed link to Unity's official Addressables Groups window, then create settings there intentionally.

## Apply or Fix is disabled

Review blocking diagnostics. Common causes are unresolved folder/group GUIDs, ambiguous type names, unreadable assets, rule overlap, duplicate final addresses, Addressable folder entries, read-only/non-buildable groups, missing incompatible schemas, stale preview hashes, unsupported duplicate-fix versions, or pending recovery.

Re-run Analyze after any project change. Do not bypass a stale-plan diagnostic.

## Recovery is required

Do not delete files under `Library/TorProduction.Addressables/Recovery` while deciding what to recover. Use **Recover Previous Apply** (or the matching group/scene CLI recovery command). Recovery touches only identities recorded in the snapshot. If it cannot complete, preserve the snapshot and logs for inspection.

For a build job, open the build window and inspect the recorded stage. Choose Resume only when the exact request is still intended; otherwise Cancel, Restore Original Target, or Abandon/Reset. Abandon archives package-owned evidence but does not repair external changes.

## Content Update preflight fails

Select the exact prior `addressables_content_state.bin` and confirm the same catalog/group configuration still applies. A missing, unreadable, mismatched, or restricted-update state file is blocking. Run a Full build when there is no compatible prior state.

## Existing Build cannot be selected

Run an Editor-Compatible build for the current host. Validation rejects receipts from another target, Unity/Addressables version, settings/configuration identity, output path, or stale/missing `settings.json`. After a valid receipt, selection still needs `-torConfirmExistingBuild true` on CLI or the confirmation dialog.

## Sample import is not active

This is expected. Select the imported config manually and analyze it. Sample import never creates settings or applies rules.

## Package installation/removal fails

Check the Unity editor log and `Packages/manifest.json`. Local directory/archive dependencies must remain reachable. Remove an imported sample separately. Before removing the package, resolve package-owned recovery records and use Detach if its selected GUID/automatic opt-in should be cleared.

When reporting a problem, include Unity and Addressables versions, the read-only plan/preflight JSON, the relevant package report/recovery path, and a minimal disposable-project reproduction. Do not attach proprietary project content.
