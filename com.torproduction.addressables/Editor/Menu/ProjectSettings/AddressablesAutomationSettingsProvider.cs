using System;
using System.Collections.Generic;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal sealed class AddressablesAutomationSettingsProvider : SettingsProvider {
		internal const string SettingsPath = "Project/Tor Production/Addressables Automation";
		private const string DefaultFolder = "Assets/Editor/TorProduction";
		private const string DefaultPath = DefaultFolder + "/AddressablesAutomationConfig.asset";

		private AddressablesAutomationConfig m_pendingConfig;
		private ConfigurationResolution m_resolution;
		private ConfigurationValidationReport m_analysis;
		private bool m_pendingAutomaticScenes;
		private bool m_loaded;
		private string m_message = string.Empty;
		private MessageType m_messageType = MessageType.None;

		internal AddressablesAutomationSettingsProvider() :
			base(SettingsPath, SettingsScope.Project, new HashSet<string> {
				"Addressables", "Automation", "Configuration", "Scenes", "Tor Production"
			}) {
			label = "Addressables Automation";
		}

		[SettingsProvider]
		internal static SettingsProvider CreateProvider() {
			return new AddressablesAutomationSettingsProvider();
		}

		[MenuItem("Tools/Tor Production/Addressables Automation Settings", priority = 300)]
		private static void Open() {
			SettingsService.OpenProjectSettings(SettingsPath);
		}

		public override void OnGUI(string searchContext) {
			if (!m_loaded) {
				Reload();
			}

			EditorGUILayout.LabelField("Addressables Automation", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Reads are inert. Only the explicit actions below save project state or create a configuration asset. " +
				"All content-changing workflows remain disabled until their planned phases.",
				MessageType.Info);

			DrawSavedState();
			EditorGUILayout.Space();
			DrawConfiguration();
			EditorGUILayout.Space();
			DrawAnalysis();
			EditorGUILayout.Space();
			DrawAutomation();
			EditorGUILayout.Space();
			DrawLifecycle();

			if (!string.IsNullOrEmpty(m_message)) {
				EditorGUILayout.HelpBox(m_message, m_messageType);
			}
		}

		private void DrawSavedState() {
			EditorGUILayout.LabelField("Saved project state", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Status", m_resolution.Status.ToString());
			EditorGUILayout.LabelField(
				"Selected asset",
				string.IsNullOrEmpty(m_resolution.ConfigPath) ? "None" : m_resolution.ConfigPath);
			if (!string.IsNullOrEmpty(m_resolution.Message)) {
				EditorGUILayout.HelpBox(m_resolution.Message, MessageType.Warning);
			}
		}

		private void DrawConfiguration() {
			EditorGUILayout.LabelField("Configuration asset", EditorStyles.boldLabel);
			m_pendingConfig = (AddressablesAutomationConfig)EditorGUILayout.ObjectField(
				"Pending selection", m_pendingConfig, typeof(AddressablesAutomationConfig), false);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Create at Default Path...")) {
				CreateConfig();
			}
			EditorGUI.BeginDisabledGroup(m_pendingConfig == null);
			if (GUILayout.Button("Select")) {
				SelectPending();
			}
			if (GUILayout.Button("Show in Project")) {
				Selection.activeObject = m_pendingConfig;
				EditorGUIUtility.PingObject(m_pendingConfig);
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.HelpBox(
				"Changing the object field does not save. Select stores only its GUID and disables prior automatic opt-in when the selected GUID changes.",
				MessageType.None);
		}

		private void DrawAnalysis() {
			EditorGUILayout.LabelField("Analysis", EditorStyles.boldLabel);
			EditorGUI.BeginDisabledGroup(m_pendingConfig == null && m_resolution.Config == null);
			if (GUILayout.Button("Analyze (No Changes)")) {
				var config = m_pendingConfig != null ? m_pendingConfig : m_resolution.Config;
				m_analysis = AddressablesAutomationValidator.Validate(config, AutomationScope.All);
				SetMessage(
					m_analysis.IsValid ? "Analysis completed without blocking diagnostics." : "Analysis found blocking diagnostics.",
					m_analysis.IsValid ? MessageType.Info : MessageType.Warning);
			}
			EditorGUI.EndDisabledGroup();

			var report = m_analysis ?? m_resolution.Validation;
			if (report == null) {
				return;
			}

			foreach (var diagnostic in report.Diagnostics) {
				var type = diagnostic.Severity == ConfigurationDiagnosticSeverity.Error
					? MessageType.Error
					: diagnostic.Severity == ConfigurationDiagnosticSeverity.Warning
						? MessageType.Warning
						: MessageType.Info;
				EditorGUILayout.HelpBox(
					$"[{diagnostic.Code}] {diagnostic.Location}: {diagnostic.Message}", type);
			}
		}

		private void DrawAutomation() {
			EditorGUILayout.LabelField("Automatic scene processing", EditorStyles.boldLabel);
			m_pendingAutomaticScenes = EditorGUILayout.Toggle(
				"Enable postprocessing", m_pendingAutomaticScenes);
			EditorGUILayout.HelpBox(
				"This pending toggle is saved only by Apply. Scene reconciliation remains gated until Phase 3; automatic group and dependency processing is unsupported.",
				MessageType.Info);
			if (GUILayout.Button("Apply Automatic-Scene Setting")) {
				if (AddressablesAutomationContextProvider.TryApplyAutomaticSceneProcessing(
					    m_pendingAutomaticScenes, out var error)) {
					SetMessage("Automatic-scene setting saved.", MessageType.Info);
					Reload();
				} else {
					SetMessage(error, MessageType.Error);
				}
			}
		}

		private void DrawLifecycle() {
			EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Reload")) {
				Reload();
			}
			if (GUILayout.Button("Open Addressables Groups...")) {
				OpenAddressablesGroups();
			}
			EditorGUI.BeginDisabledGroup(m_resolution.Status == ConfigurationStatus.NotConfigured);
			if (GUILayout.Button("Detach...")) {
				Detach();
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();

			if (m_resolution.Status == ConfigurationStatus.CorruptProjectState ||
			    m_resolution.Status == ConfigurationStatus.ProjectStateMigrationRequired ||
			    m_resolution.Status == ConfigurationStatus.UnsupportedProjectStateSchema) {
				if (GUILayout.Button("Back Up and Reset Project State...")) {
					Recover();
				}
			}
		}

		private void CreateConfig() {
			if (!EditorUtility.DisplayDialog(
				    "Create Addressables Automation Configuration",
				    $"Create a new configuration below '{DefaultFolder}'? Existing assets will not be overwritten.",
				    "Create", "Cancel")) {
				return;
			}

			try {
				EnsureFolder(DefaultFolder);
				var path = AssetDatabase.GenerateUniqueAssetPath(DefaultPath);
				var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
				AssetDatabase.CreateAsset(config, path);
				AssetDatabase.SaveAssets();
				m_pendingConfig = config;
				if (AddressablesAutomationContextProvider.TrySelectConfig(
					    AssetDatabase.AssetPathToGUID(path), out var error)) {
					SetMessage($"Created and selected '{path}'.", MessageType.Info);
					Reload();
				} else {
					SetMessage($"Created '{path}', but selection failed: {error}. The asset was retained.", MessageType.Error);
				}
			} catch (Exception exception) {
				SetMessage($"Could not create the configuration: {exception.Message}", MessageType.Error);
			}
		}

		private void SelectPending() {
			var path = AssetDatabase.GetAssetPath(m_pendingConfig);
			if (AddressablesAutomationContextProvider.TrySelectConfig(
				    AssetDatabase.AssetPathToGUID(path), out var error)) {
				SetMessage($"Selected '{path}'.", MessageType.Info);
				Reload();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void Detach() {
			if (!EditorUtility.DisplayDialog(
				    "Detach Addressables Automation",
				    "Clear the config GUID and automatic opt-in? No assets or legacy files will be deleted.",
				    "Detach", "Cancel")) {
				return;
			}
			if (AddressablesAutomationProjectSettingsStore.TryDetach(out var error)) {
				m_pendingConfig = null;
				SetMessage("Configuration detached. Assets were unchanged.", MessageType.Info);
				Reload();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void Recover() {
			if (!EditorUtility.DisplayDialog(
				    "Reset Addressables Automation Project State",
				    "Back up the raw settings under Library, then replace them with a detached, automation-off state?",
				    "Back Up and Reset", "Cancel")) {
				return;
			}
			if (AddressablesAutomationProjectSettingsStore.TryRecover(
				    out var recoveryPath, out var error)) {
				SetMessage($"Project state reset. Backup: {recoveryPath}", MessageType.Info);
				Reload();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void OpenAddressablesGroups() {
			if (EditorUtility.DisplayDialog(
				    "Open Addressables Groups",
				    "This package will not create Addressables settings. The official window may offer a separate creation action.",
				    "Open Window", "Cancel") &&
			    !EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups")) {
				SetMessage("Unity could not open the Addressables Groups window.", MessageType.Warning);
			}
		}

		private void Reload() {
			m_resolution = AddressablesAutomationContextProvider.ResolveManual(AutomationScope.All);
			if (m_resolution.Config != null) {
				m_pendingConfig = m_resolution.Config;
			}
			m_pendingAutomaticScenes = m_resolution.ProjectSettings.AutomationEnabled &&
				(m_resolution.ProjectSettings.AutomaticScopes & AutomationScope.Scenes) != 0;
			m_analysis = null;
			m_loaded = true;
		}

		private static void EnsureFolder(string path) {
			var segments = path.Split('/');
			var parent = segments[0];
			for (var index = 1; index < segments.Length; index++) {
				var childPath = parent + "/" + segments[index];
				if (!AssetDatabase.IsValidFolder(childPath) &&
				    string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, segments[index]))) {
					throw new InvalidOperationException($"Could not create asset folder '{childPath}'.");
				}
				parent = childPath;
			}
		}

		private void SetMessage(string message, MessageType type) {
			m_message = message ?? string.Empty;
			m_messageType = type;
		}
	}
}
