using System.Linq;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class BuildMenu {
		private const string Root = "Tools/Tor Production/Addressables/Build/";

		[MenuItem(Root + "Analyze / Preflight...")]
		private static void Analyze() => BuildWorkflowWindow.Open(ContentBuildKind.Full);

		[MenuItem(Root + "Start Full...")]
		private static void StartFull() => BuildWorkflowWindow.Open(ContentBuildKind.Full);

		[MenuItem(Root + "Start Content Update...")]
		private static void StartContentUpdate() => BuildWorkflowWindow.Open(ContentBuildKind.ContentUpdate);

		[MenuItem(Root + "Start Editor-Compatible...")]
		private static void StartEditorCompatible() => BuildWorkflowWindow.Open(ContentBuildKind.EditorCompatible);

		[MenuItem(Root + "Start Multi-Platform...")]
		internal static void BuildAllButtonClick() => BuildWorkflowWindow.Open(ContentBuildKind.MultiPlatform);

		[MenuItem(Root + "Recovery/Resume")]
		private static void Resume() {
			var recovery = BuildController.InspectRecovery();
			if (!recovery.Exists) {
				EditorUtility.DisplayDialog("Addressables Build Recovery", recovery.Message, "OK");
				return;
			}
			if (!EditorUtility.DisplayDialog(
				    "Resume Addressables Build",
				    RecoverySummary(recovery) + "\n\nResume will re-run preflight and may switch/build only after this confirmation.",
				    "Resume",
				    "Cancel")) return;
			LogResult(BuildController.Resume());
		}

		[MenuItem(Root + "Recovery/Restore Original Target")]
		private static void Restore() {
			var recovery = BuildController.InspectRecovery();
			if (!recovery.Exists) {
				EditorUtility.DisplayDialog("Addressables Build Recovery", recovery.Message, "OK");
				return;
			}
			if (!EditorUtility.DisplayDialog(
				    "Restore Original Target",
				    RecoverySummary(recovery) + $"\n\nRestore exact target '{recovery.OriginalTarget}' now?",
				    "Restore Target",
				    "Cancel")) return;
			LogResult(BuildController.Restore());
		}

		[MenuItem(Root + "Recovery/Cancel Current Job")]
		private static void Cancel() {
			var recovery = BuildController.InspectRecovery();
			if (!recovery.Exists) {
				EditorUtility.DisplayDialog("Addressables Build Recovery", recovery.Message, "OK");
				return;
			}
			if (!EditorUtility.DisplayDialog(
			    "Cancel Addressables Build",
			    RecoverySummary(recovery) + "\n\nCancel before the next synchronous build stage and restore the original target?",
			    "Cancel Job and Restore",
			    "Keep Job")) return;
			LogResult(BuildController.Cancel());
		}

		[MenuItem(Root + "Recovery/Abandon or Reset Job")]
		private static void AbandonReset() {
			var recovery = BuildController.InspectRecovery();
			var message = recovery.Exists
				? RecoverySummary(recovery) +
				  "\n\nThe current recovery file will be archived. No project settings or Addressables data will be cleared. " +
				  "If the active target differs from the original target, the archive retains that incomplete-restoration warning."
				: "No current package-owned job exists. This clears only the three legacy SessionState keys owned by this package.";
			if (!EditorUtility.DisplayDialog("Abandon / Reset Addressables Build Job", message, "Abandon / Reset", "Cancel")) return;
			LogResult(BuildController.AbandonReset());
		}

		[MenuItem(Root + "Existing Build/Validate Receipt")]
		private static void ValidateExistingBuild() => LogValidation(BuildController.ValidateExistingBuild());

		[MenuItem(Root + "Existing Build/Validate and Select Use Existing Build...")]
		private static void SelectExistingBuild() {
			var validation = BuildController.ValidateExistingBuild();
			if (!validation.IsValid) {
				LogValidation(validation);
				return;
			}
			if (!EditorUtility.DisplayDialog(
				    "Select Addressables Use Existing Build",
				    $"Receipt '{validation.ReceiptPath}' is fresh and compatible with exact target '{validation.Target}'.\n\n" +
				    "Select Addressables' built-in Use Existing Build (requires built groups) Play Mode data builder?",
				    "Select Built-In Builder",
				    "Cancel")) return;
			LogValidation(BuildController.SelectExistingBuild(true));
		}

		internal static string RecoverySummary(ContentBuildRecoveryInfo recovery) =>
			$"Job: {recovery.JobId}\nStage: {recovery.Stage}\nStale: {recovery.IsStale}\n" +
			$"Original target: {recovery.OriginalTarget}\nActive target: {recovery.ActiveTarget}\n" +
			$"Pending: {string.Join(", ", recovery.PendingTargets)}\nState: {recovery.StatePath}\n\n{recovery.Message}";

		internal static void LogResult(ContentBuildResult result) {
			var details = $"Addressables build job '{result.JobId}': {result.Status}. {result.Message}\n" +
			              $"Report: {result.ReportPath}\nRecovery: {result.RecoveryPath}";
			if (result.Status == ContentBuildStatus.FatalFailure ||
			    result.Status == ContentBuildStatus.TargetSwitchFailure ||
			    result.Status == ContentBuildStatus.RestorationFailure) Debug.LogError(details);
			else if (result.Status == ContentBuildStatus.Warning || result.Status == ContentBuildStatus.Cancellation) Debug.LogWarning(details);
			else Debug.Log(details);
		}

		internal static void LogValidation(ExistingBuildValidation validation) {
			var details = string.Join("\n", validation.Diagnostics.Select(item =>
				$"[{item.Severity}] {item.Code}: {item.Message}"));
			if (validation.IsValid) Debug.Log($"Existing-build receipt validation passed.\n{details}");
			else Debug.LogError($"Existing-build receipt validation failed.\n{details}");
		}
	}

	internal sealed class BuildWorkflowWindow : EditorWindow {
		private readonly BuildMenuSelection m_selection = new BuildMenuSelection();
		private ContentBuildPreflight m_preflight;
		private Vector2 m_scroll;

		internal static void Open(ContentBuildKind kind) {
			var window = GetWindow<BuildWorkflowWindow>(true, "Addressables Build Pipeline");
			window.minSize = new Vector2(620f, 560f);
			window.m_selection.Kind = kind;
			window.m_preflight = null;
			window.Show();
		}

		internal static void OpenRecovery() {
			var window = GetWindow<BuildWorkflowWindow>(true, "Addressables Build Recovery");
			window.minSize = new Vector2(620f, 560f);
			window.Show();
		}

		private void OnGUI() {
			m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
			DrawRecovery();
			EditorGUILayout.Space(10f);
			EditorGUILayout.LabelField("Request and Preflight", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Analyze / Preflight is read-only. Start is enabled only for the exact preview shown below and re-runs the same validation before the first package-owned job write or target switch.",
				MessageType.Info);

			EditorGUI.BeginChangeCheck();
			m_selection.Kind = (ContentBuildKind)EditorGUILayout.EnumPopup("Build kind", m_selection.Kind);
			if (m_selection.Kind == ContentBuildKind.Full || m_selection.Kind == ContentBuildKind.ContentUpdate) {
				m_selection.Target = (ContentBuildPlatform)EditorGUILayout.EnumPopup("Exact target", m_selection.Target);
			}
			if (m_selection.Kind == ContentBuildKind.ContentUpdate) {
				EditorGUILayout.BeginHorizontal();
				m_selection.StateFilePath = EditorGUILayout.TextField("Existing state file", m_selection.StateFilePath);
				if (GUILayout.Button("Browse...", GUILayout.Width(85f))) {
					var selected = EditorUtility.OpenFilePanel("Select addressables_content_state.bin", string.Empty, "bin");
					if (!string.IsNullOrEmpty(selected)) m_selection.StateFilePath = selected;
				}
				EditorGUILayout.EndHorizontal();
			}
			if (m_selection.Kind == ContentBuildKind.EditorCompatible) {
				EditorGUILayout.HelpBox(
					"The current editor OS maps to its exact standalone target. A successful full build creates a package-owned freshness receipt. Play Mode selection is a separate confirmed action.",
					MessageType.None);
			}
			if (m_selection.Kind == ContentBuildKind.MultiPlatform) DrawMultiPlatform();
			if (EditorGUI.EndChangeCheck()) m_preflight = null;

			EditorGUILayout.Space(6f);
			if (GUILayout.Button("Analyze / Preflight (No Changes)")) {
				m_preflight = BuildController.Analyze(m_selection.CreateRequest());
			}
			DrawPreflight();

			using (new EditorGUI.DisabledScope(m_preflight == null || !m_preflight.IsValid)) {
				if (GUILayout.Button("Start Previewed Build")) {
					var targets = string.Join(", ", m_preflight.Targets);
					if (EditorUtility.DisplayDialog(
					    "Start Addressables Build",
					    $"Start {m_preflight.Request.Kind} for exact target queue [{targets}]?\n\n" +
					    "The request will be revalidated before a target switch or Addressables build begins.",
					    "Start",
					    "Cancel")) {
						BuildMenu.LogResult(BuildController.Start(m_selection.CreateRequest()));
						m_preflight = null;
					}
				}
			}

			EditorGUILayout.Space(12f);
			EditorGUILayout.LabelField("Existing-Build Play Mode", EditorStyles.boldLabel);
			if (GUILayout.Button("Validate Editor-Compatible Receipt")) {
				BuildMenu.LogValidation(BuildController.ValidateExistingBuild());
			}
			if (GUILayout.Button("Validate and Select Built-In Use Existing Build...")) {
				var validation = BuildController.ValidateExistingBuild();
				if (!validation.IsValid) BuildMenu.LogValidation(validation);
				else if (EditorUtility.DisplayDialog(
					         "Select Built-In Use Existing Build",
					         $"Receipt is compatible with '{validation.Target}'. Select the built-in Addressables Play Mode builder?",
					         "Select",
					         "Cancel")) {
					BuildMenu.LogValidation(BuildController.SelectExistingBuild(true));
				}
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawMultiPlatform() {
			EditorGUILayout.LabelField("Explicit target queue", EditorStyles.boldLabel);
			m_selection.Android = EditorGUILayout.ToggleLeft("Android", m_selection.Android);
			m_selection.iOS = EditorGUILayout.ToggleLeft("iOS", m_selection.iOS);
			m_selection.Windows = EditorGUILayout.ToggleLeft("Windows (StandaloneWindows64)", m_selection.Windows);
			m_selection.macOS = EditorGUILayout.ToggleLeft("macOS (StandaloneOSX)", m_selection.macOS);
			m_selection.Linux = EditorGUILayout.ToggleLeft("Linux (StandaloneLinux64)", m_selection.Linux);
			m_selection.ContinueOnError = EditorGUILayout.ToggleLeft(
				"Continue after a per-target switch/build failure (explicit advanced policy)",
				m_selection.ContinueOnError);
		}

		private void DrawPreflight() {
			if (m_preflight == null) return;
			EditorGUILayout.Space(6f);
			EditorGUILayout.LabelField(m_preflight.IsValid ? "Preflight passed" : "Preflight blocked", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Exact queue", string.Join(", ", m_preflight.Targets));
			EditorGUILayout.LabelField("Request hash", m_preflight.RequestHash);
			EditorGUILayout.LabelField("Settings GUID", m_preflight.SettingsGuid);
			EditorGUILayout.LabelField("Settings hash", m_preflight.SettingsHash);
			foreach (var item in m_preflight.Diagnostics) {
				var type = item.Severity == ContentBuildDiagnosticSeverity.Error
					? MessageType.Error
					: item.Severity == ContentBuildDiagnosticSeverity.Warning ? MessageType.Warning : MessageType.Info;
				EditorGUILayout.HelpBox($"{item.Code} [{item.Target}] {item.Message}", type);
			}
		}

		private void DrawRecovery() {
			var recovery = BuildController.InspectRecovery();
			EditorGUILayout.LabelField("Recovery", EditorStyles.boldLabel);
			if (!recovery.Exists) {
				EditorGUILayout.HelpBox("No package-owned build job exists. Import and startup are inert.", MessageType.None);
				return;
			}

			EditorGUILayout.HelpBox(BuildMenu.RecoverySummary(recovery), recovery.IsStale ? MessageType.Warning : MessageType.Info);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Resume")) BuildMenu.LogResult(BuildController.Resume());
			if (GUILayout.Button("Cancel Job")) BuildMenu.LogResult(BuildController.Cancel());
			if (GUILayout.Button("Restore Original Target")) BuildMenu.LogResult(BuildController.Restore());
			if (GUILayout.Button("Abandon / Reset")) {
				if (EditorUtility.DisplayDialog(
				    "Abandon / Reset Build Job",
				    "Archive this package-owned job record and clear the current recovery slot? This does not clear Addressables or project settings.",
				    "Archive and Reset",
				    "Cancel")) BuildMenu.LogResult(BuildController.AbandonReset());
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	[InitializeOnLoad]
	internal static class BuildRecoveryBootstrap {
		static BuildRecoveryBootstrap() {
			if (!Application.isBatchMode) EditorApplication.delayCall += OfferRecovery;
		}

		internal static bool ShouldOfferRecovery(ContentBuildRecoveryInfo recovery) =>
			recovery != null && recovery.Exists;

		private static void OfferRecovery() {
			var recovery = BuildController.InspectRecovery();
			if (ShouldOfferRecovery(recovery)) BuildWorkflowWindow.OpenRecovery();
		}
	}
}
