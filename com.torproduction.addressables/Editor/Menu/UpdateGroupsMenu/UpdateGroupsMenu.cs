using UnityEditor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    internal static class UpdateGroupsMenu {
        private const string MENU_PATH = "Tools/Tor Production/Addressables/Update Group from folder";

        [MenuItem(MENU_PATH)]
        static void UpdateFromFolderButtonClick() {
            if (!PhaseZeroWorkflowGate.TryBegin("Addressables group synchronization")) {
                return;
            }

            UpdateGroupsWindow.ShowWindow();
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateUpdateFromFolder() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;
    }
}
