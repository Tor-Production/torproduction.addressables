using System;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateAllNewAssetsController {
		internal static void UpdateAllNewAssets() {
			// Try to fix interactable templates first
			InteractableTemplateFieldsUpdater.UpdateFields();
			
			// Update all new templates
			UpdateGroupsFromConfig();
			
			// Update the list of required prefabs
			PrefabsFixerController.FixPrefabPaths();
			
			// Update prefabs group after moving them to the correct folder
			UpdateInteractablePrefabsGroup();
			
			// Find all the new dependency duplicates and add them to addressables
			DependencyResolverController.FixPrefabPaths();
			
			// If for some reason something went wrong this method should solve it
			FixWrongAssets();
		}

		private static void UpdateGroupsFromConfig() {
			UpdateGroupsController.CleanUpDefaultGroup();
			
			var configPath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.AddressableAssets);
			var config = AssetDatabase.LoadAssetAtPath<AddressableAssetsConfig>(configPath);
			var settingsArray = config?.m_Settings;

			if (settingsArray == null) {
				Debug.LogError($"{nameof(UpdateAllNewAssetsController)} -> {nameof(UpdateGroupsFromConfig)} : can't find a settings array for updating addressable groups");
				return;
			}
			
			foreach (var settingsItem in settingsArray) {
				var updateConfig = new UpdateGroupsConfig {
					FolderAsset = settingsItem.AssetsFolder,
					GroupName = settingsItem.GroupName,
					Lables = settingsItem.Lables,
					TypesFilter = settingsItem.FilterByType ? settingsItem.TypeFilterNames : new string[0]
				};
				UpdateGroupsController.UpdateGroups(updateConfig);
			}
		}
		
		private static void FixWrongAssets() {
			var configPath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.AddressableAssets);
			var config = AssetDatabase.LoadAssetAtPath<AddressableAssetsConfig>(configPath);
			var settingsArray = config?.m_Settings;

			UpdateGroupsController.FixWrongItemGroups(settingsArray);
		}

		private static void UpdateInteractablePrefabsGroup() {
			var fixerConfig = PrefabsFixerController.Config;
			var updateConfig = new UpdateGroupsConfig {
				FolderAsset = fixerConfig.GetRootFolder(),
				GroupName = GroupNames.INTERACTABLES,
				Lables = Array.Empty<string>(),
				TypesFilter = new [] {"GameObject"}
			};
			UpdateGroupsController.UpdateGroups(updateConfig);
		}
	}
}
