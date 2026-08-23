using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TorProduction.Addressables.Editor {
	internal static class UnityGroupSyncDataSource {
		internal static GroupSyncProjectState Capture(
			AddressablesAutomationConfig config,
			IEnumerable<AutomationDiagnostic> initialDiagnostics = null) {
			var state = new GroupSyncProjectState();
			if (initialDiagnostics != null) {
				state.Diagnostics.AddRange(initialDiagnostics);
			}
			if (config == null) {
				return state;
			}

			var configPath = (AssetDatabase.GetAssetPath(config) ?? string.Empty).Replace('\\', '/');
			state.ConfigGuid = string.IsNullOrEmpty(configPath)
				? string.Empty
				: AssetDatabase.AssetPathToGUID(configPath);
			state.ConfigJson = EditorJsonUtility.ToJson(config, false);
			state.SettingsExist = AddressableAssetSettingsDefaultObject.SettingsExists;
			if (!state.SettingsExist) {
				return state;
			}

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			if (settings == null) {
				state.SettingsExist = false;
				return state;
			}

			var settingsPath = (AssetDatabase.GetAssetPath(settings) ?? string.Empty).Replace('\\', '/');
			state.SettingsIdentity = string.IsNullOrEmpty(settingsPath)
				? settings.GetInstanceID().ToString()
				: AssetDatabase.AssetPathToGUID(settingsPath);
			foreach (var label in settings.GetLabels()) {
				if (label != null) {
					state.Labels.Add(label);
				}
			}

			foreach (var group in settings.groups.Where(item => item != null)) {
				var bundled = group.GetSchema<BundledAssetGroupSchema>();
				state.Groups.Add(new GroupSyncGroupState {
					Guid = group.Guid ?? string.Empty,
					Name = group.Name ?? string.Empty,
					ReadOnly = group.ReadOnly,
					HasBundledSchema = bundled != null,
					HasContentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>() != null,
					IsBuildable = bundled == null || HasBuildAndLoadPaths(settings, bundled)
				});

				foreach (var entry in group.entries.Where(item => item != null)) {
					var path = (entry.AssetPath ?? string.Empty).Replace('\\', '/');
					state.Entries.Add(new GroupSyncEntryState {
						Guid = entry.guid ?? string.Empty,
						Path = path,
						GroupGuid = group.Guid ?? string.Empty,
						GroupName = group.Name ?? string.Empty,
						Address = entry.address ?? string.Empty,
						IsFolder = !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path),
						Labels = entry.labels?.Where(label => label != null).ToArray() ?? Array.Empty<string>()
					});
				}
			}

			var rules = config.SerializedGroupRules ?? Array.Empty<GroupSyncRule>();
			for (var index = 0; index < rules.Length; index++) {
				var rule = rules[index];
				if (rule == null) {
					continue;
				}
				var sourcePath = Normalize(AssetDatabase.GUIDToAssetPath(rule.SourceFolderGuid));
				var ruleState = new GroupSyncRuleState {
					Index = index,
					SourceFolderPath = sourcePath,
					DestinationGroupGuid = rule.DestinationGroupGuid ?? string.Empty,
					DestinationGroupName = rule.DestinationGroupName ?? string.Empty,
					AddressPrefix = rule.AddressPrefix ?? string.Empty,
					AddressPolicy = rule.AddressPolicy,
					LabelPolicy = rule.LabelPolicy,
					RequiredLabels = rule.RequiredLabels.Where(label => label != null).ToArray(),
					TypeFilterNames = rule.AssemblyQualifiedTypeFilters.Where(name => name != null).ToArray()
				};
				ruleState.ResolvedTypes = ResolveTypes(ruleState, state.Diagnostics);
				if (!string.IsNullOrEmpty(sourcePath) && AssetDatabase.IsValidFolder(sourcePath)) {
					CaptureAssets(rule, ruleState);
				}
				state.Rules.Add(ruleState);
			}

			return state;
		}

		private static Type[] ResolveTypes(
			GroupSyncRuleState rule,
			ICollection<AutomationDiagnostic> diagnostics) {
			var types = new List<Type>();
			for (var index = 0; index < rule.TypeFilterNames.Length; index++) {
				var name = rule.TypeFilterNames[index];
				try {
					var type = Type.GetType(name, false);
					if (type == null) {
						diagnostics.Add(new AutomationDiagnostic(
							AutomationDiagnosticCode.TypeFilterUnresolved,
							AutomationDiagnosticSeverity.Error,
							$"Groups[{rule.Index}].Types[{index}]",
							$"Type filter '{name}' could not be resolved."));
					} else {
						types.Add(type);
					}
				} catch (Exception exception) {
					diagnostics.Add(new AutomationDiagnostic(
						AutomationDiagnosticCode.TypeFilterUnresolved,
						AutomationDiagnosticSeverity.Error,
						$"Groups[{rule.Index}].Types[{index}]",
						$"Type filter '{name}' could not be resolved: {exception.Message}"));
				}
			}
			return types.ToArray();
		}

		private static void CaptureAssets(GroupSyncRule rule, GroupSyncRuleState state) {
			var exclusions = new List<string>();
			foreach (var guid in rule.ExcludedNestedFolderGuids) {
				var path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
				if (!string.IsNullOrEmpty(path)) {
					exclusions.Add(path.TrimEnd('/'));
				}
			}

			foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { state.SourceFolderPath })
			         .Distinct(StringComparer.Ordinal)
			         .OrderBy(item => item, StringComparer.Ordinal)) {
				var path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
				if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) ||
				    exclusions.Any(excluded => path.StartsWith(excluded + "/", StringComparison.Ordinal))) {
					continue;
				}

				var asset = new GroupSyncAssetState { Guid = guid, Path = path };
				try {
					Object loaded = AssetDatabase.LoadMainAssetAtPath(path);
					if (loaded == null) {
						asset.LoadError = "The main asset could not be loaded and was skipped.";
					} else {
						asset.AssetType = loaded.GetType();
					}
				} catch (Exception exception) {
					asset.LoadError = $"The main asset could not be loaded and was skipped: {exception.Message}";
				}
				state.Assets.Add(asset);
			}
		}

		internal static bool HasBuildAndLoadPaths(
			AddressableAssetSettings settings,
			BundledAssetGroupSchema schema) {
			try {
				var buildPath = schema.BuildPath?.GetValue(settings);
				var loadPath = schema.LoadPath?.GetValue(settings);
				return !string.IsNullOrWhiteSpace(buildPath) &&
				       !string.IsNullOrWhiteSpace(loadPath) &&
				       !string.Equals(buildPath, AddressableAssetProfileSettings.undefinedEntryValue, StringComparison.Ordinal) &&
				       !string.Equals(loadPath, AddressableAssetProfileSettings.undefinedEntryValue, StringComparison.Ordinal);
			} catch (Exception) {
				return false;
			}
		}

		private static string Normalize(string path) {
			return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
		}
	}
}
