using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RuntimeAddressables = UnityEngine.AddressableAssets.Addressables;

namespace TorProduction.AddressablesToolpack.Editor {
	public static class ReportUpdater {
		
		private const string LAYOUT_FILE_NAME = "buildlayout";
		
		// The buildlayout report file name and location is hardcoded so
		// this is the only way to save a uniq report for each platform
		public static void RenameBuildLayoutReport(string nameExtension) {
			// check if preferences setting is set
			if (!ProjectConfigData.GenerateBuildLayout) {
				Debug.LogWarning($"{nameof(ReportUpdater)} -> {nameof(RenameBuildLayoutReport)} : " +
				                 $"The buildlayout report is disabled and won't be created.\n" +
				                 $"To enable it go to Edit->Preferences->Addressables->Build Settings");
				return;
			}

			if (string.IsNullOrEmpty(nameExtension)) {
				Debug.LogWarning($"{nameof(ReportUpdater)} -> {nameof(RenameBuildLayoutReport)} : " +
				                 $"The empty param value ({nameExtension}) won't affect the file name. Skipped.");
				return;
			}
			
			var filePath = $"{RuntimeAddressables.LibraryPath}{LAYOUT_FILE_NAME}";
			var fileFormat = ProjectConfigData.BuildLayoutReportFileFormat;
			var extension = (fileFormat == ProjectConfigData.ReportFileFormat.JSON) ? "json" : "txt";
			var sourseFile = $"{filePath}.{extension}";
			var destinationFile = $"{filePath}{nameExtension}.{extension}";

			if (!File.Exists(sourseFile)) {
				Debug.LogWarning($"{nameof(ReportUpdater)} -> {nameof(RenameBuildLayoutReport)} : " +
				                 $"Source file doesn't exists. Expected at: {sourseFile}");
				return;
			}
			
			if (File.Exists(destinationFile)) {
				File.Delete(destinationFile);
			}
			
			File.Move(sourseFile, destinationFile);
		}
	}
}
