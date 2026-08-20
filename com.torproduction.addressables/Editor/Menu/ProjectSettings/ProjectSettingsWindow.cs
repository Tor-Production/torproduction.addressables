using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public class ProjectSettingsWindow : EditorWindow {
		private string m_sceneConfigFilePath;
		private ScenesListConfig m_scenesListConfig;
		private string m_addressableAssetsConfigFilePath;
		private AddressableAssetsConfig m_addressableAssetsConfig;
		private string m_appStatesConfigFilePath;
		private AppStateConfig m_appStatesConfig;

		[MenuItem("Tools/Tor Production/Tor Production Project setting", priority = 300)]
		public static void ShowWindow() {
			GetWindow<ProjectSettingsWindow>("Tor Production Project Setting");
		}

		private void OnEnable() {
			// Load the existing settings
			m_sceneConfigFilePath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.Scenes);
			m_scenesListConfig = AssetDatabase.LoadAssetAtPath<ScenesListConfig>(m_sceneConfigFilePath);
			
			m_addressableAssetsConfigFilePath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.AddressableAssets);
			m_addressableAssetsConfig = AssetDatabase.LoadAssetAtPath<AddressableAssetsConfig>(m_addressableAssetsConfigFilePath);
			
			m_appStatesConfigFilePath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.AppStates);
			m_appStatesConfig = AssetDatabase.LoadAssetAtPath<AppStateConfig>(m_appStatesConfigFilePath);
		}

		private void OnGUI() {
			// Display a field for the user to set the ScenesListConfig asset
			m_scenesListConfig = (ScenesListConfig)EditorGUILayout.ObjectField("Scenes List Config", m_scenesListConfig, typeof(ScenesListConfig), false);

			// Update configFilePath if scenesListConfig changes
			if (m_scenesListConfig != null && AssetDatabase.GetAssetPath(m_scenesListConfig) != m_sceneConfigFilePath) {
				m_sceneConfigFilePath = AssetDatabase.GetAssetPath(m_scenesListConfig);
			}

			// Display a field for the user to set the ScenesListConfig asset
			m_addressableAssetsConfig = (AddressableAssetsConfig)EditorGUILayout.ObjectField("Addressable Assets Config", m_addressableAssetsConfig, typeof(AddressableAssetsConfig), false);

			// Update configFilePath if addressableAssetsConfig changes
			if (m_addressableAssetsConfig != null && AssetDatabase.GetAssetPath(m_addressableAssetsConfig) != m_addressableAssetsConfigFilePath) {
				m_addressableAssetsConfigFilePath = AssetDatabase.GetAssetPath(m_addressableAssetsConfig);
			}
			
			// Display a field for the user to set the AppStateConfig asset
			m_appStatesConfig = (AppStateConfig)EditorGUILayout.ObjectField("AppStates Config", m_appStatesConfig, typeof(AppStateConfig), false);

			// Update configFilePath if addressableAssetsConfig changes
			if (m_appStatesConfig != null && AssetDatabase.GetAssetPath(m_appStatesConfig) != m_appStatesConfigFilePath) {
				m_appStatesConfigFilePath = AssetDatabase.GetAssetPath(m_appStatesConfig);
			}

			var canSave = CanSave();
			if (!canSave) {
				EditorGUILayout.HelpBox("Select all three valid legacy configuration assets before saving.", MessageType.Info);
			}

			EditorGUI.BeginDisabledGroup(!canSave);
			if (GUILayout.Button("Save")) {
				SaveConfigPath();
			}
			EditorGUI.EndDisabledGroup();
		}

		private void SaveConfigPath() {
			if (ProjectConfigPathsManager.TrySaveConfigPaths(
				    AssetDatabase.GetAssetPath(m_scenesListConfig),
				    AssetDatabase.GetAssetPath(m_addressableAssetsConfig),
				    AssetDatabase.GetAssetPath(m_appStatesConfig),
				    out var error)) {
				Debug.Log($"{nameof(ProjectSettingsWindow)} -> {nameof(SaveConfigPath)} : Configuration file paths saved:\n" +
				          $"Scenes: {m_sceneConfigFilePath}\nAddressable assets: {m_addressableAssetsConfigFilePath}");
			} else {
				Debug.LogError($"{nameof(ProjectSettingsWindow)} -> {nameof(SaveConfigPath)} : {error}");
			}
		}

		private bool CanSave() {
			return m_scenesListConfig != null &&
			       m_addressableAssetsConfig != null &&
			       m_appStatesConfig != null;
		}
	}
}
