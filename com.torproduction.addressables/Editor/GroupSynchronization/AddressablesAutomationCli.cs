using System;
using System.Linq;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	public static class AddressablesAutomationCli {
		public static void AnalyzeGroups() {
			var plan = AddressablesAutomation.AnalyzeActiveGroups();
			Debug.Log(FormatPlan(plan));
			if (!plan.IsValid) {
				throw new InvalidOperationException("Addressables group analysis found blocking diagnostics.");
			}
		}

		public static void ApplyGroups() {
			var plan = AddressablesAutomation.AnalyzeActiveGroups();
			Debug.Log(FormatPlan(plan));
			if (!plan.IsValid) {
				throw new InvalidOperationException("Addressables group analysis found blocking diagnostics; Apply was not started.");
			}
			var report = AddressablesAutomation.Apply(plan);
			Debug.Log(FormatReport(report));
			if (!report.Succeeded) {
				throw new InvalidOperationException(
					"Addressables group Apply failed: " + string.Join(" | ", report.Failures));
			}
		}

		public static void RecoverGroups() {
			var report = AddressablesAutomation.Recover();
			Debug.Log(FormatReport(report));
			if (!report.Succeeded) {
				throw new InvalidOperationException(
					"Addressables group recovery failed: " + string.Join(" | ", report.Failures));
			}
		}

		internal static string FormatPlan(AutomationPlan plan) {
			return JsonUtility.ToJson(new CliPlan {
				valid = plan.IsValid,
				hasChanges = plan.HasChanges,
				sourceHash = plan.SourceHash,
				planHash = plan.PlanHash,
				operations = plan.Operations.Select(item => item.Description).ToArray(),
				diagnostics = plan.Diagnostics.Select(item =>
					$"{item.Severity}:{item.Code}:{item.Location}:{item.Message}").ToArray()
			}, true);
		}

		internal static string FormatReport(AutomationReport report) {
			return JsonUtility.ToJson(new CliReport {
				succeeded = report.Succeeded,
				rollback = report.RollbackStatus.ToString(),
				recoveryPath = report.RecoveryPath,
				operations = report.Operations.Select(item => item.Description).ToArray(),
				failures = report.Failures.ToArray()
			}, true);
		}

		[Serializable]
		private sealed class CliPlan {
			public bool valid;
			public bool hasChanges;
			public string sourceHash;
			public string planHash;
			public string[] operations;
			public string[] diagnostics;
		}

		[Serializable]
		private sealed class CliReport {
			public bool succeeded;
			public string rollback;
			public string recoveryPath;
			public string[] operations;
			public string[] failures;
		}
	}
}
