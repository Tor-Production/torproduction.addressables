using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public static class ProjectConfigPathsManager {
		private const string PROJECT_CONFIG_FILE_PATH = "ProjectSettings/ProjectConfig.json";

		public static void SaveConfigPath(string scenesConfigPath, string addressablesConfigPath, string appStateConfigPath) {
			if (!TrySaveConfigPaths(scenesConfigPath, addressablesConfigPath, appStateConfigPath, out var error)) {
				throw new ArgumentException(error);
			}
		}

		public static bool TrySaveConfigPaths(
			string scenesConfigPath,
			string addressablesConfigPath,
			string appStateConfigPath,
			out string error) {
			if (!TryGetAssetGuid(scenesConfigPath, out var scenesGuid)) {
				error = "Select a valid Scenes List Config asset before saving.";
				return false;
			}

			if (!TryGetAssetGuid(addressablesConfigPath, out var addressablesGuid)) {
				error = "Select a valid Addressable Assets Config asset before saving.";
				return false;
			}

			if (!TryGetAssetGuid(appStateConfigPath, out var appStatesGuid)) {
				error = "Select a valid App States Config asset before saving.";
				return false;
			}

			var configData = new ProjectConfigData {
				m_ScenesListConfigGUID = scenesGuid,
				m_AddressableAssetsConfigGUID = addressablesGuid,
				m_AppStatesConfigGUID = appStatesGuid
			};

			try {
				string jsonData = JsonUtility.ToJson(configData);
				File.WriteAllText(PROJECT_CONFIG_FILE_PATH, jsonData);
				error = string.Empty;
				return true;
			} catch (Exception exception) {
				error = $"Could not save project configuration: {exception.Message}";
				return false;
			}
		}

		public static string GetConfigPath(ConfigsEnum configType) {
			return TryGetConfigPath(configType, out var configPath) ? configPath : string.Empty;
		}

		public static bool TryGetConfigPath(ConfigsEnum configType, out string configPath) {
			configPath = string.Empty;
			if (!File.Exists(PROJECT_CONFIG_FILE_PATH)) {
				return false;
			}

			try {
				var jsonData = File.ReadAllText(PROJECT_CONFIG_FILE_PATH);
				var configData = JsonUtility.FromJson<ProjectConfigData>(jsonData);
				var savedGuid = GetSavedGuid(configData, configType);
				if (string.IsNullOrEmpty(savedGuid)) {
					return false;
				}

				var savedPath = AssetDatabase.GUIDToAssetPath(savedGuid);
				if (string.IsNullOrEmpty(savedPath) || AssetDatabase.LoadMainAssetAtPath(savedPath) == null) {
					return false;
				}

				configPath = savedPath;
				return true;
			} catch (Exception) {
				return false;
			}
		}

		private static string GetSavedGuid(ProjectConfigData configData, ConfigsEnum configType) {
			if (configData == null) {
				return string.Empty;
			}

			switch (configType) {
				case ConfigsEnum.Scenes:
					return configData.m_ScenesListConfigGUID;
				case ConfigsEnum.AddressableAssets:
					return configData.m_AddressableAssetsConfigGUID;
				case ConfigsEnum.AppStates:
					return configData.m_AppStatesConfigGUID;
				default:
					return string.Empty;
			}
		}

		private static bool TryGetAssetGuid(string assetPath, out string guid) {
			guid = string.Empty;
			if (string.IsNullOrEmpty(assetPath) || AssetDatabase.LoadMainAssetAtPath(assetPath) == null) {
				return false;
			}

			guid = AssetDatabase.AssetPathToGUID(assetPath);
			return !string.IsNullOrEmpty(guid);
		}
	}
}
