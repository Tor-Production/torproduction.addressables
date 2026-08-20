using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TorProduction.AddressablesToolpack.Data;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	public class ScenesListMapper : AssetPostprocessor {
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
			if (!PhaseZeroWorkflowGate.AutomaticSceneProcessingEnabled) {
				return;
			}

			var configScenesPath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.Scenes);
			var configScenes = AssetDatabase.LoadAssetAtPath<ScenesListConfig>(configScenesPath);
			string worldScenesFolderPath = AssetDatabase.GetAssetPath(configScenes.m_ScenesLocation);
			string uiScenesFolderPath = AssetDatabase.GetAssetPath(configScenes.m_UIScenesLocation);
			
			var configAppStatesPath = ProjectConfigPathsManager.GetConfigPath(ConfigsEnum.AppStates);
			var configAppStates = AssetDatabase.LoadAssetAtPath<AppStateConfig>(configAppStatesPath);

			var previousWorldScenes = configScenes.m_ScenesConfig.GetSceneNames();
			var previousWorldSceneInfos = configScenes.m_ScenesConfig.GetSceneInfos().ToList();
			var previousAllScenesList = configAppStates.GetAppStatesDictionary();
			var currentScenes = new HashSet<string>(previousWorldScenes);
			var addedScenes = new HashSet<string>();
			var removedScenes = new HashSet<string>();
			var nameChanged = new HashSet<string>();
			var nameToGUID = previousWorldSceneInfos.ToDictionary(i => i.Name, i => i.GUID);
			var uiScenes = new HashSet<string>();
			var worldScenes = new HashSet<string>();

			bool scenesListChanged = false;

			var allChangedAssets = importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths);

			foreach (string asset in allChangedAssets) {
				if (IsSceneInFolder(asset, worldScenesFolderPath)) {
					worldScenes.Add(asset);
					
					var prevoiousState = scenesListChanged;
					scenesListChanged = true;
					var guid = AssetDatabase.AssetPathToGUID(asset);
					var sceneName = Path.GetFileNameWithoutExtension(asset);
					if (IsANameChange(sceneName, guid, previousWorldSceneInfos)) {
						currentScenes.Add(sceneName);
						nameChanged.Add(asset);
						nameToGUID[sceneName] = guid;
						continue;
					}
					if (importedAssets.Contains(asset) || movedAssets.Contains(asset)) {
						if (IsAlreadyListed(sceneName, guid, previousWorldSceneInfos)) {
							scenesListChanged = prevoiousState;
							continue;
						}
						
						currentScenes.Add(sceneName);

						addedScenes.Add(asset);
						nameToGUID[sceneName] = guid;
					} else {
						currentScenes.Remove(sceneName);
						
						if (movedFromAssetPaths.Contains(asset)) {
							var removedAsset = movedAssets.Where(s => s.EndsWith(sceneName + ".unity")).FirstOrDefault();
							if (string.IsNullOrEmpty(removedAsset)) {
								int index = movedFromAssetPaths.Select((item, index) => new { Item = item, Index = index })
									.FirstOrDefault(x => x.Item == asset)?.Index ?? -1;
								if (index != -1 && nameChanged.Contains(movedAssets[index])) {
									removedScenes.Add(asset);
									worldScenes.Add(asset);
								}
							} else {
								removedScenes.Add(removedAsset);
								worldScenes.Add(removedAsset);
							}
						} else {
							removedScenes.Add(asset);
						}
					}
				} else if (IsSceneInFolder(asset, uiScenesFolderPath)) {
          			uiScenes.Add(asset);
		            
		            var prevoiousState = scenesListChanged;
		            scenesListChanged = true;
		            var guid = AssetDatabase.AssetPathToGUID(asset);
		            var sceneName = Path.GetFileNameWithoutExtension(asset);
		            
		            if (importedAssets.Contains(asset) || movedAssets.Contains(asset)) {
			            if (previousAllScenesList.ContainsKey(sceneName)) {
				            scenesListChanged = prevoiousState;
				            continue;
			            }
						
			            addedScenes.Add(asset);
			            nameToGUID[sceneName] = guid;
		            } else {
			            if (movedFromAssetPaths.Contains(asset)) {
				            var removedAsset = movedAssets.Where(s => s.EndsWith(sceneName + ".unity")).FirstOrDefault();
				            if (string.IsNullOrEmpty(removedAsset)) {
					            int index = movedFromAssetPaths.Select((item, index) => new { Item = item, Index = index })
						            .FirstOrDefault(x => x.Item == asset)?.Index ?? -1;
					            if (index != -1) {
						            removedScenes.Add(asset);
					            }
				            } else {
					            removedScenes.Add(removedAsset);
				            }
			            } else {
				            removedScenes.Add(asset);
			            }
		            }
				}

			}

			if (scenesListChanged) {
				// Update m_MainMenuConfig with the new scenes list
				var currentScenesList = currentScenes.ToList();
				currentScenesList.Sort();
				configScenes.m_ScenesConfig.SetSceneInfos(currentScenesList.Select( name => new SceneInfo {
						Name = name,
						GUID = nameToGUID[name]
					}
					).ToArray());
				
				foreach (var scene in nameChanged) {
					var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
					if (worldScenes.Contains(scene)) {
						//TODO: 2 for the game scenes. TMP solution
						previousAllScenesList.Add(sceneAsset.name, new AppState{StateValue = 2});
						ProjectAssetUtil.UpdateAssetAddress(sceneAsset);
						Debug.Log($"{nameof(ScenesListMapper)} -> {nameof(OnPostprocessAllAssets)} : The scene \"{scene}\" was added to the list of build scenes (in addressables)");
					} else {
						//TODO: 1 for the UI scenes. TMP solution
						previousAllScenesList.Add(sceneAsset.name, new AppState{StateValue = 1});
					}
				}
				
				foreach (var scene in addedScenes) {
					var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
					if (worldScenes.Contains(scene)) {
						//TODO: 2 for the game scenes. TMP solution
						previousAllScenesList.Add(sceneAsset.name, new AppState{StateValue = 2});
						ProjectAssetUtil.MakeAssetAddressable(sceneAsset, GroupNames.SCENES, scene);
						Debug.Log($"{nameof(ScenesListMapper)} -> {nameof(OnPostprocessAllAssets)} : The scene \"{scene}\" was added to the list of build scenes (in addressables)");
					} else if (uiScenes.Contains(scene)) {
						//TODO: 1 for the UI scenes. TMP solution
						previousAllScenesList.Add(sceneAsset.name, new AppState{StateValue = 1});
						AddBuildSettingsScene(scene);
					}
				}
				// For removed scenes
				foreach (var scene in removedScenes) {
					var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
					var name = Path.GetFileNameWithoutExtension(scene);
					if (!string.IsNullOrEmpty(name) && previousAllScenesList.ContainsKey(name)) {
						previousAllScenesList.Remove(name);
					}

					if (worldScenes.Contains(scene)) {
						ProjectAssetUtil.RemoveAssetFromAddressable(sceneAsset);
						Debug.Log($"{nameof(ScenesListMapper)} -> {nameof(OnPostprocessAllAssets)} : The scene \"{scene}\" was removed from the list of build scenes (in addressables)");
					} else {
						RemoveBuildSettingsScene(scene);
					}
				}
				
				// Save changes
				EditorUtility.SetDirty(configScenes.m_ScenesConfig);
				AssetDatabase.SaveAssets();
			}
		}

		private static bool IsSceneInFolder(string assetPath, string folderPath) {
			if (!assetPath.EndsWith(".unity")) {
				return false; // Return false if it's not a Unity scene file
			}

			string folderGUID = AssetDatabase.AssetPathToGUID(folderPath);
			string assetFolder = Path.GetDirectoryName(assetPath); // Get the directory of the asset

			while (!string.IsNullOrEmpty(assetFolder)) {
				string assetFolderGUID = AssetDatabase.AssetPathToGUID(assetFolder);
				if (assetFolderGUID == folderGUID) {
					return true; // The asset is in the folder
				}

				if (assetFolder == "Assets") {
					break; // Reached the root Assets folder, stop the loop
				}

				assetFolder = Path.GetDirectoryName(assetFolder); // Move up in the directory hierarchy
			}

			return false;
		}

		private static bool IsAlreadyListed(string name, string guid, List<SceneInfo> previousList) {
			bool guidSame = previousList.Any(scene => scene.Name == name && scene.GUID == guid);
			return guidSame;
		}

		private static bool IsANameChange(string name, string guid, List<SceneInfo> previousList) {
			bool sameGuidFound = previousList.Any(scene => scene.Name != name && scene.GUID == guid);
			return sameGuidFound;
		}
		
		public static void AddBuildSettingsScene(string scenePath) {
			// Find valid Scene paths and make a list of EditorBuildSettingsScene
			var editorBuildSettingsScenes = EditorBuildSettings.scenes.ToList();
			var alreadyContains = editorBuildSettingsScenes.Any(s => s.guid == AssetDatabase.GUIDFromAssetPath(scenePath));
			if (!string.IsNullOrEmpty(scenePath) && !alreadyContains) {
				editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(scenePath, true));
			}

			// Set the Build Settings window Scene list
			EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
		}
		
		public static void RemoveBuildSettingsScene(string scenePath) {
			// Find valid Scene paths and make a list of EditorBuildSettingsScene
			var editorBuildSettingsScenes = EditorBuildSettings.scenes.ToList();
			if (!string.IsNullOrEmpty(scenePath)) {
				var sceneItem = editorBuildSettingsScenes.Find(i => i.path == scenePath);
				editorBuildSettingsScenes.Remove(sceneItem);
			}

			// Set the Build Settings window Scene list
			EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
		}
	}
}
