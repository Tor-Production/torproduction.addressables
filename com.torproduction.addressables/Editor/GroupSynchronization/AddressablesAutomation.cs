using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;

namespace TorProduction.Addressables.Editor {
	public static class AddressablesAutomation {
		public static AutomationPlan Analyze(
			AddressablesAutomationConfig config,
			AutomationScope scope) {
			if (scope != AutomationScope.Groups) {
				return InvalidPlan(
					AutomationDiagnosticCode.InvalidScope,
					"Scope",
					"Phase 2 supports the Groups scope only. Scene and dependency scopes remain disabled until their planned phases.",
					config);
			}
			if (config == null) {
				return InvalidPlan(
					AutomationDiagnosticCode.ConfigurationInvalid,
					"Configuration",
					"Select an Addressables Automation configuration asset.",
					config);
			}
			if (!AddressablesAutomationContextProvider.TryValidateConfigCandidate(
				    config, out var candidateError)) {
				return InvalidPlan(
					AutomationDiagnosticCode.ConfigurationInvalid,
					"Configuration",
					candidateError,
					config);
			}

			var diagnostics = ConvertDiagnostics(
				AddressablesAutomationValidator.Validate(config, AutomationScope.Groups));
			if (GroupSyncRecovery.TryFindPending(out var recoveryPath)) {
				diagnostics.Add(new AutomationDiagnostic(
					AutomationDiagnosticCode.RecoveryRequired,
					AutomationDiagnosticSeverity.Error,
					"Recovery",
					$"A prior group Apply did not finish cleanly. Recover '{recoveryPath}' before analyzing another Apply."));
			}

			var state = UnityGroupSyncDataSource.Capture(config, diagnostics);
			return GroupSyncPlanner.Create(state, config);
		}

		public static AutomationReport Apply(AutomationPlan plan) {
			if (plan == null) {
				return Failure(
					AutomationDiagnosticCode.ConfigurationInvalid,
					"Apply",
					"A non-null analyzed plan is required.");
			}
			if (plan.Scope != AutomationScope.Groups || plan.Config == null) {
				return Failure(
					AutomationDiagnosticCode.InvalidScope,
					"Apply",
					"Only a Groups plan produced by AddressablesAutomation.Analyze can be applied.");
			}
			if (!plan.IsValid) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(), plan.Diagnostics,
					new[] { "The plan has blocking diagnostics and was not applied." },
					AutomationRollbackStatus.NotRequired, string.Empty);
			}
			if (GroupSyncRecovery.TryFindPending(out var recoveryPath)) {
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(),
					new[] { new AutomationDiagnostic(
						AutomationDiagnosticCode.RecoveryRequired,
						AutomationDiagnosticSeverity.Error,
						"Recovery",
						$"Recover '{recoveryPath}' before applying another plan.") },
					new[] { "A prior recovery snapshot is pending." },
					AutomationRollbackStatus.NotRequired, recoveryPath);
			}

			var current = Analyze(plan.Config, AutomationScope.Groups);
			if (!current.IsValid ||
			    !string.Equals(current.SourceHash, plan.SourceHash, StringComparison.Ordinal) ||
			    !string.Equals(current.PlanHash, plan.PlanHash, StringComparison.Ordinal)) {
				var diagnostics = new List<AutomationDiagnostic>(current.Diagnostics) {
					new AutomationDiagnostic(
						AutomationDiagnosticCode.StalePlan,
						AutomationDiagnosticSeverity.Error,
						"Apply",
						"The configuration, source assets, or Addressables state changed after preview. Re-analyze before Apply.")
				};
				return new AutomationReport(
					false, Array.Empty<AutomationOperation>(), diagnostics,
					new[] { "The analyzed plan is stale." },
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

		public static AutomationReport Recover() {
			return GroupSyncRecovery.Recover();
		}

		internal static AutomationPlan AnalyzeActiveGroups() {
			var resolution = AddressablesAutomationContextProvider.ResolveManual(AutomationScope.Groups);
			return resolution.IsReady
				? Analyze(resolution.Config, AutomationScope.Groups)
				: InvalidPlan(
					ConfigurationCode(resolution),
					"Configuration",
					resolution.Message,
					resolution.Config);
		}

		private static AutomationDiagnosticCode ConfigurationCode(ConfigurationResolution resolution) {
			return resolution.Status == ConfigurationStatus.AddressablesSettingsMissing
				? AutomationDiagnosticCode.AddressablesSettingsMissing
				: AutomationDiagnosticCode.ConfigurationInvalid;
		}

		private static List<AutomationDiagnostic> ConvertDiagnostics(
			ConfigurationValidationReport validation) {
			return validation.Diagnostics.Select(item => new AutomationDiagnostic(
				item.Code == ConfigurationDiagnosticCode.AddressablesSettingsMissing
					? AutomationDiagnosticCode.AddressablesSettingsMissing
					: item.Code == ConfigurationDiagnosticCode.TypeFilterUnresolved ||
					  item.Code == ConfigurationDiagnosticCode.TypeFilterNotAssemblyQualified
						? AutomationDiagnosticCode.TypeFilterUnresolved
						: AutomationDiagnosticCode.ConfigurationInvalid,
				item.Severity == ConfigurationDiagnosticSeverity.Error
					? AutomationDiagnosticSeverity.Error
					: item.Severity == ConfigurationDiagnosticSeverity.Warning
						? AutomationDiagnosticSeverity.Warning
						: AutomationDiagnosticSeverity.Info,
				item.Location,
				$"[{item.Code}] {item.Message}"))
				.ToList();
		}

		private static AutomationPlan InvalidPlan(
			AutomationDiagnosticCode code,
			string location,
			string message,
			AddressablesAutomationConfig config) {
			var diagnostic = new AutomationDiagnostic(
				code, AutomationDiagnosticSeverity.Error, location, message);
			var sourceHash = AutomationHash.Compute($"{code}|{location}|{message}");
			return new AutomationPlan(
				AutomationScope.Groups, sourceHash, sourceHash,
				Array.Empty<AutomationOperation>(), new[] { diagnostic }, config);
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
