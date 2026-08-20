using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class PhaseZeroWorkflowGate {
		internal const string DisabledReason =
			"This workflow is disabled in 0.1.0-preview.1 until its safe analyze/apply implementation is complete.";

		internal static bool IncompleteWorkflowsEnabled => false;
		internal static bool AutomaticSceneProcessingEnabled => false;

		internal static bool TryBegin(string workflowName) {
			if (IncompleteWorkflowsEnabled) {
				return true;
			}

			Debug.LogWarning($"{workflowName}: {DisabledReason}");
			return false;
		}
	}
}
