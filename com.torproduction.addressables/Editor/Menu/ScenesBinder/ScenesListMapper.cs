using System.Linq;
using TorProduction.Addressables.Editor;
using UnityEditor;
using UnityEngine;

namespace TorProduction.Addressables.Editor {
	internal sealed class ScenesListMapper : AssetPostprocessor {
		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths) {
			ScenePostprocessCoordinator.Notify(
				(importedAssets ?? new string[0])
					.Concat(deletedAssets ?? new string[0])
					.Concat(movedAssets ?? new string[0])
					.Concat(movedFromAssetPaths ?? new string[0]),
				callback => EditorApplication.delayCall += () => callback(),
				Reconcile,
				exception => Debug.LogError($"Automatic scene reconciliation failed: {exception.Message}"));
		}

		private static void Reconcile() {
			if (!AddressablesAutomationWorkflowGate.AutomaticSceneReconciliationImplemented) return;
			var context = AddressablesAutomationContextProvider.ResolveAutomatic(AutomationScope.Scenes);
			if (!context.IsReady) return;
			var plan = AddressablesAutomation.Analyze(context.Config, AutomationScope.Scenes);
			if (!plan.IsValid) {
				Debug.LogError("Automatic scene reconciliation was blocked: " + string.Join(" | ", plan.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
				return;
			}
			if (!plan.HasChanges) return;
			var report = AddressablesAutomation.Apply(plan);
			if (!report.Succeeded) Debug.LogError("Automatic scene reconciliation failed: " + string.Join(" | ", report.Failures));
		}
	}
}
