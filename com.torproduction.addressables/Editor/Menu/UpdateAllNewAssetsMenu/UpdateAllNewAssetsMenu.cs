using UnityEditor;
using UnityEditor.AddressableAssets;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateAllNewAssetsMenu {
		private const string UPDATE_ALL_PATH = "Tools/Tor Production/Addressables/Update All New Assets";
		private const string UPDATE_AND_BUILD_PATH = "Tools/Tor Production/Addressables/Update All New Assets and Build";

		[MenuItem(UPDATE_ALL_PATH, priority = 0)]
		internal static void UpdateAllNewAssetsButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Update All New Assets")) {
				return;
			}

			UpdateAllNewAssetsController.UpdateAllNewAssets();
		}

		[MenuItem(UPDATE_ALL_PATH, true)]
		private static bool ValidateUpdateAll() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;

		[MenuItem(UPDATE_AND_BUILD_PATH, priority = 0)]
		internal static void UpdateAllNewAssetsAndBuildButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Update All New Assets and Build")) {
				return;
			}

			UpdateAllNewAssetsController.UpdateAllNewAssets();
			BuildMenu.BuildAllButtonClick();
		}

		[MenuItem(UPDATE_AND_BUILD_PATH, true)]
		private static bool ValidateUpdateAllAndBuild() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;
	}
}
