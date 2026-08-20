using UnityEditor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    internal static class DependencyResolverMenu {
        private const string MENU_PATH = "Tools/Tor Production/Addressables/Resolve dependencies";

        [MenuItem(MENU_PATH)]
        internal static void FixPrefabPathsButtonClick() {
            if (!PhaseZeroWorkflowGate.TryBegin("Addressables duplicate-dependency resolution")) {
                return;
            }

            DependencyResolverController.FixPrefabPaths();
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateResolveDependencies() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;
    }
}
