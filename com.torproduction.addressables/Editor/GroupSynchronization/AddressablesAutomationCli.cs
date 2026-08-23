using System;
using System.Linq;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	public static class AddressablesAutomationCli {
		public static void AnalyzeGroups() {
			RunAnalyzeGroups(AddressablesAutomation.AnalyzeActiveGroups, Debug.Log);
		}

		internal static void RunAnalyzeGroups(
			Func<AutomationPlan> analyze,
			Action<string> writeOutput) {
			var plan = analyze();
			writeOutput(FormatPlan(plan));
			if (!plan.IsValid) {
				throw new InvalidOperationException("Addressables group analysis found blocking diagnostics.");
			}
		}

		public static void ApplyGroups() {
			RunApplyGroups(
				AddressablesAutomation.AnalyzeActiveGroups,
				AddressablesAutomation.Apply,
				Debug.Log);
		}

		internal static void RunApplyGroups(
			Func<AutomationPlan> analyze,
			Func<AutomationPlan, AutomationReport> apply,
			Action<string> writeOutput) {
			var plan = analyze();
			writeOutput(FormatPlan(plan));
			if (!plan.IsValid) {
				throw new InvalidOperationException("Addressables group analysis found blocking diagnostics; Apply was not started.");
			}
			var report = apply(plan);
			writeOutput(FormatReport(report));
			if (!report.Succeeded) {
				throw new InvalidOperationException(
					"Addressables group Apply failed: " + string.Join(" | ", report.Failures));
			}
		}

		public static void RecoverGroups() {
			RunRecoverGroups(AddressablesAutomation.Recover, Debug.Log);
		}

		public static void AnalyzeScenes() {
			RunAnalyzeScenes(AddressablesAutomation.AnalyzeActiveScenes, Debug.Log);
		}

		internal static void RunAnalyzeScenes(Func<AutomationPlan> analyze, Action<string> writeOutput) {
			var plan = analyze();
			writeOutput(FormatPlan(plan));
			if (!plan.IsValid) throw new InvalidOperationException("Addressables scene analysis found blocking diagnostics.");
		}

		public static void ApplyScenes() {
			RunApplyScenes(AddressablesAutomation.AnalyzeActiveScenes, AddressablesAutomation.Apply, Debug.Log);
		}

		internal static void RunApplyScenes(
			Func<AutomationPlan> analyze,
			Func<AutomationPlan, AutomationReport> apply,
			Action<string> writeOutput) {
			var plan = analyze();
			writeOutput(FormatPlan(plan));
			if (!plan.IsValid) throw new InvalidOperationException("Addressables scene analysis found blocking diagnostics; Apply was not started.");
			var report = apply(plan);
			writeOutput(FormatReport(report));
			if (!report.Succeeded) throw new InvalidOperationException("Addressables scene Apply failed: " + string.Join(" | ", report.Failures));
		}

		public static void RecoverScenes() {
			RunRecoverScenes(AddressablesAutomation.Recover, Debug.Log);
		}

		internal static void RunRecoverScenes(Func<AutomationReport> recover, Action<string> writeOutput) {
			var report = recover();
			writeOutput(FormatReport(report));
			if (!report.Succeeded) throw new InvalidOperationException("Addressables scene recovery failed: " + string.Join(" | ", report.Failures));
		}

		internal static void RunRecoverGroups(
			Func<AutomationReport> recover,
			Action<string> writeOutput) {
			var report = recover();
			writeOutput(FormatReport(report));
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
