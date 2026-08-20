using UnityEditor;
using TorProduction.Addressables.Editor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    internal static class UpdateGroupsMenu {
        private const string MENU_PATH = "Tools/Tor Production/Addressables/Update Group from folder";

        [MenuItem(MENU_PATH)]
        static void UpdateFromFolderButtonClick() {
            if (!AddressablesAutomationWorkflowGate.TryBegin("Addressables group synchronization", AutomationScope.Groups)) {
                return;
            }

            UpdateGroupsWindow.ShowWindow();
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateUpdateFromFolder() => AddressablesAutomationWorkflowGate.CanExecute(AutomationScope.Groups);
    }
}
