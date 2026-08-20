using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal class BuildController {
		private const string BUILD_ADDRESSABLES_STATE_KEY = "BuildAddressables";
		private const string PLATFORMS_LIST_TO_BUILD_STATE_KEY = "PlatformsToBuild";
		private const string INITIAL_PLATFORM_BEFORE_BUILD_STATE_KEY = "InitialPlatform";

		// This is a dictionary of all available build targets by TargetPlatform as
		// a key ID (it's used for storing it to the SessionState system)
		static readonly Dictionary<TargetPlatform, BuildTarget> m_buildTargetsDictionary = new() {
			{
				TargetPlatform.Android, BuildTarget.Android
			}, {
				TargetPlatform.iOS, BuildTarget.iOS
			}, {
#if UNITY_EDITOR_WIN
				TargetPlatform.EditorWindows, BuildTarget.StandaloneWindows64
#elif UNITY_EDITOR_OSX
			TargetPlatform.EditorWindows, BuildTarget.StandaloneOSX
#else // alternative for robustness
			TargetPlatform.None, BuildTarget.NoTarget
#endif
			}
		};

		internal static Dictionary<TargetPlatform, BuildTarget> TargetsDictionary => m_buildTargetsDictionary; 
		
		static void OnBuildTargetSwitched() {
			AssetDatabase.SaveAssets(); // in case of changes in serialised fields after switching a platform
			bool toBuild = SessionState.GetBool(BUILD_ADDRESSABLES_STATE_KEY, false);

			SessionState.EraseBool(BUILD_ADDRESSABLES_STATE_KEY);
			if (toBuild) {
				BuildCurrentPlatformAndSwitch();
				return;
			}

			SessionState.EraseString(INITIAL_PLATFORM_BEFORE_BUILD_STATE_KEY);
		}

		internal static void InitBuildProcess(int[] platformIDs) {
			SessionState.SetBool(BUILD_ADDRESSABLES_STATE_KEY, true);

			platformIDs = OptimizePlatformsQueue(platformIDs);
			SessionState.SetIntArray(PLATFORMS_LIST_TO_BUILD_STATE_KEY, platformIDs);

			var currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
			SessionState.SetString(INITIAL_PLATFORM_BEFORE_BUILD_STATE_KEY, currentBuildTarget.ToString());
			
			BuildCurrentPlatformAndSwitch();
		}

		static void BuildCurrentPlatformAndSwitch() {
			Debug.Log($"{nameof(BuildMenu)} -> {nameof(OnBuildTargetSwitched)} : " +
			          $"Building Addressables as requested for {EditorUserBuildSettings.activeBuildTarget}");
			
			var currentTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

			var targets = SessionState.GetIntArray(PLATFORMS_LIST_TO_BUILD_STATE_KEY, null);
			if (targets != null && targets.Length > 0) {
				var currentTargetIDToBuild = (TargetPlatform)targets[0];
				var currentTargetToBuild = m_buildTargetsDictionary[currentTargetIDToBuild];
				var currentTargetGroupToBuild = BuildPipeline.GetBuildTargetGroup(currentTargetToBuild);
				if (currentTargetGroup != currentTargetGroupToBuild) {
					EditorUserBuildSettings.SwitchActiveBuildTarget(currentTargetGroupToBuild, currentTargetToBuild);
					OnBuildTargetSwitched();
					return; 
				}

				// Update bundles
				var result = UpdateContentBundles();
				
				// Rename report file
				ReportUpdater.RenameBuildLayoutReport($"_{currentTargetIDToBuild.ToString()}");
				
				if (result == null) {
					Debug.LogError($"{nameof(BuildMenu)} -> {nameof(BuildCurrentPlatformAndSwitch)} : Unexpected error during bundles build");
					ClearBuildStateAndSwitchToInitialPlatform();
					return;
				}

				if (!string.IsNullOrEmpty(result.Error)) {
					Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildCurrentPlatformAndSwitch)} : Error during bundles build:\n{result.Error}");
				}

				// Remove first element
				targets = targets.Skip(1).ToArray();

				if (targets.Length > 0) {
					SessionState.SetBool(BUILD_ADDRESSABLES_STATE_KEY, true);
					SessionState.SetIntArray(PLATFORMS_LIST_TO_BUILD_STATE_KEY, targets);
					currentTargetIDToBuild = (TargetPlatform)targets[0];
					currentTargetToBuild = m_buildTargetsDictionary[currentTargetIDToBuild];
					if (currentTargetGroup != BuildPipeline.GetBuildTargetGroup(currentTargetToBuild)) {
						EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(currentTargetToBuild), currentTargetToBuild);	
					}
					OnBuildTargetSwitched();
					return;
				}
			}

			// now return to the initial state
			ClearBuildStateAndSwitchToInitialPlatform();
			OnBuildTargetSwitched();
		}

		// This method is used to speed up the build process if we are using several platforms to be built.
		// It reduces the count of Switch Platform processes if the selected target group is the first (or last)
		static int[] OptimizePlatformsQueue(int[] platforms) {
			var length = platforms.Length;
			if (length < 1) {
				return platforms;
			}

			var currentTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
			var firstTargetPlatform = (TargetPlatform)platforms[0];
			var lastTargetPlatform = (TargetPlatform)platforms[length - 1];

			if (currentTargetGroup == BuildPipeline.GetBuildTargetGroup(m_buildTargetsDictionary[firstTargetPlatform]) ||
			    currentTargetGroup == BuildPipeline.GetBuildTargetGroup(m_buildTargetsDictionary[lastTargetPlatform])) {
				return platforms;
			}

			int targetIndex = -1;
			for (int i = 1; i < length; ++i) {
				var platform = (TargetPlatform)platforms[i];
				if (BuildPipeline.GetBuildTargetGroup(m_buildTargetsDictionary[platform]) == currentTargetGroup) {
					targetIndex = i;
					break;
				}
			}

			if (targetIndex == -1) {
				Debug.LogError($"{nameof(BuildMenu)} -> {nameof(OptimizePlatformsQueue)} : The current platform wasn't found in the list for a build");
				return platforms;
			}

			// Swap platforms
			(platforms[0], platforms[targetIndex]) = (platforms[targetIndex], platforms[0]);

			return platforms;
		}

		static void ClearBuildStateAndSwitchToInitialPlatform() {
			SessionState.EraseIntArray(PLATFORMS_LIST_TO_BUILD_STATE_KEY);
			SessionState.EraseBool(BUILD_ADDRESSABLES_STATE_KEY);
			var initialPlatformName = SessionState.GetString(INITIAL_PLATFORM_BEFORE_BUILD_STATE_KEY, "");
			if (string.IsNullOrEmpty(initialPlatformName)) {
				Debug.LogError($"{nameof(BuildMenu)} -> {nameof(BuildCurrentPlatformAndSwitch)} : Error during retrieving the initial Build Target Group. " +
				               $"Please, check your build settings and manually switch the platform if needed");
				return;
			}

			if (!Enum.TryParse<BuildTarget>(initialPlatformName, out BuildTarget target)) {
				Debug.LogError($"{nameof(BuildMenu)} -> {nameof(BuildCurrentPlatformAndSwitch)} : Can't find a BuildTarget value for string {initialPlatformName}");
				return;
			}

			if (EditorUserBuildSettings.activeBuildTarget.ToString() != initialPlatformName) {
				var success = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
				if (success) {
					Debug.Log($"{nameof(BuildMenu)} -> {nameof(ClearBuildStateAndSwitchToInitialPlatform)} : " +
					               $"Addressables content successfully built. Current target platform is {initialPlatformName}");
				} else {
				Debug.LogError($"{nameof(BuildMenu)} -> {nameof(BuildCurrentPlatformAndSwitch)} : " +
				               $"Couldn't switch back to initial {initialPlatformName} build target due to unexpected error");
				}
			}
		}

		static AddressablesPlayerBuildResult UpdateContentBundles() {
			var settings = AddressableAssetSettingsDefaultObject.Settings;
			var statePath = Path.Combine(settings.ContentStateBuildPath, "addressables_content_state.bin");
			AddressablesRuntimeProperties.ClearCachedPropertyValues(); // The cache always keeps the previous build target so we have to clear it manually
			return ContentUpdateScript.BuildContentUpdate(settings, statePath);
		}

	}
}
