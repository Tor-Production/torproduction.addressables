using UnityEditor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    internal static class DependencyResolverMenu {
        [MenuItem("Tools/Tor Production/Addressables/Resolve dependencies")]
        internal static void FixPrefabPathsButtonClick() {
            DependencyResolverController.FixPrefabPaths();
        }
    }
}
