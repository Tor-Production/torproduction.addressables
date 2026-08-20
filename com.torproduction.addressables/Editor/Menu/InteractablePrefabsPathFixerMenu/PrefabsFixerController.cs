using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Android;
using TorProduction.AddressablesToolpack.Data;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal class PrefabsFixerController {
		private const string PREFABS_FIXER_CONFIG_PATH = "Assets/AddressableAssetsData/Configs/DefaultPrefabFixerConfig.asset";

		internal static PrefabsFixerConfig Config {
			get {
				return AssetDatabase.LoadAssetAtPath<PrefabsFixerConfig>(PREFABS_FIXER_CONFIG_PATH);
			}
		}
		internal static void FixPrefabPaths() {
			var allInteractableTemplates = LoadAllInteractables();

			// Load PrefabFixerConfig ScriptableObject
			var config = Config;
			if (config == null) {
				Debug.LogError($"{nameof(PrefabsFixerController)} -> {nameof(FixPrefabPaths)} : PrefabFixerConfig not found at the specified path.");
				return;
			}

			// Retrieve the root folder path from the config
			var rootFolderAsset = config.GetRootFolder();
			string targetFolderPath = AssetDatabase.GetAssetPath(rootFolderAsset);
			if (string.IsNullOrEmpty(targetFolderPath)) {
				Debug.LogError($"{nameof(PrefabsFixerController)} -> {nameof(FixPrefabPaths)} : Root folder path in PrefabFixerConfig is not set or invalid.");
				return;
			}

			string reportPath = InitLoggingFile();

			// Start the file with the current date and time
			File.AppendAllText(reportPath, $"\n\n\tUpdate Session: {DateTime.Now}\n");

			// Get the type of the class where the field is defined
			Type type = typeof(IObjectTemplate); // Replace with the actual class name

			// Get the field
			FieldInfo prefabFieldInfo = type.GetField("m_characterPrefab", BindingFlags.NonPublic | BindingFlags.Instance); // this one is used for legacy code
			FieldInfo referenceFieldInfo = type.GetField("m_characterPrefabReference", BindingFlags.NonPublic | BindingFlags.Instance);
			if (prefabFieldInfo == null && referenceFieldInfo == null) {
				// Handle the case where the field is not found
				Debug.LogError($"{nameof(PrefabsFixerController)} -> {nameof(FixPrefabPaths)} : " +
				               $"m_characterPrefab and m_characterPrefabReference fields not found. There is nothing to do right here");
				return;
			}

			var movedAmbiguousPrefabs = new HashSet<GameObject>(HandleAmbiguousPrefabs(allInteractableTemplates, targetFolderPath));

			var totalMoved = 0;

			foreach (var template in allInteractableTemplates) {
				var prefabPath = "";
				var prefabTargetFolder = Path.Combine(targetFolderPath, template.CustomInteractableType);

				if (prefabFieldInfo != null) {
					var prefab = (GameObject)prefabFieldInfo.GetValue(template);
					if (prefab != null && !movedAmbiguousPrefabs.Contains(prefab)) {
						prefabPath = AssetDatabase.GetAssetPath(prefab);
						if (MovePrefabToCorrectFolder(prefabPath, prefabTargetFolder, reportPath)) {
							++totalMoved;
						} else {
							prefabPath = "";
						}
					}
				}

				if (referenceFieldInfo != null) {
					var reference = (AssetReferenceGameObject)referenceFieldInfo.GetValue(template);
					var prefab = (GameObject)reference.editorAsset;
					if (prefab != null && !movedAmbiguousPrefabs.Contains(prefab)) {
						var referencePath = AssetDatabase.GetAssetPath(prefab);
						if (prefabPath != referencePath) {
							if (MovePrefabToCorrectFolder(referencePath, prefabTargetFolder, reportPath)) {
								++totalMoved;
							}
						}
					}
				}
			}

			File.AppendAllText(reportPath, $"Total number of moved files: {totalMoved}\n");
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log($"{totalMoved} interactable prefabs moved. Moved asset paths saved to " + reportPath);
		}

		private static List<GameObject> HandleAmbiguousPrefabs(List<ObjectTemplate> allTemplates,
			string targetFolderPath)
		{
			return null;
		}

		// TODO: adapt to generic
		// private static List<GameObject> HandleAmbiguousPrefabs(List<InteractableObjectTemplate> allTemplates, string targetFolderPath) {
		// 	var ambiguousPrefabs = new Dictionary<GameObject, HashSet<string>>();
		// 	
		// 	Type type = typeof(InteractableObjectTemplate);
		// 	FieldInfo prefabFieldInfo = type.GetField("m_characterPrefab", BindingFlags.NonPublic | BindingFlags.Instance); // this one is used for legacy code
		// 	FieldInfo referenceFieldInfo = type.GetField("m_characterPrefabReference", BindingFlags.NonPublic | BindingFlags.Instance);
		// 	
		// 	foreach (var template in allTemplates) {
		// 		if (prefabFieldInfo != null) {
		// 			var prefab = (GameObject)prefabFieldInfo.GetValue(template);
		// 			if (prefab != null) {
		// 				var path = Path.Combine(targetFolderPath, template.CustomInteractableType).Replace('\\', '/');
		// 				if (ambiguousPrefabs.TryGetValue(prefab, out var types)) {
		// 					types.Add(path);
		// 				} else {
		// 					ambiguousPrefabs.Add(prefab, new HashSet<string>(new[] { path }));
		// 				}
		// 			}
		// 		}
		// 		
		// 		if (referenceFieldInfo != null) {
		// 			var prefabReference = (AssetReferenceGameObject)referenceFieldInfo.GetValue(template);
		// 			var prefab = prefabReference?.editorAsset;
		//
		// 			if (prefab == null) {
		// 				continue;
		// 			}
		//
		// 			var path = Path.Combine(targetFolderPath, template.CustomInteractableType).Replace('\\', '/');
		// 			if (ambiguousPrefabs.TryGetValue(prefab, out var types)) {
		// 				types.Add(path);
		// 			} else {
		// 				ambiguousPrefabs.Add(prefab, new HashSet<string>(new []{path}));
		// 			}
		// 		}
		// 	}
		// 	
		// 	var keysToRemove = ambiguousPrefabs.Where(kvp => kvp.Value.Count == 1)
		// 		.Select(kvp => kvp.Key)
		// 		.ToList();
		//
		// 	//remove non-ambiguous items
		// 	foreach (var key in keysToRemove) {
		// 		ambiguousPrefabs.Remove(key);
		// 	}
		//
		// 	var needToBeMoved = new HashSet<GameObject>();
		// 	foreach (var pair in ambiguousPrefabs) {
		// 		needToBeMoved.Add(pair.Key);
		// 	}
		//
		// 	foreach (var pair in ambiguousPrefabs) {
		// 		if (!needToBeMoved.Contains(pair.Key)) {
		// 			continue;
		// 		}
		// 		var paths = pair.Value;
		// 		var prefabPath = AssetDatabase.GetAssetPath(pair.Key);
		// 		prefabPath = prefabPath.Replace('\\', '/');
		// 		foreach (var path in paths) {
		// 			var fixedPath = path.Replace('\\', '/');
		// 			if (prefabPath.StartsWith(fixedPath)) {
		// 				needToBeMoved.Remove(pair.Key);
		// 				break;
		// 			}
		// 		}
		// 	}
		//
		// 	foreach (var item in needToBeMoved) {
		// 		var prefabPath = AssetDatabase.GetAssetPath(item);
		// 		var prefabTarget = ambiguousPrefabs[item].FirstOrDefault();
		// 		MovePrefabToCorrectFolder(prefabPath, prefabTarget);
		// 	}
		//
		// 	return ambiguousPrefabs.Keys.ToList();
		// }

		private static string InitLoggingFile() {
			// Get the project root folder path by moving up one level from the Assets folder
			string projectRootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			string logFolder = Path.Combine(projectRootPath, "Logs");

			// Create the Logs folder if it doesn't exist
			if (!Directory.Exists(logFolder)) {
				Directory.CreateDirectory(logFolder);
			}

			// Set the path for the log file
			return Path.Combine(logFolder, "MovedInteractables.txt");
		}

		private static bool MovePrefabToCorrectFolder(string prefabPath, string targetFolderPath, string reportPath = null) {
			// Set same separators for both paths
			targetFolderPath = targetFolderPath.Replace('\\', '/');
			prefabPath = prefabPath.Replace('\\', '/');
			
			// Check if the prefab is in the target folder or its subfolders
			if (!prefabPath.StartsWith(targetFolderPath)) {
				if (!Directory.Exists(targetFolderPath)) {
					Directory.CreateDirectory(targetFolderPath);
					AssetDatabase.Refresh();
				}
				// Move the prefab to the target folder
				string newPrefabPath = Path.Combine(targetFolderPath, Path.GetFileName(prefabPath));
				newPrefabPath = newPrefabPath.Replace('\\', '/');
				var result = AssetDatabase.MoveAsset(prefabPath, newPrefabPath);

				if (reportPath != null) {
					// Append the updated asset path to the file
					var reportString = $"{prefabPath}\n\t\u2514\u2500\u2500 moved to {newPrefabPath}\n";
					reportString += string.IsNullOrEmpty(result) ? "" : $"\tresult: {result}\n";
					File.AppendAllText(reportPath, reportString);
				}

				return true;
			}

			return false;
		}

		// TODO: adapt to generic
		// private static List<InteractableObjectTemplate> LoadAllInteractables() {
		// 	var settings = AddressableAssetSettingsDefaultObject.Settings;
		// 	var assetsList = new List<InteractableObjectTemplate>();
		//
		// 	if (settings != null) {
		// 		var group = settings.FindGroup(GroupNames.INTERACTABLE_TEMPLATES);
		// 		if (group != null) {
		// 			foreach (var entry in group.entries) {
		// 				if (entry.labels.Contains("InteractableTemplate")) {
		// 					string assetPath = entry.AssetPath;
		// 					var loadedAsset = AssetDatabase.LoadAssetAtPath<InteractableObjectTemplate>(assetPath);
		// 					if (loadedAsset != null) {
		// 						assetsList.Add(loadedAsset);
		// 					}
		// 				}
		// 			}
		// 		}
		// 	}
		//
		// 	return assetsList;
		// }
		
		//temporary placeholder
		private static List<ObjectTemplate> LoadAllInteractables()
		{
			return null;
		}
	}
}
