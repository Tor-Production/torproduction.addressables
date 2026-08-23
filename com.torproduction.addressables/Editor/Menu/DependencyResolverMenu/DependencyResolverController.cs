using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal sealed class DependencyAnalysisAssetState {
		internal string Guid = string.Empty;
		internal string Path = string.Empty;
		internal bool IsExplicit;
		internal string ExplicitGroupGuid = string.Empty;
		internal string ExplicitGroupName = string.Empty;
		internal string[] ReferencingGroupGuids = Array.Empty<string>();
		internal string[] ReferencingGroupNames = Array.Empty<string>();
	}

	internal sealed class DependencyAnalysisProjectState {
		internal bool SettingsExist;
		internal string SettingsIdentity = string.Empty;
		internal string ConfigGuid = string.Empty;
		internal string ConfigJson = string.Empty;
		internal string DestinationGroupGuid = string.Empty;
		internal string DestinationGroupName = string.Empty;
		internal GroupSyncGroupState DestinationGroup;
		internal string AdapterVersion = string.Empty;
		internal bool AdapterSupported;
		internal bool AnalysisSucceeded;
		internal string AdapterDiagnostic = string.Empty;
		internal readonly List<DependencyAnalysisAssetState> Assets = new List<DependencyAnalysisAssetState>();
		internal readonly List<AutomationDiagnostic> Diagnostics = new List<AutomationDiagnostic>();

		internal string ComputeHash() {
			var builder = new StringBuilder();
			Append(builder, SettingsExist ? "1" : "0");
			Append(builder, SettingsIdentity);
			Append(builder, ConfigGuid);
			Append(builder, ConfigJson);
			Append(builder, DestinationGroupGuid);
			Append(builder, DestinationGroupName);
			Append(builder, AdapterVersion);
			Append(builder, AdapterSupported ? "1" : "0");
			Append(builder, AnalysisSucceeded ? "1" : "0");
			Append(builder, AdapterDiagnostic);
			if (DestinationGroup != null) {
				Append(builder, DestinationGroup.Guid);
				Append(builder, DestinationGroup.Name);
				Append(builder, DestinationGroup.ReadOnly ? "1" : "0");
				Append(builder, DestinationGroup.HasBundledSchema ? "1" : "0");
				Append(builder, DestinationGroup.HasContentUpdateSchema ? "1" : "0");
				Append(builder, DestinationGroup.IsBuildable ? "1" : "0");
			}
			foreach (var asset in Assets.OrderBy(item => item.Guid, StringComparer.Ordinal)) {
				Append(builder, asset.Guid);
				Append(builder, asset.Path);
				Append(builder, asset.IsExplicit ? "1" : "0");
				Append(builder, asset.ExplicitGroupGuid);
				Append(builder, asset.ExplicitGroupName);
				foreach (var group in asset.ReferencingGroupGuids.OrderBy(item => item, StringComparer.Ordinal)) {
					Append(builder, group);
				}
				foreach (var group in asset.ReferencingGroupNames.OrderBy(item => item, StringComparer.Ordinal)) {
					Append(builder, group);
				}
			}
			foreach (var diagnostic in Diagnostics.OrderBy(item => item.Location, StringComparer.Ordinal)
			         .ThenBy(item => item.Code)) {
				Append(builder, diagnostic.Code.ToString());
				Append(builder, diagnostic.Severity.ToString());
				Append(builder, diagnostic.Location);
				Append(builder, diagnostic.Message);
			}
			return AutomationHash.Compute(builder.ToString());
		}

		private static void Append(StringBuilder builder, string value) {
			value = value ?? string.Empty;
			builder.Append(value.Length).Append(':').Append(value).Append('|');
		}
	}

	internal static class DependencyAnalysisPlanner {
		internal static AutomationPlan Create(
			DependencyAnalysisProjectState state,
			AddressablesAutomationConfig config = null) {
			if (state == null) {
				throw new ArgumentNullException(nameof(state));
			}

			var operations = new List<AutomationOperation>();
			var diagnostics = new List<AutomationDiagnostic>(state.Diagnostics);
			var sourceHash = state.ComputeHash();
			if (!state.SettingsExist) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.AddressablesSettingsMissing,
					"Addressables",
					"Addressables settings do not exist. Analysis did not create them."));
				return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
			}

			if (!state.AdapterSupported) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.DependencyAdapterUnsupported,
					"Dependencies",
					string.IsNullOrWhiteSpace(state.AdapterDiagnostic)
						? $"Addressables {state.AdapterVersion} has no verified duplicate-dependency adapter. Fix is disabled."
						: state.AdapterDiagnostic));
			} else {
				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.DependencyAdapterVerified,
					AutomationDiagnosticSeverity.Info,
					"Dependencies",
					state.AdapterDiagnostic));
			}
			if (state.AdapterSupported && !state.AnalysisSucceeded) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.DependencyAnalysisFailed,
					"Dependencies",
					string.IsNullOrWhiteSpace(state.AdapterDiagnostic)
						? "The Addressables duplicate-dependency analyzer did not complete successfully. Fix is disabled."
						: state.AdapterDiagnostic));
			}
			if (string.IsNullOrWhiteSpace(state.DestinationGroupGuid) &&
			    string.IsNullOrWhiteSpace(state.DestinationGroupName)) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.DestinationGroupMissing,
					"Dependencies",
					"Specify a destination group name before analyzing duplicate dependencies."));
			}

			foreach (var asset in state.Assets.Where(item => item.IsExplicit)
			         .OrderBy(item => item.Guid, StringComparer.Ordinal)) {
				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.DependencyAlreadyExplicit,
					AutomationDiagnosticSeverity.Info,
					asset.Path,
					$"Already explicit in group '{asset.ExplicitGroupName}'. It is report-only and will not be moved. " +
					$"Duplicate references: {DisplayGroups(asset)}."));
			}

			var candidates = state.Assets.Where(item => !item.IsExplicit)
				.OrderBy(item => item.Guid, StringComparer.Ordinal)
				.ThenBy(item => item.Path, StringComparer.Ordinal)
				.ToArray();
			if (state.DestinationGroup?.ReadOnly == true) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.DestinationGroupReadOnly,
					"Dependencies",
					$"Destination group '{state.DestinationGroup.Name}' is read-only."));
			}
			if (state.DestinationGroup != null && state.DestinationGroup.HasBundledSchema &&
			    !state.DestinationGroup.IsBuildable) {
				diagnostics.Add(Error(
					AutomationDiagnosticCode.DestinationGroupNonBuildable,
					"Dependencies",
					$"Destination group '{state.DestinationGroup.Name}' has invalid bundled build/load paths."));
			}

			if (diagnostics.Any(item => item.Severity == AutomationDiagnosticSeverity.Error) ||
			    candidates.Length == 0) {
				return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
			}

			var groupGuid = state.DestinationGroup?.Guid ?? state.DestinationGroupGuid;
			var groupName = state.DestinationGroup?.Name ?? state.DestinationGroupName;
			if (state.DestinationGroup == null) {
				operations.Add(new AutomationOperation(
					AutomationOperationKind.CreateGroup,
					groupName: groupName));
			}
			if (state.DestinationGroup == null || !state.DestinationGroup.HasBundledSchema) {
				operations.Add(new AutomationOperation(
					AutomationOperationKind.AddBundledAssetGroupSchema,
					groupGuid: groupGuid,
					groupName: groupName));
			}
			if (state.DestinationGroup == null || !state.DestinationGroup.HasContentUpdateSchema) {
				operations.Add(new AutomationOperation(
					AutomationOperationKind.AddContentUpdateGroupSchema,
					groupGuid: groupGuid,
					groupName: groupName));
			}
			foreach (var asset in candidates) {
				operations.Add(new AutomationOperation(
					AutomationOperationKind.CreateEntry,
					asset.Guid,
					asset.Path,
					groupGuid,
					groupName,
					value: DisplayGroups(asset)));
			}

			return Build(sourceHash, operations, diagnostics, config, state.ConfigGuid);
		}

		private static string DisplayGroups(DependencyAnalysisAssetState asset) {
			var groups = asset.ReferencingGroupNames
				.Where(item => !string.IsNullOrWhiteSpace(item))
				.Distinct(StringComparer.Ordinal)
				.OrderBy(item => item, StringComparer.Ordinal)
				.ToArray();
			return groups.Length == 0 ? "unknown Addressables groups" : string.Join(", ", groups);
		}

		private static AutomationPlan Build(
			string sourceHash,
			IEnumerable<AutomationOperation> operations,
			IEnumerable<AutomationDiagnostic> diagnostics,
			AddressablesAutomationConfig config,
			string configGuid) {
			var sortedOperations = operations
				.OrderBy(OperationRank)
				.ThenBy(item => item.AssetGuid, StringComparer.Ordinal)
				.ThenBy(item => item.GroupName, StringComparer.Ordinal)
				.ToArray();
			var sortedDiagnostics = diagnostics
				.OrderByDescending(item => item.Severity)
				.ThenBy(item => item.Location, StringComparer.Ordinal)
				.ThenBy(item => item.Code)
				.ThenBy(item => item.Message, StringComparer.Ordinal)
				.ToArray();
			var text = new StringBuilder(sourceHash);
			foreach (var operation in sortedOperations) {
				text.Append('|').Append((int)operation.Kind)
					.Append('|').Append(operation.AssetGuid)
					.Append('|').Append(operation.AssetPath)
					.Append('|').Append(operation.GroupGuid)
					.Append('|').Append(operation.GroupName)
					.Append('|').Append(operation.Value);
			}
			return new AutomationPlan(
				AutomationScope.Dependencies,
				sourceHash,
				AutomationHash.Compute(text.ToString()),
				sortedOperations,
				sortedDiagnostics,
				config,
				configGuid: configGuid);
		}

		private static int OperationRank(AutomationOperation operation) {
			switch (operation.Kind) {
				case AutomationOperationKind.CreateGroup: return 0;
				case AutomationOperationKind.AddBundledAssetGroupSchema: return 1;
				case AutomationOperationKind.AddContentUpdateGroupSchema: return 2;
				default: return 10;
			}
		}

		private static AutomationDiagnostic Error(
			AutomationDiagnosticCode code,
			string location,
			string message) {
			return new AutomationDiagnostic(
				code, AutomationDiagnosticSeverity.Error, location, message);
		}
	}

	internal static class UnityDependencyAnalysisDataSource {
		internal static DependencyAnalysisProjectState Capture(
			AddressablesAutomationConfig config,
			IEnumerable<AutomationDiagnostic> initialDiagnostics = null,
			IDuplicateDependencyAdapter adapter = null) {
			var state = new DependencyAnalysisProjectState();
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
			var dependencySettings = config.SerializedDependencySettings;
			if (dependencySettings != null) {
				state.DestinationGroupGuid = dependencySettings.DestinationGroupGuid ?? string.Empty;
				state.DestinationGroupName = dependencySettings.DestinationGroupName ?? string.Empty;
			}

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
			state.DestinationGroup = CaptureDestinationGroup(settings, state);

			adapter = adapter ?? new AddressablesDuplicateDependencyAdapter();
			state.AdapterVersion = adapter.Version;
			state.AdapterSupported = adapter.IsVerified;
			state.AdapterDiagnostic = adapter.CapabilityDiagnostic;
			if (!state.AdapterSupported || state.Diagnostics.Any(item =>
				    item.Severity == AutomationDiagnosticSeverity.Error)) {
				return state;
			}

			var result = adapter.Analyze(settings);
			state.AdapterVersion = result.Version;
			state.AdapterSupported = result.Supported;
			state.AnalysisSucceeded = result.Succeeded;
			state.AdapterDiagnostic = result.Diagnostic;
			if (!result.Succeeded) {
				return state;
			}

			foreach (var group in result.Occurrences.GroupBy(item => item.AssetGuid, StringComparer.Ordinal)
			         .OrderBy(item => item.Key, StringComparer.Ordinal)) {
				var occurrence = group.First();
				var entry = settings.FindAssetEntry(group.Key, false);
				state.Assets.Add(new DependencyAnalysisAssetState {
					Guid = group.Key,
					Path = string.IsNullOrEmpty(occurrence.AssetPath)
						? AssetDatabase.GUIDToAssetPath(group.Key)
						: occurrence.AssetPath,
					IsExplicit = entry != null,
					ExplicitGroupGuid = entry?.parentGroup?.Guid ?? string.Empty,
					ExplicitGroupName = entry?.parentGroup?.Name ?? string.Empty,
					ReferencingGroupGuids = group.Select(item => item.ReferencingGroupGuid)
						.Where(item => !string.IsNullOrEmpty(item))
						.Distinct(StringComparer.Ordinal)
						.OrderBy(item => item, StringComparer.Ordinal)
						.ToArray(),
					ReferencingGroupNames = group.Select(item => item.ReferencingGroupName)
						.Where(item => !string.IsNullOrEmpty(item))
						.Distinct(StringComparer.Ordinal)
						.OrderBy(item => item, StringComparer.Ordinal)
						.ToArray()
				});
			}
			return state;
		}

		private static GroupSyncGroupState CaptureDestinationGroup(
			AddressableAssetSettings settings,
			DependencyAnalysisProjectState state) {
			var group = !string.IsNullOrEmpty(state.DestinationGroupGuid)
				? settings.groups.FirstOrDefault(item => item != null &&
					string.Equals(item.Guid, state.DestinationGroupGuid, StringComparison.Ordinal))
				: null;
			if (group == null && !string.IsNullOrWhiteSpace(state.DestinationGroupName)) {
				group = settings.FindGroup(state.DestinationGroupName);
			}
			if (group == null) {
				return null;
			}

			var bundled = group.GetSchema<BundledAssetGroupSchema>();
			return new GroupSyncGroupState {
				Guid = group.Guid ?? string.Empty,
				Name = group.Name ?? string.Empty,
				ReadOnly = group.ReadOnly,
				HasBundledSchema = bundled != null,
				HasContentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>() != null,
				IsBuildable = bundled == null || UnityGroupSyncDataSource.HasBuildAndLoadPaths(settings, bundled)
			};
		}
	}

	internal static class DependencyResolverController {
		internal static AutomationPlan Analyze(AddressablesAutomationConfig config) {
			var diagnostics = new List<AutomationDiagnostic>();
			var candidateError = "Select an Addressables Automation configuration asset.";
			if (config == null ||
			    !AddressablesAutomationContextProvider.TryValidateConfigCandidate(config, out candidateError)) {
				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.ConfigurationInvalid,
					AutomationDiagnosticSeverity.Error,
					"Configuration",
					candidateError));
			} else {
				diagnostics.AddRange(ConvertDiagnostics(
					AddressablesAutomationValidator.Validate(config, AutomationScope.Dependencies)));
			}
			if (GroupSyncRecovery.TryFindPending(out var recoveryPath)) {
				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.RecoveryRequired,
					AutomationDiagnosticSeverity.Error,
					"Recovery",
					$"Recover '{recoveryPath}' before analyzing dependency fixes."));
			}
			return DependencyAnalysisPlanner.Create(
				UnityDependencyAnalysisDataSource.Capture(config, diagnostics), config);
		}

		internal static AutomationReport Fix(AutomationPlan plan, bool explicitlyConfirmed) {
			if (!explicitlyConfirmed) {
				return Failure(
					AutomationDiagnosticCode.DependencyFixConfirmationRequired,
					"Dependencies",
					"Dependency Fix requires a separate explicit confirmation after reviewing an analyze-only plan.");
			}
			if (plan == null || plan.Scope != AutomationScope.Dependencies) {
				return Failure(
					AutomationDiagnosticCode.InvalidScope,
					"Dependencies",
					"Fix requires a dependency plan produced by the analyze-only action.");
			}
			if (!plan.IsValid) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(), plan.Diagnostics,
					new[] { "The dependency plan has blocking diagnostics and was not fixed." },
					AutomationRollbackStatus.NotRequired, string.Empty);
			}
			if (GroupSyncRecovery.TryFindPending(out var recoveryPath)) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(),
					new[] { new AutomationDiagnostic(
						AutomationDiagnosticCode.RecoveryRequired,
						AutomationDiagnosticSeverity.Error,
						"Recovery",
						$"Recover '{recoveryPath}' before fixing dependencies.") },
					new[] { "A prior recovery snapshot is pending." },
					AutomationRollbackStatus.NotRequired, recoveryPath);
			}

			var config = plan.Config;
			if (config == null && !string.IsNullOrEmpty(plan.ConfigGuid)) {
				var path = AssetDatabase.GUIDToAssetPath(plan.ConfigGuid);
				config = string.IsNullOrEmpty(path)
					? null
					: AssetDatabase.LoadAssetAtPath<AddressablesAutomationConfig>(path);
				plan.BindConfig(config);
			}
			if (config == null) {
				return Failure(
					AutomationDiagnosticCode.ConfigurationInvalid,
					"Configuration",
					"The analyzed configuration no longer resolves by GUID. No changes were made.");
			}

			var current = Analyze(config);
			if (!current.IsValid ||
			    !string.Equals(current.SourceHash, plan.SourceHash, StringComparison.Ordinal) ||
			    !string.Equals(current.PlanHash, plan.PlanHash, StringComparison.Ordinal)) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(),
					current.Diagnostics.Concat(new[] { new AutomationDiagnostic(
						AutomationDiagnosticCode.StalePlan,
						AutomationDiagnosticSeverity.Error,
						"Dependencies",
						"Addressables state or analyzer results changed after preview. Analyze again before Fix.") }),
					new[] { "The dependency plan is stale." },
					AutomationRollbackStatus.NotRequired, string.Empty);
			}
			if (!plan.HasChanges) {
				return new AutomationReport(
					true, Array.Empty<AutomationOperation>(), plan.Diagnostics,
					Array.Empty<string>(), AutomationRollbackStatus.NotRequired, string.Empty);
			}
			if (!AddressableAssetSettingsDefaultObject.SettingsExists) {
				return Failure(
					AutomationDiagnosticCode.AddressablesSettingsMissing,
					"Addressables",
					"Addressables settings disappeared after analysis. No changes were made.");
			}

			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			return GroupSyncTransaction.Apply(
				plan, new UnityGroupSyncMutationBackend(settings));
		}

		private static IEnumerable<AutomationDiagnostic> ConvertDiagnostics(
			ConfigurationValidationReport validation) {
			return validation.Diagnostics.Select(item => new AutomationDiagnostic(
				item.Code == ConfigurationDiagnosticCode.AddressablesSettingsMissing
					? AutomationDiagnosticCode.AddressablesSettingsMissing
					: AutomationDiagnosticCode.ConfigurationInvalid,
				item.Severity == ConfigurationDiagnosticSeverity.Error
					? AutomationDiagnosticSeverity.Error
					: item.Severity == ConfigurationDiagnosticSeverity.Warning
						? AutomationDiagnosticSeverity.Warning
						: AutomationDiagnosticSeverity.Info,
				item.Location,
				$"[{item.Code}] {item.Message}"));
		}

		private static AutomationReport Failure(
			AutomationDiagnosticCode code,
			string location,
			string message) {
			return new AutomationReport(
				false, Array.Empty<AutomationOperation>(),
				new[] { new AutomationDiagnostic(
					code, AutomationDiagnosticSeverity.Error, location, message) },
				new[] { message }, AutomationRollbackStatus.NotRequired, string.Empty);
		}
	}
}
