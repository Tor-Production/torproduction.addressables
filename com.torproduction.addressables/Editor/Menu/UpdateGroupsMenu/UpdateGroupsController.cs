using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Object = UnityEngine.Object;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateGroupsController {
		
		private static bool FOLDERS_FIX_ALLOWED = false;
		
		internal static void UpdateGroups(UpdateGroupsConfig config) {
			string folderPath = AssetDatabase.GetAssetPath(config.FolderAsset);
			Debug.Log($"{nameof(UpdateGroupsController)} -> {nameof(UpdateGroups)} : Start updating addressable groups:\n" +
			          $"Group: {config.GroupName}\n" +
			          $"Folder: {folderPath}\n" +
			          $"{(config.TypesFilter == null ? "" : "Filters: " + string.Join(", ", config.TypesFilter))}");

			if (config.FolderAsset == null || string.IsNullOrEmpty(config.GroupName)) {
				Debug.LogError($"{nameof(UpdateGroupsController)} -> {nameof(UpdateGroups)} : FolderAsset and GroupName must be set.");
				return;
			}

			var assetGuids = AssetDatabase.FindAssets("", new[] { folderPath });
			// Remove folders
			assetGuids = assetGuids
				.Where(guid => !Directory.Exists(AssetDatabase.GUIDToAssetPath(guid)))
				.ToArray();

			var addressableSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			var group = addressableSettings.groups.FirstOrDefault(g => g.Name == config.GroupName);

			if (group == null) {
				Debug.LogError($"{nameof(UpdateGroupsController)} -> {nameof(UpdateGroups)} : Group with name {config.GroupName} not found.");
				return;
			}

			var useAll = config.TypesFilter == null || config.TypesFilter.Length == 0; // if no filter provided
			var assetsAlreadyInGroup = new HashSet<string>();
			var assetsToChange = new HashSet<string>();

			
			var qualifiedTypesFilter = new List<Type>();
			var asmList = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var filterTypeName in config.TypesFilter) {
				var typeNamePattern = $".{filterTypeName}";
				
				foreach (var asm in asmList) {
					var typesList = asm.GetTypes();

					foreach (var type in typesList) {
						if (type.FullName.EndsWith(typeNamePattern)) {
							qualifiedTypesFilter.Add(type);
							goto Found;
						}
					}

				}
				Found: ; // Label used as the target for 'goto' to escape nested loops
			
			}

			foreach (string guid in assetGuids) {
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);
				Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

				// Check if the asset type matches the filter or is an inherited type
				bool typeMatchesOrInherited = false;


				if (!useAll) {
					var assetType = asset.GetType();
					foreach (var filterType in qualifiedTypesFilter) {
						if (filterType != null && filterType.IsAssignableFrom(assetType)) {
							typeMatchesOrInherited = true;
							break;
						}
					}
				}

				if (useAll || typeMatchesOrInherited) {
					AddressableAssetEntry entry = addressableSettings.FindAssetEntry(guid);
					if (entry == null) {
						assetsToChange.Add(guid);
					} else {
						assetsAlreadyInGroup.Add(guid);
					}
				}
			}

			
			// TODO: investigate the way to perform it in bulk since it's a heavy operation and requires a huge amount of time for big numbers of assets
			foreach (string guid in assetsToChange) {
				// Add assets to the specified group in addressables
				var entry = addressableSettings.CreateOrMoveEntry(guid, group);
				
				foreach (var lable in config.Lables) {
					entry.SetLabel(lable, true, false);
				}
				entry.SetAddress(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)), false);
				// Save changes
				addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
			}
			
			UpdateGroupsReport.ReportUpdatedGroups(addressableSettings, config.GroupName, folderPath, assetsToChange, assetsAlreadyInGroup);
		}

		internal static void CleanUpDefaultGroup() {
			var addressableSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			var entries = new List<AddressableAssetEntry>(addressableSettings.DefaultGroup.entries);
			foreach (var entry in entries) {
				addressableSettings.DefaultGroup.RemoveAssetEntry(entry);
			}
		}

		// searches for items which are in wrong groups and move to a correct one 
		internal static void FixWrongItemGroups(UpdateGroupSettings[] settings) {
			if (settings == null || settings.Length == 0) {
				Debug.LogError($"{nameof(UpdateGroupsController)} -> {nameof(FixWrongItemGroups)} : The list of settings is empty");
				return;
			}

			Dictionary<string, UpdateGroupSettings> configByType = new();
			foreach (var config in settings) {
				foreach (var type in config.TypeFilterNames) {
					configByType.Add(type, config);
				}
			}
			
			var asmList = AppDomain.CurrentDomain.GetAssemblies();
			Dictionary<string, Type> typeByName = new();
			foreach (var filterTypeName in configByType.Keys) {
				var typeNamePattern = $".{filterTypeName}";
				
				foreach (var asm in asmList) {
					var typesList = asm.GetTypes();

					foreach (var type in typesList) {
						if (type.FullName.EndsWith(typeNamePattern)) {
							typeByName.Add(filterTypeName, type);
							goto Found;
						}
					}

				}
				Found: ; // Label used as the target for 'goto' to escape nested loops
			}
			
			var addressableSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			var allEntries = addressableSettings.groups.SelectMany(group => group.entries).ToList();
			foreach (var entry in allEntries) {
				var entryType = entry.MainAssetType;
				foreach (var groupTypeName in configByType.Keys) {
					var groupType = typeByName[groupTypeName];
					if (groupType.IsAssignableFrom(entryType)) {
						var config = configByType[groupTypeName];
						// if the list of labels is not the same
						if (!new HashSet<string>(config.Lables).SetEquals(entry.labels)) {
							entry.labels.Clear();
							foreach (var label in config.Lables) {
								entry.SetLabel(label, true, false);
							}
							addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
						}

						// if the group is wrong
						if (entry.parentGroup.name != config.GroupName) {
							var group = addressableSettings.groups.FirstOrDefault(g => g.Name == config.GroupName);
							addressableSettings.MoveEntry(entry, group);
						}

						if (FOLDERS_FIX_ALLOWED) {
							// if the folder is wrong
							string folderPath = AssetDatabase.GetAssetPath(config.AssetsFolder);
							string filePath = entry.AssetPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

							folderPath = folderPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
								.TrimEnd(Path.DirectorySeparatorChar);
							bool isFileInFolder = filePath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase);

							if (!isFileInFolder) {
								var newPath = Path.Combine(folderPath, Path.GetFileName(filePath));
								AssetDatabase.MoveAsset(filePath, newPath);
							}
						}
					}
				}
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		} 
	}
}
