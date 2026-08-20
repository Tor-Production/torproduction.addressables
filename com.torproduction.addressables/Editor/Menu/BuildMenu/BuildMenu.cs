using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class BuildMenu {
		[MenuItem("Tools/Tor Production/Addressables/Build/Build iOS")]
		private static void BuildiOSButtonClick() {
			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildiOSButtonClick)} : Start building iOS addressable bundles");
			var platformIDs = new[] { (int)TargetPlatform.iOS };
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem("Tools/Tor Production/Addressables/Build/Build Android")]
		private static void BuildAndroidButtonClick() {
			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildAndroidButtonClick)} : Start building Android addressable bundles");
			var platformIDs = new[] { (int)TargetPlatform.Android };
			BuildController.InitBuildProcess(platformIDs);
		}

		[MenuItem("Tools/Tor Production/Addressables/Build/Build Editor")]
		private static void BuildEditorButtonClick() {
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

		[MenuItem("Tools/Tor Production/Addressables/Build/Build All")]
		internal static void BuildAllButtonClick() {
			Debug.Log($"{nameof(BuildMenu)} -> {nameof(BuildAllButtonClick)} : Start building all platforms addressable bundles");
			var platformIDs = BuildController.TargetsDictionary.Keys.Select(key => (int)key).ToArray();
			BuildController.InitBuildProcess(platformIDs);
		}

	}
}
