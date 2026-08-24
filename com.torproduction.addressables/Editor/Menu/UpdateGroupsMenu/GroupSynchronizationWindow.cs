using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal static class GroupSynchronizationController {
		internal static AutomationPlan Analyze() => AddressablesAutomation.AnalyzeActiveGroups();
		internal static AutomationReport Apply(AutomationPlan plan) => AddressablesAutomation.Apply(plan);
		internal static AutomationReport Recover() => AddressablesAutomation.Recover();
	}

	internal sealed class GroupSynchronizationWindow : EditorWindow {
		private AutomationPlan m_plan;
		private AutomationReport m_report;
		private Vector2 m_scroll;

		internal static void ShowWindow() {
			var window = GetWindow<GroupSynchronizationWindow>("Group Synchronization");
			window.minSize = new Vector2(620f, 360f);
			window.Show();
		}

		private void OnEnable() {
			m_plan = null;
			m_report = null;
		}

		private void OnGUI() {
			EditorGUILayout.LabelField("Deterministic Group Synchronization", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Analyze is read-only. Apply uses this exact preview, rejects stale plans, writes a recovery snapshot, and rolls back on failure.",
				MessageType.Info);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Analyze Groups (No Changes)", GUILayout.Height(30f))) {
				m_plan = GroupSynchronizationController.Analyze();
				m_report = null;
			}
			EditorGUI.BeginDisabledGroup(m_plan == null || !m_plan.IsValid || !m_plan.HasChanges);
			if (GUILayout.Button("Apply Preview...", GUILayout.Height(30f))) {
				ApplyPreview();
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();

			if (GroupSyncRecovery.TryFindPending(out var recoveryPath)) {
				EditorGUILayout.HelpBox($"A recovery snapshot is pending: {recoveryPath}", MessageType.Error);
				if (GUILayout.Button("Recover Previous Group Apply...")) {
					Recover(recoveryPath);
				}
			}

			m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
			DrawPlan();
			DrawReport();
			EditorGUILayout.EndScrollView();
		}

		private void DrawPlan() {
			if (m_plan == null) {
				EditorGUILayout.HelpBox("Run Analyze to preview the deterministic operation list.", MessageType.None);
				return;
			}
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Plan hash", m_plan.PlanHash);
			EditorGUILayout.LabelField("Operations", m_plan.Operations.Count.ToString());
			if (m_plan.IsValid && !m_plan.HasChanges) {
				EditorGUILayout.HelpBox("The configured groups already converge. Apply is unnecessary.", MessageType.Info);
			}
			foreach (var diagnostic in m_plan.Diagnostics) {
				EditorGUILayout.HelpBox(
					$"[{diagnostic.Code}] {diagnostic.Location}: {diagnostic.Message}",
					ToMessageType(diagnostic.Severity));
			}
			for (var index = 0; index < m_plan.Operations.Count; index++) {
				EditorGUILayout.LabelField($"{index + 1}. {m_plan.Operations[index].Description}", EditorStyles.wordWrappedLabel);
			}
		}

		private void DrawReport() {
			if (m_report == null) {
				return;
			}
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Last Apply / Recovery", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				m_report.Succeeded
					? $"Succeeded. Applied {m_report.Operations.Count} operations."
					: $"Failed. Rollback: {m_report.RollbackStatus}.",
				m_report.Succeeded ? MessageType.Info : MessageType.Error);
			foreach (var failure in m_report.Failures) {
				EditorGUILayout.HelpBox(failure, MessageType.Error);
			}
			if (!string.IsNullOrEmpty(m_report.RecoveryPath)) {
				EditorGUILayout.SelectableLabel(m_report.RecoveryPath, GUILayout.Height(18f));
			}
		}

		private void ApplyPreview() {
			if (!EditorUtility.DisplayDialog(
				    "Apply Addressables Group Plan",
				    $"Apply the {m_plan.Operations.Count} operations shown in preview? A recovery snapshot is written before the first change.",
				    "Apply Preview", "Cancel")) {
				return;
			}
			m_report = GroupSynchronizationController.Apply(m_plan);
			if (m_report.Succeeded) {
				m_plan = GroupSynchronizationController.Analyze();
			}
			Repaint();
		}

		private void Recover(string recoveryPath) {
			if (!EditorUtility.DisplayDialog(
				    "Recover Addressables Group Apply",
				    $"Restore the package-owned Addressables state recorded in '{recoveryPath}'? Unrelated entries and groups are not removed.",
				    "Recover", "Cancel")) {
				return;
			}
			m_report = GroupSynchronizationController.Recover();
			m_plan = null;
			Repaint();
		}

		private static MessageType ToMessageType(AutomationDiagnosticSeverity severity) {
			return severity == AutomationDiagnosticSeverity.Error
				? MessageType.Error
				: severity == AutomationDiagnosticSeverity.Warning
					? MessageType.Warning
					: MessageType.Info;
		}
	}
}
