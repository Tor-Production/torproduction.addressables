# Phase 6 breaking changes

This is the pre-1.0 package-layout/API migration record only. It does not change the package version, license, legal attribution, release process, or publication state. Removed runtime and project-specific APIs have no compatibility shims.

| Previous public API | Phase 6 disposition | Supported path |
| --- | --- | --- |
| Production assembly `TorProduction.AddressablesService.Editor` | Renamed to `TorProduction.Addressables.Editor` | Update explicit assembly references. Retained public types remain under `TorProduction.Addressables.Editor`; `AddressablesAutomationConfig` also carries Unity `MovedFrom` assembly metadata and a real serialized fixture verifies its data. |
| Production assembly `TorProduction.AddressablesToolpack.Editor.Menu` | Merged into `TorProduction.Addressables.Editor` | Menu/UI implementation is internal. Use `AddressablesAutomation`, `AddressablesBuildQueue`, or the public CLI entry points. |
| Production assembly `TorProduction.AddressablesToolpack` | Removed as project-specific runtime surface | None. The package is editor-only and contributes no player assembly. |
| `TorProduction.AddressablesToolpack.Data.ITemplate` | Removed as project-specific | None. |
| `TorProduction.AddressablesToolpack.Data.IObjectTemplate` | Removed as project-specific | None. |
| `TorProduction.AddressablesToolpack.Data.ObjectTemplate` | Removed as project-specific | None. Use host-owned data models. |
| `TorProduction.AddressablesToolpack.InteractableFactoryId` | Removed as project-specific | None. Use host-owned identifiers. |
| `TorProduction.AddressablesToolpack.SerializableDictionary<TKey,TValue>` | Removed as dead/template code | None. No retained production workflow required it. |
| `TorProduction.AddressablesToolpack.Common.SceneField` | Removed as unrelated utility | None. Use a host-owned scene reference if needed. |
| `TorProduction.AddressablesToolpack.Common.SceneFieldPropertyDrawer` | Removed as unrelated utility | None. |
| `TorProduction.AddressablesToolpack.Common.ReadOnlyAttribute` | Removed as unrelated utility | None. |
| `TorProduction.AddressablesToolpack.Common.ReadOnlyDrawer` | Removed as unrelated utility | None. |
| `StansAssets.PackageSample.MyPublicRuntimeExampleClass` | Removed as dead/template code | None. |
| `StansAssets.PackageSample.Editor.MyPublicEditorExampleClass` | Removed as dead/template code | None. |
| `TorProduction.AddressablesToolpack.Editor.AssetTypes` | Removed as dead/template code | Type filters are stored as assembly-qualified names on `GroupSyncRule` and resolved by the supported planner/validator. |
| `TorProduction.AddressablesToolpack.Editor.GroupNames` | Removed as dead/template code | Group identity is configured on `GroupSyncRule`, `SceneFolderRule`, and `DependencyAnalysisSettings`. |
| `TorProduction.AddressablesToolpack.Editor.ProjectAssetUtil` | Removed as unrelated mutable utility | Use `AddressablesAutomation.Analyze` and `AddressablesAutomation.Apply`. |
| `TorProduction.AddressablesToolpack.Editor.Menu.AddressableMenuUtils` | Removed as dead/template code | No replacement; UI implementation is internal. |
| `TorProduction.AddressablesToolpack.Editor.Menu.AddressableAssetsConfig` | Replaced by named editor API | Use `AddressablesAutomationConfig.GroupRules`. Explicit legacy preview still recognizes its historical script GUID/fields without retaining the type. |
| `TorProduction.AddressablesToolpack.Editor.Menu.UpdateGroupSettings` | Replaced by named editor API | Use `GroupSyncRule`. |
| `TorProduction.AddressablesToolpack.Editor.Menu.UpdateGroupSettingsDrawer` | Removed as dead/template code | The current SettingsProvider edits supported configuration. |
| `TorProduction.AddressablesToolpack.Editor.Menu.ScenesListConfig` | Replaced by named editor API | Use `AddressablesAutomationConfig.SceneRules`. Explicit legacy preview still recognizes its historical script GUID/fields without retaining the type. |
| `TorProduction.AddressablesToolpack.Editor.Menu.ScenesListMapper` | Removed from the public surface; retained only as internal postprocessor plumbing | Use `AddressablesAutomation.Analyze`/`Apply` for scenes and the explicit automatic-scene opt-in. |
| `TorProduction.AddressablesToolpack.Editor.Menu.ProjectSettingsWindow` | Replaced by named editor API | Use `AddressablesAutomationConfig`, `AddressablesAutomationValidator`, and **Project Settings > Tor Production > Addressables Automation**. |

The complete retained public type/member and allowed-reference surface is the deterministic `Documentation~/API_SURFACE.txt` snapshot enforced by EditMode tests.
