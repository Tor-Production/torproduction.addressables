using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RuntimeAddressables = UnityEngine.AddressableAssets.Addressables;
using UnityEditor.AddressableAssets.Settings;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateGroupsReport {
		internal static void ReportUpdatedGroups(AddressableAssetSettings addressableSettings, string targetGroupName, string assetsPath, HashSet<string> changedItems, HashSet<string> untouchedItems) {
			var changedCount = changedItems.Count;
			var untouchedCount = untouchedItems.Count;
			var totalCount = untouchedCount + changedCount;
			
			// File to store the updated asset paths
			string reportPath = Path.Combine(RuntimeAddressables.LibraryPath, "UpdateGroups.txt");
			
			// Start the file with the current date and time
			File.AppendAllText(reportPath, $"\n\n\tUpdate Session: {DateTime.Now}\n");
			
			File.AppendAllText(reportPath, $"{changedCount} of {totalCount} assets were moved to {targetGroupName} addressable group.\n" +
			                               $"{untouchedCount} of found assets were already marked as addressable.\n" +
			                               $"The targer folder to search is:\n{assetsPath}\n");
			if (changedCount > 0) {
				// Changed assets
				File.AppendAllText(reportPath, $"\n\tThe list of changed assets({changedCount}):\n");
				foreach (var guid in changedItems) {
					string assetPath = AssetDatabase.GUIDToAssetPath(guid);
					File.AppendAllText(reportPath, $"{Path.GetFileNameWithoutExtension(assetPath)} - GUID: {guid}\n\t\u2514\u2500at {assetPath}\n");
				}
			}

			if (untouchedCount > 0) {
				// Unchanged assets
				File.AppendAllText(reportPath, $"\n\tThe list of untouched assets({untouchedCount}):\n");
				foreach (var guid in untouchedItems) {
					string assetPath = AssetDatabase.GUIDToAssetPath(guid);
					AddressableAssetEntry entry = addressableSettings.FindAssetEntry(guid);
					File.AppendAllText(reportPath, $"{entry.parentGroup} - group\n\t\u2514\u2500{Path.GetFileNameWithoutExtension(assetPath)} - GUID: {guid}\n\t\t\u2514\u2500at {assetPath}\n");
				}
			}

			File.AppendAllText(reportPath, "\n-----------------------END-----------------------\n\n");
			
			Debug.Log($"{nameof(UpdateGroupsReport)} -> {nameof(ReportUpdatedGroups)} : " +
			          $"Total items found: {totalCount}\n" +
			          $"Number of items moved to group: {changedCount}\n" +
			          $"Number of items were already in groups: {untouchedCount}\n" +
			          $"Detailed list of items is reported in the file: {reportPath}");
		}
	}
}
