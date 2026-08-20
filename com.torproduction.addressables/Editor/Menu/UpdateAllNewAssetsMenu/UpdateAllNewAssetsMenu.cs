using UnityEditor;
using UnityEditor.AddressableAssets;
using TorProduction.Addressables.Editor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateAllNewAssetsMenu {
		private const string UPDATE_ALL_PATH = "Tools/Tor Production/Addressables/Update All New Assets";
		private const string UPDATE_AND_BUILD_PATH = "Tools/Tor Production/Addressables/Update All New Assets and Build";

		[MenuItem(UPDATE_ALL_PATH, priority = 0)]
		internal static void UpdateAllNewAssetsButtonClick() {
			if (!AddressablesAutomationWorkflowGate.TryBegin("Update All New Assets", AutomationScope.All)) {
				return;
			}

			UpdateAllNewAssetsController.UpdateAllNewAssets();
		}

		[MenuItem(UPDATE_ALL_PATH, true)]
		private static bool ValidateUpdateAll() => AddressablesAutomationWorkflowGate.CanExecute(AutomationScope.All);

		[MenuItem(UPDATE_AND_BUILD_PATH, priority = 0)]
		internal static void UpdateAllNewAssetsAndBuildButtonClick() {
			if (!AddressablesAutomationWorkflowGate.TryBegin("Update All New Assets and Build", AutomationScope.All)) {
				return;
			}

			UpdateAllNewAssetsController.UpdateAllNewAssets();
			BuildMenu.BuildAllButtonClick();
		}

		[MenuItem(UPDATE_AND_BUILD_PATH, true)]
		private static bool ValidateUpdateAllAndBuild() => AddressablesAutomationWorkflowGate.CanExecute(AutomationScope.All);
	}
}
