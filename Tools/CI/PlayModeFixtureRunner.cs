using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TorProduction.Addressables.ReleaseReadiness {
	public static class PlayModeFixtureRunner {
		private const string FixtureRoot = "Assets/__TorProductionAddressablesPlayMode";
		private const string AssetPath = FixtureRoot + "/KnownAsset.txt";
		private const string SettingsRoot = "Assets/AddressableAssetsData";
		private const string Address = "tor-production/release-readiness-known-asset";

		public static void Prepare() {
			RequireMarker();
			if (AddressableAssetSettingsDefaultObject.SettingsExists ||
			    AssetDatabase.IsValidFolder(FixtureRoot) ||
			    AssetDatabase.IsValidFolder(SettingsRoot)) {
				throw new InvalidOperationException(
					"The disposable PlayMode project is not clean before fixture setup.");
			}

			Directory.CreateDirectory(FixtureRoot);
			File.WriteAllText(AssetPath, "Tor Production Addressables PlayMode fixture\n");
			AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

			var settings = AddressableAssetSettings.Create(
				SettingsRoot,
				"AddressableAssetSettings",
				true,
				true);
			AddressableAssetSettingsDefaultObject.Settings = settings;
			var entry = settings.CreateOrMoveEntry(
				AssetDatabase.AssetPathToGUID(AssetPath),
				settings.DefaultGroup,
				false,
				false);
			entry.address = Address;

			var packedPlayModeIndex = settings.DataBuilders.FindIndex(
				builder => builder is BuildScriptPackedPlayMode);
			if (packedPlayModeIndex < 0) {
				throw new InvalidOperationException(
					"Addressables did not create its built-in packed Play Mode builder.");
			}
			settings.ActivePlayModeDataBuilderIndex = packedPlayModeIndex;
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();

			AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
			if (result == null || !string.IsNullOrEmpty(result.Error)) {
				throw new InvalidOperationException(
					"Unable to build the disposable Addressables PlayMode fixture: " +
					(result?.Error ?? "no result"));
			}
			Debug.Log("Prepared built-in packed Play Mode Addressables fixture at " + result.OutputPath);
		}

		public static void Cleanup() {
			RequireMarker();
			AddressableAssetSettingsDefaultObject.Settings = null;
			foreach (var assetPath in new[] { SettingsRoot, FixtureRoot, "Assets/StreamingAssets/aa" }) {
				if (AssetDatabase.IsValidFolder(assetPath) ||
				    AssetDatabase.LoadMainAssetAtPath(assetPath) != null) {
					if (!AssetDatabase.DeleteAsset(assetPath)) {
						throw new InvalidOperationException("Unable to remove PlayMode fixture asset: " + assetPath);
					}
				}
			}
			if (Directory.Exists("ServerData")) FileUtil.DeleteFileOrDirectory("ServerData");
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			Debug.Log("Removed the disposable Addressables PlayMode fixture.");
		}

		private static void RequireMarker() {
			if (!Environment.GetCommandLineArgs().Contains("-torReleaseReadinessPlayMode")) {
				throw new InvalidOperationException(
					"PlayMode fixture mutation requires -torReleaseReadinessPlayMode in a disposable project.");
			}
		}
	}
}
