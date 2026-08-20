using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor {
	public static class ProjectAssetUtil {
		/// <summary>
		/// Marks an object as Addressable so that it would be packed to a bundle
		/// </summary>
		/// <param name="asset"> An asset in the project </param>
		/// <param name="groupName"> A group name in addressable groups </param>
		/// <param name="address"> It's ID in realm of Addressables. Usually asset name, GUID or relative path is used </param>
		public static void MakeAssetAddressable(Object asset, string groupName, string address) {
			// Get the addressable asset settings
			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

			if (settings == null) {
				Debug.LogError($"{nameof(ProjectAssetUtil)} -> {nameof(MakeAssetAddressable)} : Failed to retrieve default Addressable Asset Settings.");
				return;
			}

			// Create or get existing group
			var group = settings.FindGroup(groupName);
			if (group == null) {
				group = settings.CreateGroup(groupName, false, false, false, null);
				Debug.LogWarning($"{nameof(ProjectAssetUtil)} -> {nameof(MakeAssetAddressable)} : The Group with name {groupName} is not found. A new one is created");
			}

			// Get the asset path
			string assetPath = AssetDatabase.GetAssetPath(asset);

			// Check if the asset is already addressable
			AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
			if (entry == null) {
				// Add the asset to the addressable group
				entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
				entry.address = address;
				entry.SetAddress(Path.GetFileNameWithoutExtension(entry.address), false);
				
			} else {
				Debug.LogWarning($"{nameof(ProjectAssetUtil)} -> {nameof(MakeAssetAddressable)} : Asset is already addressable.");
			}

			// Save changes
			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
		}
		
		/// <summary>
		/// Removes an object from Addressables.
		/// </summary>
		/// <param name="asset">An asset in the project</param>
		public static void RemoveAssetFromAddressable(Object asset) {
			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

			if (settings == null) {
				Debug.LogError($"{nameof(ProjectAssetUtil)} -> {nameof(RemoveAssetFromAddressable)} : Failed to retrieve default Addressable Asset Settings.");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(asset);
			string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);

			AddressableAssetEntry entry = settings.FindAssetEntry(assetGUID);
			if (entry != null) {
				// Remove the asset from its group
				entry.parentGroup.RemoveAssetEntry(entry);
				Debug.Log($"{nameof(ProjectAssetUtil)} -> {nameof(RemoveAssetFromAddressable)} : Asset removed from Addressables. ({entry.address})");
			} else {
				Debug.LogWarning($"{nameof(ProjectAssetUtil)} -> {nameof(RemoveAssetFromAddressable)} : Asset is not found in Addressables.");
			}

			// Save changes
			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, true);
		}

		/// <summary>
		/// Update Asset address/name
		/// </summary>
		/// <param name="asset"></param>
		public static void UpdateAssetAddress(Object asset) {
			var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);

			if (settings == null) {
				Debug.LogError($"{nameof(ProjectAssetUtil)} -> {nameof(UpdateAssetAddress)} : Failed to retrieve default Addressable Asset Settings.");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(asset);
			string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);

			AddressableAssetEntry entry = settings.FindAssetEntry(assetGUID);
			if (entry != null) {
				entry.address = Path.GetFileNameWithoutExtension(assetPath);;
				Debug.Log($"{nameof(ProjectAssetUtil)} -> {nameof(UpdateAssetAddress)} : Asset name updated in Addressables. ({entry.address})");
			} else {
				Debug.LogWarning($"{nameof(ProjectAssetUtil)} -> {nameof(UpdateAssetAddress)} : Asset is not found in Addressables.");
			}

			// Save changes
			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
		}
	}
}
