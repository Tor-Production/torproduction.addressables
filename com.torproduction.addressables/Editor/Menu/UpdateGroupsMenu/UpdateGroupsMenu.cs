using UnityEditor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
    internal static class UpdateGroupsMenu {
        [MenuItem("Tools/Tor Production/Addressables/Update Group from folder")]
        static void UpdateFromFolderButtonClick() {
            UpdateGroupsWindow.ShowWindow();
        }

    }
}
