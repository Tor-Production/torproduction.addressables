using System;
using System.Collections.Generic;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal sealed class AddressablesAutomationSettingsProvider : SettingsProvider {
		internal const string SettingsPath = "Project/Tor Production/Addressables Automation";
		internal const string SetActiveConfigurationLabel = "Set Active Configuration";
		internal const string RevertPendingChangesLabel = "Revert Pending Changes";
		internal const string ActivationHelpText =
			"Changing Pending Configuration does not activate it. Set Active Configuration validates it and persists " +
			"its GUID as the active project selection. Revert Pending Changes restores pending UI state from saved project state.";
		internal const string RecoveryHelpText =
			"Recovery controls appear only when damaged or incompatible stored project state is detected.";
		private const string DefaultFolder = "Assets/Editor/TorProduction";
		private const string DefaultPath = DefaultFolder + "/AddressablesAutomationConfig.asset";

		private AddressablesAutomationConfig m_pendingConfig;
		private ConfigurationResolution m_resolution;
		private ConfigurationResolution m_sceneResolution;
		private ConfigurationValidationReport m_analysis;
		private LegacyMigrationPreview m_legacyPreview;
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
				LoadSavedProjectState();
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
			DrawLegacyMigration();
			EditorGUILayout.Space();
			DrawAutomation();
			EditorGUILayout.Space();
			DrawLifecycle();

			if (!string.IsNullOrEmpty(m_message)) {
				EditorGUILayout.HelpBox(m_message, m_messageType);
			}
		}

		private void DrawSavedState() {
			EditorGUILayout.LabelField("Active configuration", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Status", m_resolution.Status.ToString());
			EditorGUILayout.LabelField(
				"Active asset",
				string.IsNullOrEmpty(m_resolution.ConfigPath) ? "None" : m_resolution.ConfigPath);
			if (!string.IsNullOrEmpty(m_resolution.Message)) {
				EditorGUILayout.HelpBox(m_resolution.Message, MessageType.Warning);
			}
		}

		private void DrawConfiguration() {
			EditorGUILayout.LabelField("Pending configuration", EditorStyles.boldLabel);
			m_pendingConfig = (AddressablesAutomationConfig)EditorGUILayout.ObjectField(
				"Pending asset", m_pendingConfig, typeof(AddressablesAutomationConfig), false);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Create and Set Active...")) {
				CreateConfig();
			}
			EditorGUI.BeginDisabledGroup(m_pendingConfig == null);
			if (GUILayout.Button(SetActiveConfigurationLabel)) {
				SetActiveConfiguration();
			}
			if (GUILayout.Button("Show in Project")) {
				Selection.activeObject = m_pendingConfig;
				EditorGUIUtility.PingObject(m_pendingConfig);
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.HelpBox(
				ActivationHelpText,
				MessageType.None);
		}

		private void DrawAnalysis() {
			EditorGUILayout.LabelField("Analysis", EditorStyles.boldLabel);
			EditorGUI.BeginDisabledGroup(m_pendingConfig == null && m_resolution.Config == null);
			if (GUILayout.Button("Analyze (No Changes)")) {
				Analyze();
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

		private void DrawLegacyMigration() {
			EditorGUILayout.LabelField("Legacy migration", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Preview is a read-only, one-shot analysis of ProjectSettings/ProjectConfig.json and its referenced legacy assets. " +
				"It never rewrites or deletes legacy data.",
				MessageType.Info);
			if (GUILayout.Button("Preview Legacy Migration (No Changes)")) {
				m_legacyPreview = LegacyConfigurationMigration.Preview();
				SetMessage(
					m_legacyPreview.HasBlockingErrors
						? "Legacy preview completed with blocking diagnostics. No files were changed."
						: "Legacy preview completed. No files were changed.",
					m_legacyPreview.HasBlockingErrors ? MessageType.Warning : MessageType.Info);
			}

			if (m_legacyPreview == null) {
				return;
			}

			EditorGUILayout.LabelField("Mapped group rules", m_legacyPreview.GroupRules.Length.ToString());
			EditorGUILayout.LabelField("Mapped scene rules", m_legacyPreview.SceneRules.Length.ToString());
			foreach (var diagnostic in m_legacyPreview.Diagnostics) {
				var type = diagnostic.Severity == ConfigurationDiagnosticSeverity.Error
					? MessageType.Error
					: diagnostic.Severity == ConfigurationDiagnosticSeverity.Warning
						? MessageType.Warning
						: MessageType.Info;
				EditorGUILayout.HelpBox(
					$"[{diagnostic.Code}] {diagnostic.Kind}/{diagnostic.Location}: {diagnostic.Message}", type);
			}

			EditorGUI.BeginDisabledGroup(!m_legacyPreview.HasLegacyState);
			if (GUILayout.Button("Create and Set Active Migrated Configuration...")) {
				CreateMigratedConfig();
			}
			EditorGUI.EndDisabledGroup();
			if (m_legacyPreview.HasBlockingErrors) {
				EditorGUILayout.HelpBox(
					"A migrated asset may still be created to preserve intent, but it remains invalid and automation stays off until every blocking diagnostic is resolved.",
					MessageType.Warning);
			}
		}

		private void DrawAutomation() {
			EditorGUILayout.LabelField("Automatic scene processing", EditorStyles.boldLabel);
			m_pendingAutomaticScenes = EditorGUILayout.Toggle(
				"Enable postprocessing", m_pendingAutomaticScenes);
			EditorGUILayout.HelpBox(
				"This pending toggle is saved only by Apply. Scene reconciliation remains gated until Phase 3; automatic group and dependency processing is unsupported.",
				MessageType.Info);
			var canApply = CanApplyAutomaticSceneSetting(m_sceneResolution, m_pendingAutomaticScenes);
			EditorGUI.BeginDisabledGroup(!canApply);
			if (GUILayout.Button("Apply Automatic-Scene Setting")) {
				if (AddressablesAutomationContextProvider.TryApplyAutomaticSceneProcessing(
					    m_pendingAutomaticScenes, out var error)) {
					SetMessage("Automatic-scene setting saved.", MessageType.Info);
					LoadSavedProjectState();
				} else {
					SetMessage(error, MessageType.Error);
				}
			}
			EditorGUI.EndDisabledGroup();
			if (m_pendingAutomaticScenes && !m_sceneResolution.IsReady) {
				EditorGUILayout.HelpBox(m_sceneResolution.Message, MessageType.Warning);
			}
		}

		private void DrawLifecycle() {
			EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(RecoveryHelpText, MessageType.None);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(RevertPendingChangesLabel)) {
				LoadSavedProjectState();
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

			if (m_resolution.Status == ConfigurationStatus.ProjectStateMigrationRequired) {
				if (GUILayout.Button("Back Up and Migrate Project State...")) {
					MigrateProjectState();
				}
			} else if (m_resolution.Status == ConfigurationStatus.CorruptProjectState ||
			           m_resolution.Status == ConfigurationStatus.UnsupportedProjectStateSchema) {
				if (GUILayout.Button("Back Up and Reset Project State...")) {
					Recover();
				}
			}

			if (m_resolution.Status == ConfigurationStatus.ConfigMigrationRequired &&
			    m_resolution.Config != null &&
			    GUILayout.Button("Back Up and Migrate Configuration Asset...")) {
				MigrateConfiguration();
			}
		}

		private void CreateConfig() {
			if (!EditorUtility.DisplayDialog(
				    "Create Addressables Automation Configuration",
				    $"Create a new configuration below '{DefaultFolder}' and set it active? Existing assets will not be overwritten.",
				    "Create and Set Active", "Cancel")) {
				return;
			}

			CreateConfigAsset(Array.Empty<GroupSyncRule>(), Array.Empty<SceneFolderRule>());
		}

		private void CreateMigratedConfig() {
			var warning = m_legacyPreview.HasBlockingErrors
				? "The preview has blocking diagnostics. Create the asset anyway to preserve the mapped values? Automation will remain off."
				: "Create a new configuration from this preview?";
			if (!EditorUtility.DisplayDialog(
				    "Create Migrated Addressables Configuration",
				    warning + " The new asset will be set active; legacy JSON and assets will remain untouched.",
				    "Create and Set Active", "Cancel")) {
				return;
			}
			CreateConfigAsset(m_legacyPreview.GroupRules, m_legacyPreview.SceneRules);
		}

		private void CreateConfigAsset(GroupSyncRule[] groupRules, SceneFolderRule[] sceneRules) {
			try {
				EnsureFolder(DefaultFolder);
				var path = AssetDatabase.GenerateUniqueAssetPath(DefaultPath);
				var config = ScriptableObject.CreateInstance<AddressablesAutomationConfig>();
				config.ReplaceWithCurrentSchema(groupRules, sceneRules);
				AssetDatabase.CreateAsset(config, path);
				AssetDatabase.SaveAssets();
				m_pendingConfig = config;
				if (AddressablesAutomationContextProvider.TrySelectConfig(
					    AssetDatabase.AssetPathToGUID(path), out var error)) {
					SetMessage($"Created and set active '{path}'. Legacy data was unchanged.", MessageType.Info);
					LoadSavedProjectState();
				} else {
					SetMessage($"Created '{path}', but selection failed: {error}. The asset was retained.", MessageType.Error);
				}
			} catch (Exception exception) {
				SetMessage($"Could not create the configuration: {exception.Message}", MessageType.Error);
			}
		}

		private void SetActiveConfiguration() {
			if (!AddressablesAutomationContextProvider.TryValidateConfigCandidate(
				    m_pendingConfig, out var candidateError)) {
				SetMessage(candidateError, MessageType.Error);
				return;
			}

			var path = AssetDatabase.GetAssetPath(m_pendingConfig);
			if (AddressablesAutomationContextProvider.TrySelectConfig(
				    AssetDatabase.AssetPathToGUID(path), out var error)) {
				SetMessage($"Set active configuration to '{path}'.", MessageType.Info);
				LoadSavedProjectState();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void Analyze() {
			var config = m_pendingConfig != null ? m_pendingConfig : m_resolution.Config;
			if (!AddressablesAutomationContextProvider.TryValidateConfigCandidate(config, out var candidateError)) {
				m_analysis = null;
				SetMessage(candidateError, MessageType.Error);
				return;
			}

			m_analysis = AddressablesAutomationValidator.Validate(config, AutomationScope.All);
			SetMessage(
				m_analysis.IsValid ? "Analysis completed without blocking diagnostics." : "Analysis found blocking diagnostics.",
				m_analysis.IsValid ? MessageType.Info : MessageType.Warning);
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
				LoadSavedProjectState();
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
				LoadSavedProjectState();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void MigrateProjectState() {
			if (!EditorUtility.DisplayDialog(
				    "Migrate Addressables Automation Project State",
				    "Back up the raw settings under Library, preserve the selected config GUID, upgrade the schema, and turn automatic processing off?",
				    "Back Up and Migrate", "Cancel")) {
				return;
			}
			if (AddressablesAutomationProjectSettingsStore.TryMigrate(
				    out var recoveryPath, out var error)) {
				SetMessage($"Project state migrated. Backup: {recoveryPath}", MessageType.Info);
				LoadSavedProjectState();
			} else {
				SetMessage(error, MessageType.Error);
			}
		}

		private void MigrateConfiguration() {
			if (!EditorUtility.DisplayDialog(
				    "Migrate Addressables Automation Configuration",
				    "Write a JSON backup under Library, normalize existing rule collections, and upgrade this asset to the current schema?",
				    "Back Up and Migrate", "Cancel")) {
				return;
			}
			if (AddressablesAutomationSchemaMigration.TryMigrateConfig(
				    m_resolution.Config, out var recoveryPath, out var error)) {
				SetMessage($"Configuration migrated. Backup: {recoveryPath}", MessageType.Info);
				LoadSavedProjectState();
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

		private void LoadSavedProjectState() {
			m_resolution = AddressablesAutomationContextProvider.ResolveManual(AutomationScope.All);
			m_sceneResolution = AddressablesAutomationContextProvider.ResolveManual(AutomationScope.Scenes);
			m_pendingConfig = m_resolution.Config;
			m_pendingAutomaticScenes = m_resolution.ProjectSettings.AutomationEnabled &&
				(m_resolution.ProjectSettings.AutomaticScopes & AutomationScope.Scenes) != 0;
			m_analysis = null;
			m_loaded = true;
		}

		internal static bool CanApplyAutomaticSceneSetting(
			ConfigurationResolution sceneResolution,
			bool pendingEnabled) {
			return pendingEnabled
				? sceneResolution.IsReady
				: sceneResolution.ProjectSettings.AutomationEnabled;
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
