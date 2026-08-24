using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal enum LegacyMigrationDiagnosticCode {
		SettingsMissing,
		SettingsCorrupt,
		ReferenceFieldMissing,
		ReferenceMalformed,
		ReferencedAssetMissing,
		ReferencedAssetWrongType,
		ReferencedAssetSchemaMismatch,
		SourceFolderMissing,
		SourceFolderOutsideAssets,
		TypeFilterEmpty,
		TypeFilterUnresolved,
		TypeFilterAmbiguous,
		TypeLoaderWarning,
		AdditionalSceneModeRequired,
		SceneCatalogRetained,
		AppStatesIntentionallyIgnored,
		SectionFailed
	}

	internal sealed class LegacyMigrationDiagnostic {
		internal LegacyMigrationDiagnostic(
			LegacyMigrationDiagnosticCode code,
			ConfigurationDiagnosticSeverity severity,
			LegacyConfigurationKind kind,
			string location,
			string message,
			string legacyValue = "") {
			Code = code;
			Severity = severity;
			Kind = kind;
			Location = location ?? string.Empty;
			Message = message ?? string.Empty;
			LegacyValue = legacyValue ?? string.Empty;
		}

		internal LegacyMigrationDiagnosticCode Code { get; }
		internal ConfigurationDiagnosticSeverity Severity { get; }
		internal LegacyConfigurationKind Kind { get; }
		internal string Location { get; }
		internal string Message { get; }
		internal string LegacyValue { get; }
	}

	internal sealed class LegacyMigrationPreview {
		internal LegacyMigrationPreview(
			string scenesGuid,
			string addressablesGuid,
			string appStatesGuid,
			GroupSyncRule[] groupRules,
			SceneFolderRule[] sceneRules,
			IReadOnlyList<LegacyMigrationDiagnostic> diagnostics) {
			ScenesConfigGuid = scenesGuid ?? string.Empty;
			AddressableAssetsConfigGuid = addressablesGuid ?? string.Empty;
			AppStatesConfigGuid = appStatesGuid ?? string.Empty;
			GroupRules = groupRules ?? Array.Empty<GroupSyncRule>();
			SceneRules = sceneRules ?? Array.Empty<SceneFolderRule>();
			Diagnostics = diagnostics ?? Array.Empty<LegacyMigrationDiagnostic>();
		}

		internal string ScenesConfigGuid { get; }
		internal string AddressableAssetsConfigGuid { get; }
		internal string AppStatesConfigGuid { get; }
		internal GroupSyncRule[] GroupRules { get; }
		internal SceneFolderRule[] SceneRules { get; }
		internal IReadOnlyList<LegacyMigrationDiagnostic> Diagnostics { get; }
		internal bool HasLegacyState =>
			!string.IsNullOrEmpty(ScenesConfigGuid) ||
			!string.IsNullOrEmpty(AddressableAssetsConfigGuid) ||
			!string.IsNullOrEmpty(AppStatesConfigGuid) ||
			GroupRules.Length != 0 || SceneRules.Length != 0;
		internal bool HasBlockingErrors => Diagnostics.Any(item =>
			item.Severity == ConfigurationDiagnosticSeverity.Error);
	}

	internal sealed class LegacyTypeLookupResult {
		internal LegacyTypeLookupResult(IReadOnlyList<Type> matches, IReadOnlyList<string> loaderErrors) {
			Matches = matches ?? Array.Empty<Type>();
			LoaderErrors = loaderErrors ?? Array.Empty<string>();
		}

		internal IReadOnlyList<Type> Matches { get; }
		internal IReadOnlyList<string> LoaderErrors { get; }
	}

	internal interface ILegacyMigrationEnvironment {
		string GuidToAssetPath(string guid);
		UnityEngine.Object LoadMainAssetAtPath(string path);
		string GetAssetPath(UnityEngine.Object asset);
		string AssetPathToGuid(string path);
		bool IsValidFolder(string path);
		string GetMonoScriptGuid(ScriptableObject asset);
		bool TryGetGroupGuid(string groupName, out string groupGuid);
		LegacyTypeLookupResult FindTypes(string legacyName);
	}

	internal sealed class UnityLegacyMigrationEnvironment : ILegacyMigrationEnvironment {
		private readonly AddressablesSettingsView m_addressables = new AddressablesSettingsView();

		public string GuidToAssetPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);
		public UnityEngine.Object LoadMainAssetAtPath(string path) => AssetDatabase.LoadMainAssetAtPath(path);
		public string GetAssetPath(UnityEngine.Object asset) => AssetDatabase.GetAssetPath(asset);
		public string AssetPathToGuid(string path) => AssetDatabase.AssetPathToGUID(path);
		public bool IsValidFolder(string path) => AssetDatabase.IsValidFolder(path);

		public string GetMonoScriptGuid(ScriptableObject asset) {
			var script = MonoScript.FromScriptableObject(asset);
			return script == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
		}

		public bool TryGetGroupGuid(string groupName, out string groupGuid) {
			return m_addressables.TryGetGroupGuid(groupName, out groupGuid);
		}

		public LegacyTypeLookupResult FindTypes(string legacyName) {
			var matches = new List<Type>();
			var loaderErrors = new List<string>();
			if (string.IsNullOrWhiteSpace(legacyName)) {
				return new LegacyTypeLookupResult(matches, loaderErrors);
			}

			if (legacyName.IndexOf(',') >= 0) {
				try {
					var resolved = Type.GetType(legacyName, false);
					if (resolved != null) {
						matches.Add(resolved);
					}
				} catch (Exception exception) {
					loaderErrors.Add(exception.Message);
				}
				return new LegacyTypeLookupResult(matches, loaderErrors);
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if (assembly.IsDynamic) {
					continue;
				}

				IEnumerable<Type> types;
				try {
					types = assembly.GetTypes();
				} catch (ReflectionTypeLoadException exception) {
					types = exception.Types.Where(type => type != null);
					loaderErrors.AddRange(exception.LoaderExceptions
						.Where(error => error != null)
						.Select(error => error.Message));
				} catch (Exception exception) {
					loaderErrors.Add(exception.Message);
					continue;
				}

				matches.AddRange(types.Where(type =>
					string.Equals(type.Name, legacyName, StringComparison.Ordinal) ||
					string.Equals(type.FullName, legacyName, StringComparison.Ordinal)));
			}

			return new LegacyTypeLookupResult(
				matches.GroupBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
					.Select(group => group.First()).ToArray(),
				loaderErrors);
		}
	}

	internal static class LegacyConfigurationMigration {
		internal const string SettingsPath = LegacyProjectConfigFormat.SettingsPath;
		internal const string LegacyScenesGroupName = "ScenesGroup";
		internal const string ScenesConfigScriptGuid = "485de64a98f018044b1d84b3d9a26955";
		internal const string AddressableAssetsConfigScriptGuid = "bb1976c08118e184da01617a660f81b5";

		internal static LegacyMigrationPreview Preview() {
			if (!File.Exists(SettingsPath)) {
				var missing = new PreviewBuilder();
				missing.Add(
					LegacyMigrationDiagnosticCode.SettingsMissing,
					ConfigurationDiagnosticSeverity.Info,
					LegacyConfigurationKind.ProjectSettings,
					SettingsPath,
					"No legacy ProjectConfig.json exists. Nothing was created or changed.");
				return missing.Build();
			}

			try {
				return Preview(File.ReadAllText(SettingsPath), new UnityLegacyMigrationEnvironment());
			} catch (Exception exception) {
				var failed = new PreviewBuilder();
				failed.Add(
					LegacyMigrationDiagnosticCode.SettingsCorrupt,
					ConfigurationDiagnosticSeverity.Error,
					LegacyConfigurationKind.ProjectSettings,
					SettingsPath,
					$"The legacy settings file could not be read: {exception.Message}");
				return failed.Build();
			}
		}

		internal static LegacyMigrationPreview Preview(
			string json,
			ILegacyMigrationEnvironment environment) {
			var builder = new PreviewBuilder();
			if (string.IsNullOrWhiteSpace(json)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SettingsCorrupt,
					ConfigurationDiagnosticSeverity.Error,
					LegacyConfigurationKind.ProjectSettings,
					SettingsPath,
					"The legacy settings file is empty.");
				return builder.Build();
			}

			var trimmed = json.Trim();
			if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
			    !trimmed.EndsWith("}", StringComparison.Ordinal)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SettingsCorrupt,
					ConfigurationDiagnosticSeverity.Error,
					LegacyConfigurationKind.ProjectSettings,
					SettingsPath,
					"The legacy settings file is not a complete JSON object. Recoverable fields were still inspected independently.");
			}

			builder.ScenesGuid = ExtractString(
				json, nameof(LegacyProjectConfigData.m_ScenesListConfigGUID),
				LegacyConfigurationKind.Scenes, builder);
			builder.AddressablesGuid = ExtractString(
				json, nameof(LegacyProjectConfigData.m_AddressableAssetsConfigGUID),
				LegacyConfigurationKind.AddressableAssets, builder);
			builder.AppStatesGuid = ExtractString(
				json, nameof(LegacyProjectConfigData.m_AppStatesConfigGUID),
				LegacyConfigurationKind.AppStates, builder);

			MapSection(
				LegacyConfigurationKind.AddressableAssets,
				() => MapAddressableAssets(builder.AddressablesGuid, environment, builder),
				builder);
			MapSection(
				LegacyConfigurationKind.Scenes,
				() => MapScenes(builder.ScenesGuid, environment, builder),
				builder);
			MapSection(
				LegacyConfigurationKind.AppStates,
				() => ReportAppStates(builder.AppStatesGuid, environment, builder),
				builder);
			return builder.Build();
		}

		private static void MapSection(
			LegacyConfigurationKind kind,
			Action action,
			PreviewBuilder builder) {
			try {
				action();
			} catch (Exception exception) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SectionFailed,
					ConfigurationDiagnosticSeverity.Error,
					kind,
					kind.ToString(),
					$"This legacy section could not be inspected: {exception.Message}. Other sections were retained.");
			}
		}

		private static string ExtractString(
			string json,
			string fieldName,
			LegacyConfigurationKind kind,
			PreviewBuilder builder) {
			var marker = "\"" + fieldName + "\"";
			var markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
			if (markerIndex < 0) {
				builder.Add(
					LegacyMigrationDiagnosticCode.ReferenceFieldMissing,
					ConfigurationDiagnosticSeverity.Warning,
					kind, fieldName,
					$"Legacy field '{fieldName}' is absent; other fields were preserved.");
				return string.Empty;
			}

			var colon = json.IndexOf(':', markerIndex + marker.Length);
			if (colon < 0) {
				return MalformedField(kind, fieldName, builder);
			}

			var index = colon + 1;
			while (index < json.Length && char.IsWhiteSpace(json[index])) {
				index++;
			}
			if (index + 4 <= json.Length &&
			    string.Equals(json.Substring(index, 4), "null", StringComparison.Ordinal)) {
				return string.Empty;
			}
			if (index >= json.Length || json[index] != '"') {
				return MalformedField(kind, fieldName, builder);
			}

			var start = index;
			var escaped = false;
			for (index++; index < json.Length; index++) {
				var character = json[index];
				if (escaped) {
					escaped = false;
					continue;
				}
				if (character == '\\') {
					escaped = true;
					continue;
				}
				if (character != '"') {
					continue;
				}

				try {
					var token = json.Substring(start, index - start + 1);
					var value = JsonUtility.FromJson<JsonStringValue>("{\"value\":" + token + "}");
					var result = value?.value ?? string.Empty;
					if (!string.IsNullOrEmpty(result) && !AddressablesAutomationProjectSettingsStore.IsGuid(result)) {
						builder.Add(
							LegacyMigrationDiagnosticCode.ReferenceMalformed,
							ConfigurationDiagnosticSeverity.Error,
							kind, fieldName,
							$"Legacy value '{result}' is not a Unity GUID.", result);
					}
					return result;
				} catch (Exception) {
					return MalformedField(kind, fieldName, builder);
				}
			}

			return MalformedField(kind, fieldName, builder);
		}

		private static string MalformedField(
			LegacyConfigurationKind kind,
			string fieldName,
			PreviewBuilder builder) {
			builder.Add(
				LegacyMigrationDiagnosticCode.ReferenceMalformed,
				ConfigurationDiagnosticSeverity.Error,
				kind, fieldName,
				$"Legacy field '{fieldName}' is not a valid JSON string. Other fields were inspected independently.");
			return string.Empty;
		}

		private static void MapAddressableAssets(
			string guid,
			ILegacyMigrationEnvironment environment,
			PreviewBuilder builder) {
			if (!TryLoadConfig(
				    guid, AddressableAssetsConfigScriptGuid, LegacyConfigurationKind.AddressableAssets,
				    environment, builder, out var serialized)) {
				return;
			}

			var settings = serialized.FindProperty("m_Settings");
			if (!RequireArray(settings, LegacyConfigurationKind.AddressableAssets, "m_Settings", builder)) {
				return;
			}

			for (var index = 0; index < settings.arraySize; index++) {
				var item = settings.GetArrayElementAtIndex(index);
				var location = $"m_Settings[{index}]";
				var groupName = ReadString(item, "m_groupName", LegacyConfigurationKind.AddressableAssets, location, builder);
				var folder = ReadObject(item, "m_assetsFolder", LegacyConfigurationKind.AddressableAssets, location, builder);
				var labels = ReadStringArray(item, "m_lables", LegacyConfigurationKind.AddressableAssets, location, builder);
				var filterByType = ReadBool(item, "m_filterByType", LegacyConfigurationKind.AddressableAssets, location, builder);
				var types = filterByType
					? MigrateTypes(ReadStringArray(
						item, "m_typesFilterNames", LegacyConfigurationKind.AddressableAssets, location, builder),
						environment, location, builder)
					: Array.Empty<string>();

				if (!TryMapFolder(folder, environment, LegacyConfigurationKind.AddressableAssets,
					    location + ".m_assetsFolder", builder, out var folderGuid)) {
					continue;
				}

				environment.TryGetGroupGuid(groupName, out var groupGuid);
				builder.GroupRules.Add(new GroupSyncRule(
					folderGuid, Array.Empty<string>(), groupGuid, groupName, string.Empty,
					GroupAddressPolicy.RelativePath, ExistingLabelPolicy.PreserveUnrelated,
					labels, types));
			}
		}

		private static void MapScenes(
			string guid,
			ILegacyMigrationEnvironment environment,
			PreviewBuilder builder) {
			if (!TryLoadConfig(
				    guid, ScenesConfigScriptGuid, LegacyConfigurationKind.Scenes,
				    environment, builder, out var serialized)) {
				return;
			}

			MapSceneFolder(
				ReadObject(serialized, "m_ScenesLocation", LegacyConfigurationKind.Scenes, "Scenes", builder),
				SceneFolderMode.Addressable, environment, builder, "m_ScenesLocation");
			MapSceneFolder(
				ReadObject(serialized, "m_UIScenesLocation", LegacyConfigurationKind.Scenes, "Scenes", builder),
				SceneFolderMode.LocalBuildSettings, environment, builder, "m_UIScenesLocation");

			var otherFolders = serialized.FindProperty("m_OtherSceneFolders");
			if (RequireArray(otherFolders, LegacyConfigurationKind.Scenes, "m_OtherSceneFolders", builder)) {
				for (var index = 0; index < otherFolders.arraySize; index++) {
					var folder = otherFolders.GetArrayElementAtIndex(index).objectReferenceValue;
					if (!TryMapFolder(folder, environment, LegacyConfigurationKind.Scenes,
						    $"m_OtherSceneFolders[{index}]", builder, out var folderGuid)) {
						continue;
					}
					builder.SceneRules.Add(new SceneFolderRule(
						folderGuid, Array.Empty<string>(), SceneFolderMode.Unspecified,
						string.Empty, string.Empty, string.Empty, string.Empty,
						SceneAddressPolicy.RelativePath, Array.Empty<string>()));
					builder.Add(
						LegacyMigrationDiagnosticCode.AdditionalSceneModeRequired,
						ConfigurationDiagnosticSeverity.Error,
						LegacyConfigurationKind.Scenes, $"m_OtherSceneFolders[{index}]",
						"The legacy additional folder had no semantics. Its GUID was retained, but Addressable or Local mode must be selected explicitly.",
						folderGuid);
				}
			}

			var sceneCatalog = serialized.FindProperty("m_ScenesConfig");
			if (sceneCatalog == null) {
				SchemaMismatch(LegacyConfigurationKind.Scenes, "m_ScenesConfig", builder);
			} else if (sceneCatalog.objectReferenceValue != null) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SceneCatalogRetained,
					ConfigurationDiagnosticSeverity.Info,
					LegacyConfigurationKind.Scenes, "m_ScenesConfig",
					"The generated legacy scene catalog remains untouched and is not copied into the new rule configuration.",
					environment.GetAssetPath(sceneCatalog.objectReferenceValue));
			}
		}

		private static void MapSceneFolder(
			UnityEngine.Object folder,
			SceneFolderMode mode,
			ILegacyMigrationEnvironment environment,
			PreviewBuilder builder,
			string location) {
			if (folder == null || !TryMapFolder(
				    folder, environment, LegacyConfigurationKind.Scenes, location, builder, out var folderGuid)) {
				return;
			}

			var groupName = mode == SceneFolderMode.Addressable ? LegacyScenesGroupName : string.Empty;
			var groupGuid = string.Empty;
			if (!string.IsNullOrEmpty(groupName)) {
				environment.TryGetGroupGuid(groupName, out groupGuid);
			}
			builder.SceneRules.Add(new SceneFolderRule(
				folderGuid, Array.Empty<string>(), mode, groupGuid, groupName,
				string.Empty, string.Empty,
				mode == SceneFolderMode.Addressable
					? SceneAddressPolicy.PreserveManagedAddress
					: SceneAddressPolicy.RelativePath,
				Array.Empty<string>()));
		}

		private static void ReportAppStates(
			string guid,
			ILegacyMigrationEnvironment environment,
			PreviewBuilder builder) {
			if (string.IsNullOrEmpty(guid)) {
				return;
			}

			var path = environment.GuidToAssetPath(guid);
			var exists = !string.IsNullOrEmpty(path) && environment.LoadMainAssetAtPath(path) != null;
			builder.Add(
				LegacyMigrationDiagnosticCode.AppStatesIntentionallyIgnored,
				ConfigurationDiagnosticSeverity.Warning,
				LegacyConfigurationKind.AppStates, nameof(LegacyProjectConfigData.m_AppStatesConfigGUID),
				exists
					? "Numeric application-state mappings are game-specific and were intentionally not migrated. The legacy asset remains untouched."
					: "The app-state GUID does not resolve, and numeric application-state mappings are intentionally not migrated.",
				guid);
		}

		private static bool TryLoadConfig(
			string guid,
			string expectedScriptGuid,
			LegacyConfigurationKind kind,
			ILegacyMigrationEnvironment environment,
			PreviewBuilder builder,
			out SerializedObject serialized) {
			serialized = null;
			if (string.IsNullOrEmpty(guid) || !AddressablesAutomationProjectSettingsStore.IsGuid(guid)) {
				return false;
			}

			var path = environment.GuidToAssetPath(guid);
			var asset = string.IsNullOrEmpty(path) ? null : environment.LoadMainAssetAtPath(path);
			if (!(asset is ScriptableObject scriptableObject)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.ReferencedAssetMissing,
					ConfigurationDiagnosticSeverity.Error,
					kind, path, $"Legacy GUID '{guid}' does not resolve to a ScriptableObject.", guid);
				return false;
			}

			var actualScriptGuid = environment.GetMonoScriptGuid(scriptableObject);
			if (!string.Equals(actualScriptGuid, expectedScriptGuid, StringComparison.OrdinalIgnoreCase)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.ReferencedAssetWrongType,
					ConfigurationDiagnosticSeverity.Error,
					kind, path,
					$"Legacy asset uses script GUID '{actualScriptGuid}', expected '{expectedScriptGuid}'.",
					guid);
				return false;
			}

			serialized = new SerializedObject(scriptableObject);
			serialized.UpdateIfRequiredOrScript();
			return true;
		}

		private static bool TryMapFolder(
			UnityEngine.Object folder,
			ILegacyMigrationEnvironment environment,
			LegacyConfigurationKind kind,
			string location,
			PreviewBuilder builder,
			out string guid) {
			guid = string.Empty;
			var path = folder == null
				? string.Empty
				: (environment.GetAssetPath(folder) ?? string.Empty).Replace('\\', '/');
			if (string.IsNullOrEmpty(path) || !environment.IsValidFolder(path)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SourceFolderMissing,
					ConfigurationDiagnosticSeverity.Error,
					kind, location, "The legacy source folder is missing or unresolved.");
				return false;
			}

			guid = environment.AssetPathToGuid(path);
			if (string.IsNullOrEmpty(guid)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SourceFolderMissing,
					ConfigurationDiagnosticSeverity.Error,
					kind, location, $"Folder '{path}' has no persistent Unity GUID.");
				return false;
			}

			if (!path.StartsWith("Assets/", StringComparison.Ordinal)) {
				builder.Add(
					LegacyMigrationDiagnosticCode.SourceFolderOutsideAssets,
					ConfigurationDiagnosticSeverity.Error,
					kind, location,
					$"Folder '{path}' is outside the host Assets folder. Its GUID was retained, but automation remains blocked.", guid);
			}
			return true;
		}

		private static string[] MigrateTypes(
			string[] legacyNames,
			ILegacyMigrationEnvironment environment,
			string location,
			PreviewBuilder builder) {
			var migrated = new string[legacyNames.Length];
			for (var index = 0; index < legacyNames.Length; index++) {
				var legacyName = legacyNames[index] ?? string.Empty;
				var typeLocation = $"{location}.m_typesFilterNames[{index}]";
				if (string.IsNullOrWhiteSpace(legacyName)) {
					builder.Add(
						LegacyMigrationDiagnosticCode.TypeFilterEmpty,
						ConfigurationDiagnosticSeverity.Error,
						LegacyConfigurationKind.AddressableAssets, typeLocation,
						"An enabled legacy type filter is empty. It was retained so the new validator fails closed.");
					migrated[index] = legacyName;
					continue;
				}

				var result = environment.FindTypes(legacyName);
				foreach (var loaderError in result.LoaderErrors) {
					builder.Add(
						LegacyMigrationDiagnosticCode.TypeLoaderWarning,
						ConfigurationDiagnosticSeverity.Warning,
						LegacyConfigurationKind.AddressableAssets, typeLocation,
						$"A project assembly was only partially inspected: {loaderError}", legacyName);
				}

				if (result.Matches.Count == 1 &&
				    !string.IsNullOrEmpty(result.Matches[0].AssemblyQualifiedName)) {
					migrated[index] = result.Matches[0].AssemblyQualifiedName;
				} else {
					var ambiguous = result.Matches.Count > 1;
					builder.Add(
						ambiguous
							? LegacyMigrationDiagnosticCode.TypeFilterAmbiguous
							: LegacyMigrationDiagnosticCode.TypeFilterUnresolved,
						ConfigurationDiagnosticSeverity.Error,
						LegacyConfigurationKind.AddressableAssets, typeLocation,
						ambiguous
							? $"Legacy type name '{legacyName}' matches {result.Matches.Count} types; choose an assembly-qualified type explicitly."
							: $"Legacy type name '{legacyName}' could not be resolved; choose an assembly-qualified type explicitly.",
						legacyName);
					migrated[index] = legacyName;
				}
			}
			return migrated;
		}

		private static bool RequireArray(
			SerializedProperty property,
			LegacyConfigurationKind kind,
			string location,
			PreviewBuilder builder) {
			if (property != null && property.isArray) {
				return true;
			}
			SchemaMismatch(kind, location, builder);
			return false;
		}

		private static string ReadString(
			SerializedProperty parent, string name, LegacyConfigurationKind kind,
			string location, PreviewBuilder builder) {
			var property = parent?.FindPropertyRelative(name);
			if (property != null) {
				return property.stringValue ?? string.Empty;
			}
			SchemaMismatch(kind, location + "." + name, builder);
			return string.Empty;
		}

		private static bool ReadBool(
			SerializedProperty parent, string name, LegacyConfigurationKind kind,
			string location, PreviewBuilder builder) {
			var property = parent?.FindPropertyRelative(name);
			if (property != null) {
				return property.boolValue;
			}
			SchemaMismatch(kind, location + "." + name, builder);
			return false;
		}

		private static UnityEngine.Object ReadObject(
			SerializedObject parent, string name, LegacyConfigurationKind kind,
			string location, PreviewBuilder builder) {
			var property = parent.FindProperty(name);
			if (property != null) {
				return property.objectReferenceValue;
			}
			SchemaMismatch(kind, location + "." + name, builder);
			return null;
		}

		private static UnityEngine.Object ReadObject(
			SerializedProperty parent, string name, LegacyConfigurationKind kind,
			string location, PreviewBuilder builder) {
			var property = parent?.FindPropertyRelative(name);
			if (property != null) {
				return property.objectReferenceValue;
			}
			SchemaMismatch(kind, location + "." + name, builder);
			return null;
		}

		private static string[] ReadStringArray(
			SerializedProperty parent, string name, LegacyConfigurationKind kind,
			string location, PreviewBuilder builder) {
			var property = parent?.FindPropertyRelative(name);
			if (!RequireArray(property, kind, location + "." + name, builder)) {
				return Array.Empty<string>();
			}
			var values = new string[property.arraySize];
			for (var index = 0; index < values.Length; index++) {
				values[index] = property.GetArrayElementAtIndex(index).stringValue ?? string.Empty;
			}
			return values;
		}

		private static void SchemaMismatch(
			LegacyConfigurationKind kind,
			string location,
			PreviewBuilder builder) {
			builder.Add(
				LegacyMigrationDiagnosticCode.ReferencedAssetSchemaMismatch,
				ConfigurationDiagnosticSeverity.Error,
				kind, location,
				$"Expected legacy serialized field '{location}' is missing or has an incompatible shape.");
		}

		[Serializable]
		private sealed class JsonStringValue {
			public string value;
		}

		private sealed class PreviewBuilder {
			internal string ScenesGuid = string.Empty;
			internal string AddressablesGuid = string.Empty;
			internal string AppStatesGuid = string.Empty;
			internal readonly List<GroupSyncRule> GroupRules = new List<GroupSyncRule>();
			internal readonly List<SceneFolderRule> SceneRules = new List<SceneFolderRule>();
			private readonly List<LegacyMigrationDiagnostic> m_diagnostics =
				new List<LegacyMigrationDiagnostic>();

			internal void Add(
				LegacyMigrationDiagnosticCode code,
				ConfigurationDiagnosticSeverity severity,
				LegacyConfigurationKind kind,
				string location,
				string message,
				string legacyValue = "") {
				m_diagnostics.Add(new LegacyMigrationDiagnostic(
					code, severity, kind, location, message, legacyValue));
			}

			internal LegacyMigrationPreview Build() {
				return new LegacyMigrationPreview(
					ScenesGuid, AddressablesGuid, AppStatesGuid,
					GroupRules.ToArray(), SceneRules.ToArray(), m_diagnostics.ToArray());
			}
		}
	}
}
