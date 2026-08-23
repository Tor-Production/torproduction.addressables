using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	public enum ConfigurationDiagnosticSeverity {
		Info,
		Warning,
		Error
	}

	public enum ConfigurationDiagnosticCode {
		ConfigurationMissing,
		ConfigurationIsAddressable,
		ScopeInvalid,
		ConfigSchemaMigrationRequired,
		ConfigSchemaUnsupported,
		AddressablesSettingsMissing,
		RuleCollectionMissing,
		RuleMissing,
		SourceFolderGuidMissing,
		SourceFolderMissing,
		SourceFolderOutsideAssets,
		ExcludedFolderGuidMissing,
		ExcludedFolderMissing,
		ExcludedFolderOutsideSource,
		ExcludedFolderDuplicate,
		DestinationGroupMissing,
		DestinationGroupNotFound,
		DestinationGroupNameMismatch,
		AddressPolicyInvalid,
		AddressPrefixInvalid,
		LabelPolicyInvalid,
		LabelEmpty,
		LabelDuplicate,
		LabelNotFound,
		TypeFilterEmpty,
		TypeFilterDuplicate,
		TypeFilterNotAssemblyQualified,
		TypeFilterUnresolved,
		SceneModeInvalid,
		UnexpectedSceneGroup,
		DependencySettingsMissing,
		RuleOverlap
	}

	public sealed class ConfigurationDiagnostic {
		internal ConfigurationDiagnostic(
			ConfigurationDiagnosticCode code,
			ConfigurationDiagnosticSeverity severity,
			string location,
			string message) {
			Code = code;
			Severity = severity;
			Location = location ?? string.Empty;
			Message = message ?? string.Empty;
		}

		public ConfigurationDiagnosticCode Code { get; }
		public ConfigurationDiagnosticSeverity Severity { get; }
		public string Location { get; }
		public string Message { get; }
	}

	public sealed class ConfigurationValidationReport {
		private readonly List<ConfigurationDiagnostic> m_diagnostics = new List<ConfigurationDiagnostic>();

		public IReadOnlyList<ConfigurationDiagnostic> Diagnostics => m_diagnostics;
		public bool IsValid => !m_diagnostics.Exists(item =>
			item.Severity == ConfigurationDiagnosticSeverity.Error);

		internal void Add(
			ConfigurationDiagnosticCode code,
			ConfigurationDiagnosticSeverity severity,
			string location,
			string message) {
			m_diagnostics.Add(new ConfigurationDiagnostic(code, severity, location, message));
		}
	}

	internal interface IConfigurationAssetResolver {
		string GuidToAssetPath(string guid);
		UnityEngine.Object LoadMainAssetAtPath(string path);
		bool IsValidFolder(string path);
		Type ResolveType(string assemblyQualifiedName);
	}

	internal sealed class UnityConfigurationAssetResolver : IConfigurationAssetResolver {
		internal static readonly UnityConfigurationAssetResolver Instance = new UnityConfigurationAssetResolver();

		private UnityConfigurationAssetResolver() { }

		public string GuidToAssetPath(string guid) {
			return AssetDatabase.GUIDToAssetPath(guid);
		}

		public UnityEngine.Object LoadMainAssetAtPath(string path) {
			return AssetDatabase.LoadMainAssetAtPath(path);
		}

		public bool IsValidFolder(string path) {
			return AssetDatabase.IsValidFolder(path);
		}

		public Type ResolveType(string assemblyQualifiedName) {
			return Type.GetType(assemblyQualifiedName, false);
		}
	}

	internal interface IAddressablesSettingsView {
		bool Exists { get; }
		bool TryGetGroupName(string groupGuid, out string groupName);
		bool TryGetGroupGuid(string groupName, out string groupGuid);
		bool HasGroupName(string groupName);
		bool HasLabel(string label);
		bool HasAssetEntry(string assetGuid);
	}

	internal sealed class AddressablesSettingsView : IAddressablesSettingsView {
		private readonly AddressableAssetSettings m_settings;

		internal AddressablesSettingsView() {
			// GetSettings(false) can still migrate Addressables' legacy default object in 2.7.x.
			// SettingsExists is the inert preflight that prevents that write-on-read path.
			m_settings = AddressableAssetSettingsDefaultObject.SettingsExists
				? AddressableAssetSettingsDefaultObject.GetSettings(false)
				: null;
		}

		public bool Exists => m_settings != null;

		public bool TryGetGroupName(string groupGuid, out string groupName) {
			groupName = string.Empty;
			if (m_settings == null || string.IsNullOrEmpty(groupGuid)) {
				return false;
			}

			foreach (var group in m_settings.groups) {
				if (group != null && string.Equals(group.Guid, groupGuid, StringComparison.Ordinal)) {
					groupName = group.Name;
					return true;
				}
			}

			return false;
		}

		public bool TryGetGroupGuid(string groupName, out string groupGuid) {
			groupGuid = string.Empty;
			var group = m_settings == null ? null : m_settings.FindGroup(groupName);
			if (group == null) {
				return false;
			}

			groupGuid = group.Guid;
			return !string.IsNullOrEmpty(groupGuid);
		}

		public bool HasGroupName(string groupName) {
			return m_settings != null && m_settings.FindGroup(groupName) != null;
		}

		public bool HasLabel(string label) {
			return m_settings != null && m_settings.GetLabels().Contains(label);
		}

		public bool HasAssetEntry(string assetGuid) {
			return m_settings != null && m_settings.FindAssetEntry(assetGuid, true) != null;
		}
	}

	public static class AddressablesAutomationValidator {
		public static ConfigurationValidationReport Validate(AddressablesAutomationConfig config) {
			return Validate(config, AutomationScope.All);
		}

		public static ConfigurationValidationReport Validate(
			AddressablesAutomationConfig config,
			AutomationScope scope) {
			return Validate(
				config,
				UnityConfigurationAssetResolver.Instance,
				new AddressablesSettingsView(),
				scope);
		}

		internal static ConfigurationValidationReport Validate(
			AddressablesAutomationConfig config,
			IConfigurationAssetResolver resolver,
			IAddressablesSettingsView addressables,
			AutomationScope scope = AutomationScope.All) {
			var report = new ConfigurationValidationReport();
			if (config == null) {
				report.Add(
					ConfigurationDiagnosticCode.ConfigurationMissing,
					ConfigurationDiagnosticSeverity.Error,
					"Configuration",
					"Select an Addressables Automation configuration asset.");
				return report;
			}

			if (scope == AutomationScope.None || (scope & ~AutomationScope.All) != 0) {
				report.Add(
					ConfigurationDiagnosticCode.ScopeInvalid,
					ConfigurationDiagnosticSeverity.Error,
					"Scope",
					"Select at least one supported automation scope.");
			}

			if (config.SchemaVersion < AddressablesAutomationConfig.CurrentSchemaVersion) {
				report.Add(
					ConfigurationDiagnosticCode.ConfigSchemaMigrationRequired,
					ConfigurationDiagnosticSeverity.Error,
					"Configuration",
					$"Configuration schema {config.SchemaVersion} requires an explicit migration to {AddressablesAutomationConfig.CurrentSchemaVersion}.");
			} else if (config.SchemaVersion > AddressablesAutomationConfig.CurrentSchemaVersion) {
				report.Add(
					ConfigurationDiagnosticCode.ConfigSchemaUnsupported,
					ConfigurationDiagnosticSeverity.Error,
					"Configuration",
					$"Configuration schema {config.SchemaVersion} is newer than supported schema {AddressablesAutomationConfig.CurrentSchemaVersion}.");
			}

			if (!addressables.Exists) {
				report.Add(
					ConfigurationDiagnosticCode.AddressablesSettingsMissing,
					ConfigurationDiagnosticSeverity.Error,
					"Addressables",
					"Addressables settings do not exist. Create them separately in the Addressables Groups window, then analyze again.");
			}

			var folders = new List<ValidatedFolder>();
			if ((scope & AutomationScope.Groups) != 0) {
				ValidateGroupRules(config.SerializedGroupRules, resolver, addressables, report, folders);
			}
			if ((scope & AutomationScope.Scenes) != 0) {
				ValidateSceneRules(config.SerializedSceneRules, resolver, addressables, report, folders);
			}
			if ((scope & AutomationScope.Dependencies) != 0) {
				ValidateDependencySettings(config.SerializedDependencySettings, addressables, report);
			}
			ValidateOverlaps(folders, report);
			return report;
		}

		private static void ValidateDependencySettings(
			DependencyAnalysisSettings settings,
			IAddressablesSettingsView addressables,
			ConfigurationValidationReport report) {
			if (settings == null) {
				report.Add(
					ConfigurationDiagnosticCode.DependencySettingsMissing,
					ConfigurationDiagnosticSeverity.Error,
					"Dependencies",
					"Dependency analysis settings are missing. Explicitly migrate or recreate the configuration.");
				return;
			}

			ValidateDestinationGroup(
				settings.DestinationGroupGuid,
				settings.DestinationGroupName,
				"Dependencies",
				addressables,
				report);
		}

		private static void ValidateGroupRules(
			GroupSyncRule[] rules,
			IConfigurationAssetResolver resolver,
			IAddressablesSettingsView addressables,
			ConfigurationValidationReport report,
			List<ValidatedFolder> folders) {
			if (rules == null) {
				report.Add(
					ConfigurationDiagnosticCode.RuleCollectionMissing,
					ConfigurationDiagnosticSeverity.Error,
					"Groups",
					"The group rule collection is missing. Recreate or explicitly migrate the configuration.");
				return;
			}

			for (var index = 0; index < rules.Length; index++) {
				var location = $"Groups[{index}]";
				var rule = rules[index];
				if (rule == null) {
					report.Add(
						ConfigurationDiagnosticCode.RuleMissing,
						ConfigurationDiagnosticSeverity.Error,
						location,
						"Remove or replace the missing group rule.");
					continue;
				}

				ValidateFolder(
					rule.SourceFolderGuid,
					rule.SerializedExcludedNestedFolderGuids,
					location,
					resolver,
					report,
					folders);
				ValidateAddressPolicy(rule.AddressPolicy, location, report);
				ValidateAddressPrefix(rule.AddressPrefix, location, report);
				if (!Enum.IsDefined(typeof(ExistingLabelPolicy), rule.LabelPolicy)) {
					report.Add(
						ConfigurationDiagnosticCode.LabelPolicyInvalid,
						ConfigurationDiagnosticSeverity.Error,
						location,
						"Select a supported existing-label policy.");
				}

				ValidateDestinationGroup(
					rule.DestinationGroupGuid,
					rule.DestinationGroupName,
					location,
					addressables,
					report);
				ValidateLabels(rule.SerializedRequiredLabels, location, addressables, report);
				ValidateTypes(rule.SerializedTypeFilters, location, resolver, report);
			}
		}

		private static void ValidateSceneRules(
			SceneFolderRule[] rules,
			IConfigurationAssetResolver resolver,
			IAddressablesSettingsView addressables,
			ConfigurationValidationReport report,
			List<ValidatedFolder> folders) {
			if (rules == null) {
				report.Add(
					ConfigurationDiagnosticCode.RuleCollectionMissing,
					ConfigurationDiagnosticSeverity.Error,
					"Scenes",
					"The scene rule collection is missing. Recreate or explicitly migrate the configuration.");
				return;
			}

			for (var index = 0; index < rules.Length; index++) {
				var location = $"Scenes[{index}]";
				var rule = rules[index];
				if (rule == null) {
					report.Add(
						ConfigurationDiagnosticCode.RuleMissing,
						ConfigurationDiagnosticSeverity.Error,
						location,
						"Remove or replace the missing scene rule.");
					continue;
				}

				ValidateFolder(
					rule.SourceFolderGuid,
					rule.SerializedExcludedNestedFolderGuids,
					location,
					resolver,
					report,
					folders);
				ValidateSceneAddressPolicy(rule.AddressPolicy, location, report);
				ValidateAddressPrefix(rule.AddressPrefix, location, report);
				var sceneLabels = (rule.SerializedRequiredLabels ?? Array.Empty<string>())
					.Concat(string.IsNullOrWhiteSpace(rule.Category)
						? Array.Empty<string>()
						: new[] { rule.Category })
					.ToArray();
				ValidateLabels(sceneLabels, location, addressables, report);

				if (!Enum.IsDefined(typeof(SceneFolderMode), rule.Mode) ||
				    rule.Mode == SceneFolderMode.Unspecified) {
					report.Add(
						ConfigurationDiagnosticCode.SceneModeInvalid,
						ConfigurationDiagnosticSeverity.Error,
						location,
						"Select Addressable or Local Build Settings mode.");
					continue;
				}

				if (rule.Mode == SceneFolderMode.Addressable) {
					ValidateDestinationGroup(
						rule.DestinationGroupGuid,
						rule.DestinationGroupName,
						location,
						addressables,
						report);
				} else {
					if (!string.IsNullOrWhiteSpace(rule.DestinationGroupGuid) ||
					    !string.IsNullOrWhiteSpace(rule.DestinationGroupName)) {
						report.Add(
							ConfigurationDiagnosticCode.UnexpectedSceneGroup,
							ConfigurationDiagnosticSeverity.Error,
							location,
							"Local Build Settings scene rules must not specify an Addressables group.");
					}
					if (rule.AddressPolicy != SceneAddressPolicy.RelativePath) {
						report.Add(
							ConfigurationDiagnosticCode.AddressPolicyInvalid,
							ConfigurationDiagnosticSeverity.Error,
							location,
							"Local Build Settings scene rules must use the neutral Relative Path policy.");
					}
					if (!string.IsNullOrEmpty(rule.AddressPrefix)) {
						report.Add(
							ConfigurationDiagnosticCode.AddressPrefixInvalid,
							ConfigurationDiagnosticSeverity.Error,
							location,
							"Local Build Settings scene rules must not specify an Addressables prefix.");
					}
				}
			}
		}

		private static void ValidateFolder(
			string guid,
			string[] excludedFolderGuids,
			string location,
			IConfigurationAssetResolver resolver,
			ConfigurationValidationReport report,
			List<ValidatedFolder> folders) {
			if (string.IsNullOrWhiteSpace(guid)) {
				report.Add(
					ConfigurationDiagnosticCode.SourceFolderGuidMissing,
					ConfigurationDiagnosticSeverity.Error,
					location,
					"Select a source folder so its GUID can be stored.");
				return;
			}

			string path;
			try {
				path = resolver.GuidToAssetPath(guid);
			} catch (Exception exception) {
				report.Add(
					ConfigurationDiagnosticCode.SourceFolderMissing,
					ConfigurationDiagnosticSeverity.Error,
					location,
					$"The source folder GUID '{guid}' could not be resolved: {exception.Message}");
				return;
			}
			if (string.IsNullOrEmpty(path) || !resolver.IsValidFolder(path)) {
				report.Add(
					ConfigurationDiagnosticCode.SourceFolderMissing,
					ConfigurationDiagnosticSeverity.Error,
					location,
					$"The source folder GUID '{guid}' does not resolve to an existing folder.");
				return;
			}

			path = path.Replace('\\', '/').TrimEnd('/');
			if (!path.Equals("Assets", StringComparison.Ordinal) &&
			    !path.StartsWith("Assets/", StringComparison.Ordinal)) {
				report.Add(
					ConfigurationDiagnosticCode.SourceFolderOutsideAssets,
					ConfigurationDiagnosticSeverity.Error,
					location,
					$"The source folder '{path}' is outside the project Assets folder.");
				return;
			}

			var excludedPaths = ValidateExcludedFolders(
				excludedFolderGuids,
				path,
				location,
				resolver,
				report);
			folders.Add(new ValidatedFolder(path, location, excludedPaths));
		}

		private static IReadOnlyList<string> ValidateExcludedFolders(
			string[] excludedFolderGuids,
			string sourcePath,
			string location,
			IConfigurationAssetResolver resolver,
			ConfigurationValidationReport report) {
			if (excludedFolderGuids == null || excludedFolderGuids.Length == 0) {
				return Array.Empty<string>();
			}

			var paths = new List<string>();
			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (var index = 0; index < excludedFolderGuids.Length; index++) {
				var guid = excludedFolderGuids[index];
				var excludedLocation = $"{location}.ExcludedFolders[{index}]";
				if (string.IsNullOrWhiteSpace(guid)) {
					report.Add(
						ConfigurationDiagnosticCode.ExcludedFolderGuidMissing,
						ConfigurationDiagnosticSeverity.Error,
						excludedLocation,
						"Remove the empty exclusion or select a nested folder.");
					continue;
				}

				if (!seen.Add(guid)) {
					report.Add(
						ConfigurationDiagnosticCode.ExcludedFolderDuplicate,
						ConfigurationDiagnosticSeverity.Error,
						excludedLocation,
						$"Excluded folder GUID '{guid}' is duplicated.");
					continue;
				}

				string excludedPath;
				try {
					excludedPath = resolver.GuidToAssetPath(guid);
					if (!string.IsNullOrEmpty(excludedPath) && resolver.IsValidFolder(excludedPath)) {
						// Continue with normalized containment checks below.
					} else {
						excludedPath = string.Empty;
					}
				} catch (Exception exception) {
					report.Add(
						ConfigurationDiagnosticCode.ExcludedFolderMissing,
						ConfigurationDiagnosticSeverity.Error,
						excludedLocation,
						$"Excluded folder GUID '{guid}' could not be resolved: {exception.Message}");
					continue;
				}

				if (string.IsNullOrEmpty(excludedPath)) {
					report.Add(
						ConfigurationDiagnosticCode.ExcludedFolderMissing,
						ConfigurationDiagnosticSeverity.Error,
						excludedLocation,
						$"Excluded folder GUID '{guid}' does not resolve to an existing folder.");
					continue;
				}

				excludedPath = excludedPath.Replace('\\', '/').TrimEnd('/');
				if (!excludedPath.StartsWith(sourcePath + "/", StringComparison.Ordinal)) {
					report.Add(
						ConfigurationDiagnosticCode.ExcludedFolderOutsideSource,
						ConfigurationDiagnosticSeverity.Error,
						excludedLocation,
						$"Excluded folder '{excludedPath}' must be nested below source folder '{sourcePath}'.");
					continue;
				}

				paths.Add(excludedPath);
			}

			return paths;
		}

		private static void ValidateDestinationGroup(
			string groupGuid,
			string groupName,
			string location,
			IAddressablesSettingsView addressables,
			ConfigurationValidationReport report) {
			if (string.IsNullOrWhiteSpace(groupGuid) && string.IsNullOrWhiteSpace(groupName)) {
				report.Add(
					ConfigurationDiagnosticCode.DestinationGroupMissing,
					ConfigurationDiagnosticSeverity.Error,
					location,
					"Specify a destination Addressables group name.");
				return;
			}

			if (!addressables.Exists) {
				return;
			}

			if (!string.IsNullOrWhiteSpace(groupGuid) &&
			    addressables.TryGetGroupName(groupGuid, out var resolvedName)) {
				if (!string.IsNullOrWhiteSpace(groupName) &&
				    !string.Equals(groupName, resolvedName, StringComparison.Ordinal)) {
					report.Add(
						ConfigurationDiagnosticCode.DestinationGroupNameMismatch,
						ConfigurationDiagnosticSeverity.Warning,
						location,
						$"Destination group was renamed from '{groupName}' to '{resolvedName}'. The GUID remains valid.");
				}
				return;
			}

			if (!string.IsNullOrWhiteSpace(groupGuid) && string.IsNullOrWhiteSpace(groupName)) {
				report.Add(
					ConfigurationDiagnosticCode.DestinationGroupNotFound,
					ConfigurationDiagnosticSeverity.Error,
					location,
					$"Destination group GUID '{groupGuid}' does not resolve and has no fallback group name.");
				return;
			}

			if (!string.IsNullOrWhiteSpace(groupName) && addressables.HasGroupName(groupName)) {
				report.Add(
					ConfigurationDiagnosticCode.DestinationGroupNameMismatch,
					ConfigurationDiagnosticSeverity.Warning,
					location,
					$"Destination group '{groupName}' exists, but its persistent group-asset GUID is not selected.");
				return;
			}

			if (!string.IsNullOrWhiteSpace(groupGuid) || !string.IsNullOrWhiteSpace(groupName)) {
				var displayName = string.IsNullOrWhiteSpace(groupName) ? groupGuid : groupName;
				report.Add(
					ConfigurationDiagnosticCode.DestinationGroupNotFound,
					ConfigurationDiagnosticSeverity.Warning,
					location,
					$"Addressables group '{displayName}' does not exist; a later Analyze/Apply phase must plan its creation explicitly.");
			}
		}

		private static void ValidateAddressPolicy(
			GroupAddressPolicy policy,
			string location,
			ConfigurationValidationReport report) {
			if (!Enum.IsDefined(typeof(GroupAddressPolicy), policy)) {
				report.Add(
					ConfigurationDiagnosticCode.AddressPolicyInvalid,
					ConfigurationDiagnosticSeverity.Error,
					location,
					"Select a supported address policy.");
			}
		}

		private static void ValidateSceneAddressPolicy(
			SceneAddressPolicy policy,
			string location,
			ConfigurationValidationReport report) {
			if (!Enum.IsDefined(typeof(SceneAddressPolicy), policy)) {
				report.Add(
					ConfigurationDiagnosticCode.AddressPolicyInvalid,
					ConfigurationDiagnosticSeverity.Error,
					location,
					"Select a supported scene address policy.");
			}
		}

		private static void ValidateAddressPrefix(
			string prefix,
			string location,
			ConfigurationValidationReport report) {
			if (string.IsNullOrEmpty(prefix)) {
				return;
			}

			var segments = prefix.Split('/');
			if (prefix.Contains("\\") || prefix.StartsWith("/", StringComparison.Ordinal) ||
			    prefix.EndsWith("/", StringComparison.Ordinal) || prefix.Contains("//") ||
			    Array.Exists(segments, segment => segment == "." || segment == "..")) {
				report.Add(
					ConfigurationDiagnosticCode.AddressPrefixInvalid,
					ConfigurationDiagnosticSeverity.Error,
					location,
					"Address prefixes must be relative, use forward slashes, and contain no empty, '.' or '..' segments.");
			}
		}

		private static void ValidateLabels(
			string[] labels,
			string location,
			IAddressablesSettingsView addressables,
			ConfigurationValidationReport report) {
			if (labels == null) {
				return;
			}

			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (var index = 0; index < labels.Length; index++) {
				var label = labels[index];
				if (string.IsNullOrWhiteSpace(label)) {
					report.Add(
						ConfigurationDiagnosticCode.LabelEmpty,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Labels[{index}]",
						"Remove the empty label or enter a non-empty label name.");
					continue;
				}

				if (!seen.Add(label)) {
					report.Add(
						ConfigurationDiagnosticCode.LabelDuplicate,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Labels[{index}]",
						$"Label '{label}' is duplicated in this rule.");
					continue;
				}

				if (addressables.Exists && !addressables.HasLabel(label)) {
					report.Add(
						ConfigurationDiagnosticCode.LabelNotFound,
						ConfigurationDiagnosticSeverity.Warning,
						$"{location}.Labels[{index}]",
						$"Addressables label '{label}' does not exist; a later Apply phase must create it explicitly.");
				}
			}
		}

		private static void ValidateTypes(
			string[] typeFilters,
			string location,
			IConfigurationAssetResolver resolver,
			ConfigurationValidationReport report) {
			if (typeFilters == null) {
				return;
			}

			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (var index = 0; index < typeFilters.Length; index++) {
				var typeName = typeFilters[index];
				if (string.IsNullOrWhiteSpace(typeName)) {
					report.Add(
						ConfigurationDiagnosticCode.TypeFilterEmpty,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Types[{index}]",
						"Remove the empty type filter or select an assembly-qualified type.");
					continue;
				}

				if (!seen.Add(typeName)) {
					report.Add(
						ConfigurationDiagnosticCode.TypeFilterDuplicate,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Types[{index}]",
						$"Type filter '{typeName}' is duplicated in this rule.");
					continue;
				}

				if (typeName.IndexOf(',') < 0) {
					report.Add(
						ConfigurationDiagnosticCode.TypeFilterNotAssemblyQualified,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Types[{index}]",
						$"Type filter '{typeName}' must use an assembly-qualified name.");
					continue;
				}

				Type resolvedType;
				try {
					resolvedType = resolver.ResolveType(typeName);
				} catch (Exception exception) {
					report.Add(
						ConfigurationDiagnosticCode.TypeFilterUnresolved,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Types[{index}]",
						$"Type filter '{typeName}' could not be resolved: {exception.Message}");
					continue;
				}

				if (resolvedType == null) {
					report.Add(
						ConfigurationDiagnosticCode.TypeFilterUnresolved,
						ConfigurationDiagnosticSeverity.Error,
						$"{location}.Types[{index}]",
						$"Type filter '{typeName}' is not an assembly-qualified type available in this project.");
				}
			}
		}

		private static void ValidateOverlaps(
			List<ValidatedFolder> folders,
			ConfigurationValidationReport report) {
			for (var left = 0; left < folders.Count; left++) {
				for (var right = left + 1; right < folders.Count; right++) {
					if (!FoldersOverlap(folders[left].Path, folders[right].Path) ||
					    IsExplicitlyExcluded(folders[left], folders[right]) ||
					    IsExplicitlyExcluded(folders[right], folders[left])) {
						continue;
					}

					report.Add(
						ConfigurationDiagnosticCode.RuleOverlap,
						ConfigurationDiagnosticSeverity.Error,
						folders[right].Location,
						$"Source folder '{folders[right].Path}' overlaps {folders[left].Location} ('{folders[left].Path}').");
				}
			}
		}

		private static bool FoldersOverlap(string left, string right) {
			return left.Equals(right, StringComparison.Ordinal) ||
			       left.StartsWith(right + "/", StringComparison.Ordinal) ||
			       right.StartsWith(left + "/", StringComparison.Ordinal);
		}

		private static bool IsExplicitlyExcluded(ValidatedFolder parent, ValidatedFolder nested) {
			if (!nested.Path.StartsWith(parent.Path + "/", StringComparison.Ordinal)) {
				return false;
			}

			foreach (var excludedPath in parent.ExcludedPaths) {
				if (nested.Path.Equals(excludedPath, StringComparison.Ordinal) ||
				    nested.Path.StartsWith(excludedPath + "/", StringComparison.Ordinal)) {
					return true;
				}
			}

			return false;
		}

		private readonly struct ValidatedFolder {
			internal ValidatedFolder(
				string path,
				string location,
				IReadOnlyList<string> excludedPaths) {
				Path = path;
				Location = location;
				ExcludedPaths = excludedPaths;
			}

			internal string Path { get; }
			internal string Location { get; }
			internal IReadOnlyList<string> ExcludedPaths { get; }
		}
	}
}
