using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace TorProduction.Addressables.Editor {
	internal static class UnitySceneSyncDataSource {
		internal static SceneSyncProjectState Capture(
			AddressablesAutomationConfig config,
			IEnumerable<AutomationDiagnostic> initialDiagnostics = null) {
			var state = new SceneSyncProjectState();
			if (initialDiagnostics != null) state.Diagnostics.AddRange(initialDiagnostics);
			if (config == null) return state;

			var configPath = Normalize(AssetDatabase.GetAssetPath(config));
			state.ConfigGuid = string.IsNullOrEmpty(configPath) ? string.Empty : AssetDatabase.AssetPathToGUID(configPath);
			state.ConfigJson = EditorJsonUtility.ToJson(config, false);
			state.ManagedScenes.AddRange(config.SerializedManagedScenes ?? Array.Empty<ManagedSceneRecord>());
			state.SettingsExist = AddressableAssetSettingsDefaultObject.SettingsExists;
			if (!state.SettingsExist) return state;
			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			if (settings == null) { state.SettingsExist = false; return state; }

			var settingsPath = Normalize(AssetDatabase.GetAssetPath(settings));
			state.SettingsIdentity = string.IsNullOrEmpty(settingsPath) ? settings.GetInstanceID().ToString() : AssetDatabase.AssetPathToGUID(settingsPath);
			foreach (var label in settings.GetLabels()) if (label != null) state.Labels.Add(label);
			foreach (var group in settings.groups.Where(item => item != null)) {
				var bundled = group.GetSchema<BundledAssetGroupSchema>();
				state.Groups.Add(new SceneSyncGroupState {
					Guid = group.Guid ?? string.Empty,
					Name = group.Name ?? string.Empty,
					ReadOnly = group.ReadOnly,
					HasBundledSchema = bundled != null,
					HasContentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>() != null,
					IsBuildable = bundled == null || UnityGroupSyncDataSource.HasBuildAndLoadPaths(settings, bundled)
				});
				foreach (var entry in group.entries.Where(item => item != null)) state.Entries.Add(new SceneSyncEntryState {
					Guid = entry.guid ?? string.Empty,
					Path = Normalize(entry.AssetPath),
					GroupGuid = group.Guid ?? string.Empty,
					GroupName = group.Name ?? string.Empty,
					Address = entry.address ?? string.Empty,
					Labels = entry.labels?.Where(label => label != null).ToArray() ?? Array.Empty<string>()
				});
			}

			foreach (var scene in EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>()) {
				var path = Normalize(scene.path);
				state.BuildScenes.Add(new SceneBuildState {
					Guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path),
					Path = path,
					Enabled = scene.enabled
				});
			}

			var rules = config.SerializedSceneRules ?? Array.Empty<SceneFolderRule>();
			for (var index = 0; index < rules.Length; index++) {
				var rule = rules[index];
				if (rule == null) continue;
				var source = Normalize(AssetDatabase.GUIDToAssetPath(rule.SourceFolderGuid));
				var ruleState = new SceneSyncRuleState {
					Index = index,
					SourceFolderPath = source,
					Mode = rule.Mode,
					DestinationGroupGuid = rule.DestinationGroupGuid ?? string.Empty,
					DestinationGroupName = rule.DestinationGroupName ?? string.Empty,
					Category = rule.Category ?? string.Empty,
					AddressPrefix = rule.AddressPrefix ?? string.Empty,
					AddressPolicy = rule.AddressPolicy,
					RequiredLabels = rule.RequiredLabels.Where(label => label != null).ToArray()
				};
				if (!string.IsNullOrEmpty(source) && AssetDatabase.IsValidFolder(source)) CaptureScenes(rule, ruleState, state.Diagnostics);
				state.Rules.Add(ruleState);
			}
			return state;
		}

		private static void CaptureScenes(SceneFolderRule rule, SceneSyncRuleState state, ICollection<AutomationDiagnostic> diagnostics) {
			var exclusions = rule.ExcludedNestedFolderGuids.Select(AssetDatabase.GUIDToAssetPath).Select(Normalize).Where(item => !string.IsNullOrEmpty(item)).Select(item => item.TrimEnd('/')).ToArray();
			foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { state.SourceFolderPath }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)) {
				var path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
				if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || exclusions.Any(item => path.StartsWith(item + "/", StringComparison.Ordinal))) continue;
				try {
					if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) {
						diagnostics.Add(new AutomationDiagnostic(AutomationDiagnosticCode.SceneLoadFailed, AutomationDiagnosticSeverity.Error, path, "The scene could not be loaded. Apply is blocked because complete reconciliation cannot be proven."));
						continue;
					}
				} catch (Exception exception) {
					diagnostics.Add(new AutomationDiagnostic(AutomationDiagnosticCode.SceneLoadFailed, AutomationDiagnosticSeverity.Error, path, $"The scene could not be loaded: {exception.Message}"));
					continue;
				}
				state.Scenes.Add(new SceneAssetState { Guid = guid, Path = path });
			}
		}

		private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
	}
}
