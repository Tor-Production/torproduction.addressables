using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class BuildMenu {
		private const string BUILD_IOS_PATH = "Tools/Tor Production/Addressables/Build/Build iOS";
		private const string BUILD_ANDROID_PATH = "Tools/Tor Production/Addressables/Build/Build Android";
		private const string BUILD_EDITOR_PATH = "Tools/Tor Production/Addressables/Build/Build Editor";
		private const string BUILD_ALL_PATH = "Tools/Tor Production/Addressables/Build/Build All";

		[MenuItem(BUILD_IOS_PATH)]
		private static void BuildiOSButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Addressables iOS build")) {
				return;
			}

			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildiOSButtonClick)} : Start building iOS addressable bundles");
			var platformIDs = new[] { (int)TargetPlatform.iOS };
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem(BUILD_IOS_PATH, true)]
		private static bool ValidateBuildiOS() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;

		[MenuItem(BUILD_ANDROID_PATH)]
		private static void BuildAndroidButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Addressables Android build")) {
				return;
			}

			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildAndroidButtonClick)} : Start building Android addressable bundles");
			var platformIDs = new[] { (int)TargetPlatform.Android };
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem(BUILD_ANDROID_PATH, true)]
		private static bool ValidateBuildAndroid() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;

		[MenuItem(BUILD_EDITOR_PATH)]
		private static void BuildEditorButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Addressables editor build")) {
				return;
			}

			TargetPlatform targetPlatform = TargetPlatform.None;
			switch (Application.platform) {
				case RuntimePlatform.WindowsEditor:
					targetPlatform = TargetPlatform.EditorWindows;
					break;
				case RuntimePlatform.OSXEditor:
					targetPlatform = TargetPlatform.EditorOSX;
					break;
				default:
					throw new NotImplementedException("The current platform is not supported for building addressable bundles from this menu");
			}
			
			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildEditorButtonClick)} : Start building {targetPlatform.ToString()} addressable bundles");
			
			var platformIDs = new[] { (int)targetPlatform };
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem(BUILD_EDITOR_PATH, true)]
		private static bool ValidateBuildEditor() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;

		[MenuItem(BUILD_ALL_PATH)]
		internal static void BuildAllButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Addressables multi-platform build")) {
				return;
			}

			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildAllButtonClick)} : Start building all platforms addressable bundles");
			var platformIDs = BuildController.TargetsDictionary.Keys.Select(key => (int)key).ToArray();
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem(BUILD_ALL_PATH, true)]
		private static bool ValidateBuildAll() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;

	}
}
