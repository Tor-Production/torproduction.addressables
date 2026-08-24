using UnityEditor;

namespace TorProduction.Addressables.Editor {
    internal static class DependencyResolverMenu {
        private const string MENU_PATH = "Tools/Tor Production/Addressables/Analyze Duplicate Dependencies...";

        [MenuItem(MENU_PATH)]
        internal static void OpenAnalyzeOnlyWorkflow() {
            SettingsService.OpenProjectSettings(AddressablesAutomationSettingsProvider.SettingsPath);
        }
    }
}
