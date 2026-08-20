using UnityEditor.AddressableAssets;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class DependencyResolverController {
		internal static void FixPrefabPaths() {
			var settings = AddressableAssetSettingsDefaultObject.Settings;
			new CustomCheckBundleDupeDependencies().FixIssues(settings);
		}
	}
}
