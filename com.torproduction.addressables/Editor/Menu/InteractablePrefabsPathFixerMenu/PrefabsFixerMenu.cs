using UnityEditor;
using UnityEngine;

namespace TorProduction.AddressablesToolpack.Editor.Menu {
	internal static class PrefabsFixerMenu {
		private const string MENU_PATH = "Tools/Tor Production/Addressables/Fix Prefab Paths";

		[MenuItem(MENU_PATH)]
		internal static void FixPrefabPathsButtonClick() {
			if (!PhaseZeroWorkflowGate.TryBegin("Interactable prefab path migration")) {
				return;
			}

			Debug.Log($"{nameof(PrefabsFixerMenu)} -> {nameof(FixPrefabPathsButtonClick)} : Start changing paths of used interactable prefabs");
			PrefabsFixerController.FixPrefabPaths();
		}

		[MenuItem(MENU_PATH, true)]
		private static bool ValidateFixPrefabPaths() => PhaseZeroWorkflowGate.IncompleteWorkflowsEnabled;
	}
}
