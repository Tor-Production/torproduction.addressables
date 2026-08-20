namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class UpdateAllNewAssetsController {
		internal static void UpdateAllNewAssets() {
			AddressablesAutomationWorkflowGate.TryBegin(
				"Update All New Assets", TorProduction.Addressables.Editor.AutomationScope.All);
		}
	}
}
