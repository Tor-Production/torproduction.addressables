using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public static class ProjectConfigPathsManager {
		private const string DEFAULT_SCENES_CONFIG_PATH = "Assets/Modules/Services/DataService/Configs/ScenesBinderConfig.asset";
		private const string DEFAULT_ADDRESSABLE_ASSETS_CONFIG_PATH = "Assets/Modules/Services/DataService/Configs/ScenesBinderConfig.asset";
		private const string DEFAULT_APP_STATES_CONFIG_PATH = "Assets/Modules/Common/Configs/AppStateConfig/AppStateConfig.asset";
		private const string PROJECT_CONFIG_FILE_PATH = "ProjectSettings/ProjectConfig.json";

		public static void SaveConfigPath(string scenesConfigPath, string addressablesConfigPath, string AppStateConfigPath) {
			var configData = new ProjectConfigData {
				m_ScenesListConfigGUID = AssetDatabase.AssetPathToGUID(scenesConfigPath),
				m_AddressableAssetsConfigGUID = AssetDatabase.AssetPathToGUID(addressablesConfigPath),
				m_AppStatesConfigGUID = AssetDatabase.AssetPathToGUID(AppStateConfigPath)
			};

			string jsonData = JsonUtility.ToJson(configData);
			File.WriteAllText(PROJECT_CONFIG_FILE_PATH, jsonData);
		}

		public static string GetConfigPath(ConfigsEnum configType) {
			if (!File.Exists(PROJECT_CONFIG_FILE_PATH)) {
				SaveConfigPath(DEFAULT_SCENES_CONFIG_PATH, DEFAULT_ADDRESSABLE_ASSETS_CONFIG_PATH, DEFAULT_APP_STATES_CONFIG_PATH);
			}
			var jsonData = File.ReadAllText(PROJECT_CONFIG_FILE_PATH);
			var configData = JsonUtility.FromJson<ProjectConfigData>(jsonData);
			
			string defaultPath, savedGuid;

			switch (configType) {
				case ConfigsEnum.Scenes:
					defaultPath = DEFAULT_SCENES_CONFIG_PATH;
					savedGuid = configData?.m_ScenesListConfigGUID;
					break;
				case ConfigsEnum.AddressableAssets:
					defaultPath = DEFAULT_ADDRESSABLE_ASSETS_CONFIG_PATH;
					savedGuid = configData?.m_AddressableAssetsConfigGUID;
					break;
				case ConfigsEnum.AppStates:
					defaultPath = DEFAULT_APP_STATES_CONFIG_PATH;
					savedGuid = configData?.m_AppStatesConfigGUID;
					break;
				default:
					throw new NotImplementedException($"An implementaion for {configType} is missed");
			}
			
			var configPath = AssetDatabase.GUIDToAssetPath(savedGuid); 

			if (configData == null || string.IsNullOrEmpty(configPath)) {
				SaveConfigPath(
					AssetDatabase.GUIDToAssetPath(configData?.m_ScenesListConfigGUID), 
					AssetDatabase.GUIDToAssetPath(configData?.m_AddressableAssetsConfigGUID),
					AssetDatabase.GUIDToAssetPath(configData?.m_AppStatesConfigGUID)
					);
				return defaultPath;
			}
			return configPath;
		}
	}
}
