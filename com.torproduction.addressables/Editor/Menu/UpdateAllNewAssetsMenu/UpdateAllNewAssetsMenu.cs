using UnityEditor;
using UnityEditor.AddressableAssets;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateAllNewAssetsMenu {
		[MenuItem("Tools/Tor Production/Addressables/Update All New Assets", priority = 0)]
		internal static void UpdateAllNewAssetsButtonClick() {
			UpdateAllNewAssetsController.UpdateAllNewAssets();
		}
		
		[MenuItem("Tools/Tor Production/Addressables/Update All New Assets and Build", priority = 0)]
		internal static void UpdateAllNewAssetsAndBuildButtonClick() {
			UpdateAllNewAssetsController.UpdateAllNewAssets();
			BuildMenu.BuildAllButtonClick();
		}
	}
}
