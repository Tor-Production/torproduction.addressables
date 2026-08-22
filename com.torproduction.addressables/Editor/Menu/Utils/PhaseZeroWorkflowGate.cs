using UnityEngine;
using TorProduction.Addressables.Editor;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class AddressablesAutomationWorkflowGate {
		internal const string DisabledReason =
			"This workflow is disabled until its planned safe analyze/apply implementation is complete.";

		internal static bool IncompleteWorkflowsEnabled => false;
		internal static bool GroupSynchronizationImplemented => true;
		internal static bool AutomaticSceneReconciliationImplemented => false;

		internal static bool CanExecute(AutomationScope scope) {
			var resolution = AddressablesAutomationContextProvider.ResolveManual(scope);
			return resolution.IsReady && scope == AutomationScope.Groups && GroupSynchronizationImplemented;
		}

		internal static bool TryBegin(string workflowName, AutomationScope scope) {
			var resolution = AddressablesAutomationContextProvider.ResolveManual(scope);
			if (!resolution.IsReady) {
				Debug.LogWarning(
					$"{workflowName}: configuration is disabled ({resolution.Status}). {resolution.Message}");
				return false;
			}
			if (scope == AutomationScope.Groups && GroupSynchronizationImplemented) {
				return true;
			}

			Debug.LogWarning($"{workflowName}: {DisabledReason}");
			return false;
		}
	}
}
